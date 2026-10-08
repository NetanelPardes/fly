using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace Airport.Api.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Database update failed");
            await WriteProblemAsync(context, HttpStatusCode.Conflict, "The requested change conflicts with existing data.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled request failure");
            await WriteProblemAsync(context, HttpStatusCode.InternalServerError, "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, HttpStatusCode statusCode, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        var payload = new
        {
            title = message,
            status = context.Response.StatusCode,
            traceId = context.TraceIdentifier
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
