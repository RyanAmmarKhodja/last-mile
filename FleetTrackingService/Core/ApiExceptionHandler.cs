using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StackExchange.Redis;

namespace LastMile.Tracking.Core;

/// <summary>Maps domain exceptions to HTTP responses; Redis or PostgreSQL outages become 503 (NFR-3).</summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        switch (exception)
        {
            case DomainException domain:
                return await WriteProblem(context, domain.StatusCode, domain.Title, domain.Message);

            case RedisConnectionException or RedisTimeoutException:
                logger.LogWarning("Redis unavailable: {Message}", exception.Message);
                return await WriteProblem(context, StatusCodes.Status503ServiceUnavailable, "Service unavailable",
                    "The live position store is unavailable. Try again shortly.");

            case NpgsqlException { IsTransient: true } or TimeoutException:
                logger.LogWarning("Database unavailable: {Message}", exception.Message);
                return await WriteProblem(context, StatusCodes.Status503ServiceUnavailable, "Service unavailable",
                    "The tracking database is unavailable. Try again shortly.");

            default:
                return false;
        }
    }

    private ValueTask<bool> WriteProblem(HttpContext context, int status, string title, string detail)
    {
        if (context.Response.HasStarted) return ValueTask.FromResult(false); // e.g. an SSE stream already running
        context.Response.StatusCode = status;
        return problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }
}
