namespace TicketeraOnline.Api.Middleware;

/// <summary>
/// Middleware that requires X-CSRF-PROTECT header for state-changing requests.
/// GET and OPTIONS always pass through. POST /webhook is exempt (MercadoPago callback).
/// </summary>
public class CsrfHeaderMiddleware
{
    private readonly RequestDelegate _next;

    public CsrfHeaderMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;

        // GET and OPTIONS always pass through
        if (method == HttpMethods.Get || method == HttpMethods.Options)
        {
            await _next(context);
            return;
        }

        // POST /webhook and POST /api/auth/login are exempt
        // (MercadoPago callbacks and login — no CSRF token available yet)
        if (method == HttpMethods.Post &&
            (path.StartsWith("/webhook", StringComparison.OrdinalIgnoreCase) ||
             path.StartsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Require X-CSRF-PROTECT header for POST, PUT, PATCH, DELETE
        if (method == HttpMethods.Post ||
            method == HttpMethods.Put ||
            method == HttpMethods.Patch ||
            method == HttpMethods.Delete)
        {
            if (!context.Request.Headers.ContainsKey("X-CSRF-PROTECT"))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\":\"CSRF header required\"}");
                return;
            }
        }

        await _next(context);
    }
}
