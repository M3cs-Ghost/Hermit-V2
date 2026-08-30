using System.Threading;
using System.Threading.Tasks;

namespace Hermit.Networking
{
    /// <summary>
    /// Contract for backend reads: PostgREST table reads (GetAsync) and RPC
    /// calls (InvokeRpcAsync). accessToken is always explicit — this client
    /// never stores or infers identity itself.
    ///
    /// Refined during C4 from the C3 draft, which only had InvokeRpcAsync:
    /// reading a table row (profile, wallet) is not an RPC in PostgREST, it
    /// needed its own method. accessToken was also added — the C3 draft had
    /// no way to authenticate a call at all.
    /// </summary>
    public interface IBackendClient
    {
        Task<TResponse> GetAsync<TResponse>(string path, string accessToken, CancellationToken cancellationToken);

        Task<TResponse> InvokeRpcAsync<TRequest, TResponse>(
            string rpcName,
            TRequest payload,
            string accessToken,
            CancellationToken cancellationToken);
    }
}
