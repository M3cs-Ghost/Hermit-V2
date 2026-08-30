using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Hermit.Core;

namespace Hermit.Networking
{
    /// <summary>
    /// Direct REST implementation of IBackendClient against PostgREST — table
    /// reads via GET {url}/rest/v1/{path}, RPC via POST {url}/rest/v1/rpc/{name}.
    /// No third-party SDK.
    /// </summary>
    public sealed class SupabaseRestBackendClient : IBackendClient
    {
        [Serializable] private struct PostgrestErrorBody { public string message; public string details; public string hint; public string code; }
        [Serializable] private struct JsonArrayWrapper<T> { public T[] items; }

        private readonly EnvironmentConfig _config;
        private readonly IConnectivityService _connectivity;

        public SupabaseRestBackendClient(EnvironmentConfig config, IConnectivityService connectivity)
        {
            _config = config;
            _connectivity = connectivity;
        }

        public async Task<TResponse> GetAsync<TResponse>(string path, string accessToken, CancellationToken cancellationToken)
        {
            var url = $"{_config.SupabaseUrl}/rest/v1/{path}";
            using var request = UnityWebRequest.Get(url);
            ApplyHeaders(request, accessToken);

            var response = await UnityWebRequestAsync.SendAsync(request, cancellationToken);
            return HandleResponse<TResponse>(response);
        }

        public async Task<TResponse> InvokeRpcAsync<TRequest, TResponse>(
            string rpcName, TRequest payload, string accessToken, CancellationToken cancellationToken)
        {
            var url = $"{_config.SupabaseUrl}/rest/v1/rpc/{rpcName}";
            var json = JsonUtility.ToJson(payload);

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            ApplyHeaders(request, accessToken);

            var response = await UnityWebRequestAsync.SendAsync(request, cancellationToken);
            return HandleResponse<TResponse>(response);
        }

        private void ApplyHeaders(UnityWebRequest request, string accessToken)
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("apikey", _config.SupabaseAnonKey);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization",
                $"Bearer {(string.IsNullOrEmpty(accessToken) ? _config.SupabaseAnonKey : accessToken)}");
        }

        private TResponse HandleResponse<TResponse>(UnityWebRequest response)
        {
            _connectivity.ReportBackendReachable(response.result != UnityWebRequest.Result.ConnectionError);

            if (response.result == UnityWebRequest.Result.ConnectionError)
            {
                var detail = $"Backend network error: {response.error}";
                HermitLog.Warning(detail);
                throw new HermitException(new HermitError(HermitErrorKind.Network,
                    "No se pudo conectar con el servidor. Revisa tu conexión e intenta de nuevo.", detail));
            }

            var body = response.downloadHandler.text;

            if (response.responseCode == 401)
            {
                HermitLog.Warning($"Backend session expired: {body}");
                throw new HermitException(new HermitError(HermitErrorKind.SessionExpired,
                    "Tu sesión expiró. Inicia sesión de nuevo.", body));
            }

            if (response.responseCode >= 400)
            {
                string friendly = "El servidor rechazó la solicitud.";
                try
                {
                    var parsed = JsonUtility.FromJson<PostgrestErrorBody>(body);
                    if (!string.IsNullOrEmpty(parsed.message))
                    {
                        friendly = "El servidor rechazó la solicitud: " + parsed.message;
                    }
                }
                catch
                {
                    // Body wasn't the expected Postgrest error shape — fall back
                    // to the generic friendly message, never show raw body to UI.
                }

                HermitLog.Warning($"Backend error {response.responseCode}: {body}");
                throw new HermitException(new HermitError(HermitErrorKind.Backend, friendly, body));
            }

            if (typeof(TResponse) == typeof(string))
            {
                return (TResponse)(object)body;
            }

            try
            {
                var text = body.TrimStart();
                if (text.StartsWith("["))
                {
                    // PostgREST wraps table/RPC-set results in a JSON array;
                    // unwrap the first element for callers that asked for a
                    // single object.
                    var wrapped = JsonUtility.FromJson<JsonArrayWrapper<TResponse>>("{\"items\":" + body + "}");
                    return wrapped.items != null && wrapped.items.Length > 0 ? wrapped.items[0] : default;
                }

                return JsonUtility.FromJson<TResponse>(body);
            }
            catch (Exception ex)
            {
                var detail = $"Backend response parsing failed: {ex.Message}. Body: {body}";
                HermitLog.Error(detail);
                throw new HermitException(new HermitError(HermitErrorKind.Parsing,
                    "Ocurrió un error inesperado al leer la respuesta del servidor.", detail));
            }
        }
    }
}
