using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LastMile.Profile.Core;

/// <summary>Maps domain exceptions to HTTP responses; anything else becomes a generic 500.</summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        switch (exception)
        {
            case DomainException domain:
                return await WriteProblem(context, domain.StatusCode, domain.Title, domain.Message);

            case DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }:
                // e.g. PUT /me racing the UserRegistered event for the same user.
                return await WriteProblem(context, StatusCodes.Status409Conflict, "Conflict",
                    "The profile already exists; retry the request.");

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
