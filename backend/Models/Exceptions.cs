namespace TicketeraOnline.Api.Models;

/// <summary>
/// Thrown when an authenticated user attempts an action they are not authorized to perform.
/// Maps to HTTP 403 Forbidden in the global exception handler.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }

    public ForbiddenException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
