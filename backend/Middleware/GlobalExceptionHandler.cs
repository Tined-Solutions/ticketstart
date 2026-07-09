using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Helpers;

namespace TicketeraOnline.Api.Middleware;

/// <summary>
/// Centralized exception handler implementing ASP.NET Core IExceptionHandler.
/// Maps unhandled exceptions to appropriate HTTP status codes, user-friendly responses,
/// and structured logs without exposing sensitive information.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        try
        {
            var (statusCode, errorCode, message) = MapException(exception);

            var path = httpContext.Request.Path.ToString();
            var query = LogRedactor.RedactQueryString(httpContext.Request.QueryString.ToString());
            var pathAndQuery = path + query;
            var method = httpContext.Request.Method;
            var correlationId = httpContext.TraceIdentifier;

            if (exception is OperationCanceledException)
            {
                _logger.LogInformation("Client disconnected during request");
                httpContext.Response.StatusCode = 499;
                return true;
            }
            else
            {
                _logger.LogError(
                    "Unhandled exception {ExceptionType} on {Method} {Path} with correlation {CorrelationId} — {ErrorCode}: {Message} {StackTrace}",
                    exception.GetType().Name,
                    method,
                    pathAndQuery,
                    correlationId,
                    errorCode,
                    LogRedactor.RedactMessage(message),
                    exception.StackTrace);
            }

            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/problem+json";

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = errorCode,
                Detail = message,
                Instance = pathAndQuery
            };

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            return true;
        }
        catch (Exception)
        {
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            httpContext.Response.ContentType = "application/problem+json";
            await httpContext.Response.WriteAsync("{\"code\":\"INTERNAL_ERROR\",\"message\":\"An internal error occurred.\"}");
            return true;
        }
    }

    private static (int StatusCode, string ErrorCode, string Message) MapException(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => (499, "CLIENT_CLOSED", "Client disconnected during request."),
            ArgumentException => (StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "The request is invalid. Please check the provided data."),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Authentication is required to access this resource."),
            Models.ForbiddenException => (StatusCodes.Status403Forbidden, "FORBIDDEN", "You do not have permission to perform this action."),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "NOT_FOUND", "The requested resource was not found."),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "CONFLICT", "The resource was modified by another request. Please try again."),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "An unexpected error occurred. Please try again later.")
        };
    }
}
