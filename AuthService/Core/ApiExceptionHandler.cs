using LastMile.Auth.DTOs;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LastMile.Auth.Core;

/// <summary>Maps domain/OAuth exceptions to HTTP responses; anything else becomes a generic 500.</summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        switch (exception)
        {
            case OAuthException oauth:
                context.Response.StatusCode = oauth.StatusCode;
                context.Response.Headers.CacheControl = "no-store";
                if (oauth.StatusCode == StatusCodes.Status401Unauthorized)
                    context.Response.Headers.WWWAuthenticate = "Basic realm=\"auth-service\"";
                await context.Response.WriteAsJsonAsync(new OAuthErrorDto(oauth.Error, oauth.Message), ct);
                return true;

            case DomainException domain:
                return await WriteProblem(context, domain.StatusCode, domain.Title, domain.Message);

            case DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }:
                return await WriteProblem(context, StatusCodes.Status409Conflict, "Conflict",
                    "The resource already exists.");

            default:
                return false;
        }
    }

    private ValueTask<bool> WriteProblem(HttpContext context, int status, string title, string detail)
    {
        context.Response.StatusCode = status;
        return problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }
}
