using System.Net;
using System.Text.Json;

namespace EmployeeService.Middleware;

// catch errors and return json
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
            logger.LogError(ex, "Unhandled exception in EmployeeService: {Message}", ex.Message);
            await WriteErrorResponse(context, ex);
        }
    }

    private static async Task WriteErrorResponse(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message) = ex switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized"),
            KeyNotFoundException        => (HttpStatusCode.NotFound,     ex.Message),
            ArgumentException           => (HttpStatusCode.BadRequest,   ex.Message),
            InvalidOperationException   => (HttpStatusCode.Conflict,     ex.Message),
            _                           => (HttpStatusCode.InternalServerError, "An unexpected error occurred")
        };

        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status  = (int)statusCode,
            error   = statusCode.ToString(),
            message,
            traceId = context.TraceIdentifier
        }));
    }
}
