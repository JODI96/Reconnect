using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Reconnect.Client.Networking
{
    public sealed class UnityWebRequestTransport : IHttpTransport
    {
        private readonly int _timeoutSeconds;

        public UnityWebRequestTransport(int timeoutSeconds)
        {
            _timeoutSeconds = timeoutSeconds;
        }

        public async Task<HttpResponse> SendAsync(HttpRequest request, CancellationToken ct)
        {
            using var web = new UnityWebRequest(request.Url, request.Method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = _timeoutSeconds,
            };

            if (request.JsonBody != null)
            {
                web.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.JsonBody));
                web.SetRequestHeader("Content-Type", "application/json");
            }
            web.SetRequestHeader("Accept", "application/json");
            if (request.BearerToken != null)
            {
                web.SetRequestHeader("Authorization", "Bearer " + request.BearerToken);
            }

            using (ct.Register(web.Abort))
            {
                await web.SendWebRequest().AsTask();
            }
            ct.ThrowIfCancellationRequested();

            return web.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.DataProcessingError
                ? new HttpResponse(0, null, web.error)
                : new HttpResponse(web.responseCode, web.downloadHandler.text);
        }
    }
}
