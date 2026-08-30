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
    /// Direct REST implementation of IAuthService against Supabase's GoTrue
    /// Auth API (POST {url}/auth/v1/token, POST {url}/auth/v1/logout). No
    /// third-party SDK — see Docs/C4_SUPABASE_SPIKE.md for why REST was chosen
    /// over supabase-csharp for this spike.
    /// </summary>
    public sealed class SupabaseAuthService : IAuthService
    {
        [Serializable] private struct PasswordGrantRequest { public string email; public string password; }
        [Serializable] private struct RefreshGrantRequest { public string refresh_token; }
        [Serializable] private struct TokenResponse { public string access_token; public string refresh_token; public int expires_in; public AuthUser user; }
        [Serializable] private struct AuthUser { public string id; }
        [Serializable] private struct GoTrueErrorBody { public string error_description; public string msg; public string error; }

        private readonly EnvironmentConfig _config;
        private readonly IConnectivityService _connectivity;

        public SupabaseAuthService(EnvironmentConfig config, IConnectivityService connectivity)
        {
            _config = config;
            _connectivity = connectivity;
        }

        public Task<HermitSession> SignInAsync(string email, string password, CancellationToken cancellationToken)
        {
            var json = JsonUtility.ToJson(new PasswordGrantRequest { email = email, password = password });
            return PostTokenAsync("password", json, cancellationToken);
        }

        public Task<HermitSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            var json = JsonUtility.ToJson(new RefreshGrantRequest { refresh_token = refreshToken });
            return PostTokenAsync("refresh_token", json, cancellationToken);
        }

        public async Task SignOutAsync(string accessToken, CancellationToken cancellationToken)
        {
            var url = $"{_config.SupabaseUrl}/auth/v1/logout";
            using var request = new UnityWebRequest(url, "POST");
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("apikey", _config.SupabaseAnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

            var response = await UnityWebRequestAsync.SendAsync(request, cancellationToken);
            _connectivity.ReportBackendReachable(response.result != UnityWebRequest.Result.ConnectionError);

            // A 401/404 here just means the session was already invalid server
            // side — that is still an effective logout from the client's point
            // of view. Only a real network failure is worth surfacing.
            if (response.result == UnityWebRequest.Result.ConnectionError)
            {
                throw NetworkError(response);
            }
        }

        private async Task<HermitSession> PostTokenAsync(string grantType, string jsonBody, CancellationToken cancellationToken)
        {
            if (!_config.IsConfigured)
            {
                throw new HermitException(new HermitError(HermitErrorKind.Unknown,
                    "La configuración de entorno no está lista.",
                    "EnvironmentConfig.IsConfigured == false (missing URL/anon key)"));
            }

            var url = $"{_config.SupabaseUrl}/auth/v1/token?grant_type={grantType}";
            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("apikey", _config.SupabaseAnonKey);
            request.SetRequestHeader("Content-Type", "application/json");

            var response = await UnityWebRequestAsync.SendAsync(request, cancellationToken);
            _connectivity.ReportBackendReachable(response.result != UnityWebRequest.Result.ConnectionError);

            if (response.result == UnityWebRequest.Result.ConnectionError)
            {
                throw NetworkError(response);
            }

            var body = response.downloadHandler.text;

            if (response.responseCode == 400 || response.responseCode == 401)
            {
                throw InvalidCredentialsError(body);
            }

            if (response.responseCode >= 400)
            {
                throw BackendError(response.responseCode, body);
            }

            TokenResponse parsed;
            try
            {
                parsed = JsonUtility.FromJson<TokenResponse>(body);
            }
            catch (Exception ex)
            {
                throw ParsingError(body, ex);
            }

            if (string.IsNullOrEmpty(parsed.access_token))
            {
                throw ParsingError(body, null);
            }

            return new HermitSession
            {
                UserId = parsed.user.id,
                AccessToken = parsed.access_token,
                RefreshToken = parsed.refresh_token,
                ExpiresAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + parsed.expires_in
            };
        }

        private static HermitException NetworkError(UnityWebRequest response)
        {
            var detail = $"Auth network error: {response.error}";
            HermitLog.Warning(detail);
            return new HermitException(new HermitError(HermitErrorKind.Network,
                "No se pudo conectar con el servidor. Revisa tu conexión e intenta de nuevo.", detail));
        }

        private static HermitException InvalidCredentialsError(string body)
        {
            HermitLog.Warning($"Auth invalid credentials: {body}");
            return new HermitException(new HermitError(HermitErrorKind.InvalidCredentials,
                "Correo o contraseña incorrectos.", body));
        }

        private static HermitException BackendError(long statusCode, string body)
        {
            var detail = $"Auth backend error {statusCode}: {body}";
            HermitLog.Warning(detail);
            return new HermitException(new HermitError(HermitErrorKind.Backend,
                "El servidor no pudo procesar la solicitud. Intenta más tarde.", detail));
        }

        private static HermitException ParsingError(string body, Exception ex)
        {
            var detail = $"Auth response parsing failed: {ex?.Message}. Body: {body}";
            HermitLog.Error(detail);
            return new HermitException(new HermitError(HermitErrorKind.Parsing,
                "Ocurrió un error inesperado. Intenta de nuevo.", detail));
        }
    }
}
