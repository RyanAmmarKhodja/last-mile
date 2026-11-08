namespace LastMile.Auth.Core;

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

public sealed class AuthenticationFailedException(string message) : DomainException(message)
{
    public override int StatusCode => StatusCodes.Status401Unauthorized;
    public override string Title => "Authentication failed";
}

public sealed class ForbiddenException(string message) : DomainException(message)
{
    public override int StatusCode => StatusCodes.Status403Forbidden;
    public override string Title => "Forbidden";
}

/// <summary>
/// OAuth 2.0 token endpoint error (RFC 6749 §5.2). Rendered as { "error", "error_description" }
/// rather than problem details, so standard OAuth clients understand it.
/// </summary>
public sealed class OAuthException(string error, string description, int statusCode = StatusCodes.Status400BadRequest)
    : Exception(description)
{
    public string Error { get; } = error;
    public int StatusCode { get; } = statusCode;

    public static OAuthException InvalidClient() =>
        new("invalid_client", "Client authentication failed.", StatusCodes.Status401Unauthorized);
    public static OAuthException InvalidRequest(string description) => new("invalid_request", description);
    public static OAuthException UnsupportedGrantType() =>
        new("unsupported_grant_type", "Only the client_credentials grant is supported.");
    public static OAuthException InvalidScope(string description) => new("invalid_scope", description);
    public static OAuthException InvalidTarget(string description) => new("invalid_target", description);
}
