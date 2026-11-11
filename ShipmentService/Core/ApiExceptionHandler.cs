using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LastMile.Shipments.Core;

/// <summary>Maps domain exceptions to HTTP responses; anything else becomes a generic 500.</summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        switch (exception)
        {
            case DependencyUnavailableException dep:
                logger.LogWarning(dep.Cause, "Dependency {Service} unavailable: {Message}", dep.Service, dep.Message);
                return await WriteProblem(context, dep.StatusCode, dep.Title, dep.Message);

            case DomainException domain:
                return await WriteProblem(context, domain.StatusCode, domain.Title, domain.Message);

            case DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg }:
                return await WriteProblem(context, StatusCodes.Status409Conflict, "Conflict", pg.ConstraintName switch
                {
                    // D3: a driver carries one shipment at a time (also closes the concurrent-dispatch race).
                    "ux_shipments_driver_active" => "This driver already has an active shipment.",
                    _ => "The resource already exists."
                });

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
