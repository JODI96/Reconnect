using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Reconnect.Client.Networking
{
    public static class UnityWebRequestExtensions
    {
        /// <summary>Awaitable wrapper; completes on the main thread when Unity finishes the request.</summary>
        public static Task AsTask(this UnityWebRequestAsyncOperation operation)
        {
            if (operation.isDone)
            {
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>();
            operation.completed += _ => tcs.TrySetResult(true);
            return tcs.Task;
        }
    }
}
