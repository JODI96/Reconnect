namespace Reconnect.Domain.Common;

/// <summary>Violation of a business rule. The API maps it to a 400 ProblemDetails response.</summary>
public sealed class DomainException(string message) : Exception(message);
