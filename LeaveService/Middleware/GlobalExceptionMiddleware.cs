using System.Net;
using System.Text.Json;

namespace LeaveService.Middleware;

public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in LeaveService: {Message}", ex.Message);
            context.Response.ContentType = "application/json";

            var (status, msg) = ex switch
            {
                UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized"),
                KeyNotFoundException        => (HttpStatusCode.NotFound,     ex.Message),
                ArgumentException           => (HttpStatusCode.BadRequest,   ex.Message),
                InvalidOperationException   => (HttpStatusCode.Conflict,     ex.Message),
                _                           => (HttpStatusCode.InternalServerError, "An unexpected error occurred")
            };

            context.Response.StatusCode = (int)status;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                status  = (int)status,
                error   = status.ToString(),
                message = msg,
                traceId = context.TraceIdentifier
            }));
        }
    }
}
