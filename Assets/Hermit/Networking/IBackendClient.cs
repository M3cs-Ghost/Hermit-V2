using System.Threading;
using System.Threading.Tasks;

namespace Hermit.Networking
{
    /// <summary>
    /// Contract for invoking a single backend operation (a Postgrest RPC today,
    /// potentially something else later). Request/response are left generic on
    /// purpose so this interface does not assume which client implements it.
    /// </summary>
    public interface IBackendClient
    {
        Task<TResponse> InvokeRpcAsync<TRequest, TResponse>(
            string rpcName,
            TRequest payload,
            CancellationToken cancellationToken);
    }
}
