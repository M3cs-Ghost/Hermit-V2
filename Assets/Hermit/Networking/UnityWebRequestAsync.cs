using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Hermit.Networking
{
    /// <summary>
    /// Bridges UnityWebRequest's AsyncOperation to Task/async-await without any
    /// external package (no UniTask). Internal — not part of the public
    /// contract, only used by the concrete Supabase*Service implementations.
    /// </summary>
    internal static class UnityWebRequestAsync
    {
        public static Task<UnityWebRequest> SendAsync(UnityWebRequest request, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<UnityWebRequest>();
            var operation = request.SendWebRequest();

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    if (!operation.isDone)
                    {
                        request.Abort();
                    }
                });
            }

            operation.completed += _ => tcs.TrySetResult(request);

            return tcs.Task;
        }
    }
}
