using Microsoft.AspNetCore.Diagnostics;
using Reconnect.Domain.Common;

namespace Reconnect.Api.Common.Errors;

/// <summary>Turns a violated business rule into a 400 ProblemDetails response.</summary>
public sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Business rule violated",
                Detail = domainException.Message,
            },
        });
    }
}
