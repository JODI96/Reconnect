using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Reconnect.Client.Networking
{
    /// <summary>Outcome of an API call. Either <see cref="Value"/> or <see cref="Error"/> is set.</summary>
    public sealed class ApiResult<T>
    {
        private ApiResult(T value, long statusCode, ApiError error)
        {
            Value = value;
            StatusCode = statusCode;
            Error = error;
        }

        public T Value { get; }
        public long StatusCode { get; }
        public ApiError Error { get; }
        public bool IsSuccess => Error == null;

        public static ApiResult<T> Success(T value, long statusCode) => new(value, statusCode, null);

        public static ApiResult<T> Failure(ApiError error, long statusCode) => new(default, statusCode, error);
    }

    /// <summary>User-presentable error built from a ProblemDetails body or a network failure.</summary>
    public sealed class ApiError
    {
        public ApiError(string message, IReadOnlyDictionary<string, string[]> fieldErrors = null)
        {
            Message = message;
            FieldErrors = fieldErrors ?? new Dictionary<string, string[]>();
        }

        public string Message { get; }

        /// <summary>Validation errors per field (keys as sent by the server, e.g. "BirthDate").</summary>
        public IReadOnlyDictionary<string, string[]> FieldErrors { get; }

        /// <summary>Message plus all field errors, one per line – good enough for a status label.</summary>
        public string ToDisplayString() =>
            FieldErrors.Count == 0
                ? Message
                : string.Join("\n", FieldErrors.SelectMany(f => f.Value));

        public static ApiError FromResponse(HttpResponse response)
        {
            if (response.NetworkError != null)
            {
                return new ApiError("Server nicht erreichbar. Läuft das Backend? (" + response.NetworkError + ")");
            }

            ProblemDetailsBody problem = null;
            try
            {
                problem = string.IsNullOrEmpty(response.Body) ? null : Json.Deserialize<ProblemDetailsBody>(response.Body);
            }
            catch (JsonException)
            {
                // Not a ProblemDetails body – fall through to the status-code message.
            }

            var message = problem?.Detail ?? problem?.Title ?? DefaultMessage(response.StatusCode);
            return new ApiError(message, problem?.Errors);
        }

        private static string DefaultMessage(long statusCode) => statusCode switch
        {
            401 => "Nicht angemeldet oder Anmeldung abgelaufen.",
            403 => "Keine Berechtigung.",
            404 => "Nicht gefunden.",
            _ => "Unerwarteter Fehler (HTTP " + statusCode + ").",
        };

        private sealed class ProblemDetailsBody
        {
            public string Title { get; set; }
            public string Detail { get; set; }
            public Dictionary<string, string[]> Errors { get; set; }
        }
    }
}
