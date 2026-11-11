namespace LastMile.Tracking.Core;

/// <summary>Base type for business-rule failures; translated to RFC 7807 problem responses.</summary>
public abstract class DomainException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }
    public abstract string Title { get; }
}

public sealed class DomainValidationException(string message) : DomainException(message)
{
    public override int StatusCode => StatusCodes.Status400BadRequest;
    public override string Title => "Validation failed";
}

public sealed class NotFoundException(string message) : DomainException(message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
    public override string Title => "Not found";
}

public sealed class ConflictException(string message) : DomainException(message)
{
    public override int StatusCode => StatusCodes.Status409Conflict;
    public override string Title => "Conflict";
}

public sealed class ForbiddenException(string message) : DomainException(message)
{
    public override int StatusCode => StatusCodes.Status403Forbidden;
    public override string Title => "Forbidden";
}

public sealed class ServiceUnavailableException(string message) : DomainException(message)
{
    public override int StatusCode => StatusCodes.Status503ServiceUnavailable;
    public override string Title => "Service unavailable";
}
