namespace LastMile.Profile.Core;

/// <summary>
/// Field rules shared by both profile types. The limits match AuthService's registration rules
/// (AuthService/Core/RegistrationProfileRules.cs), so data accepted at registration is always valid here.
/// </summary>
public static class ProfileRules
{
    public const int MaxNameLength = 100;
    public const int MaxPhoneLength = 50;
    public const int MaxCompanyLength = 255;
    public const int MaxAddressLength = 1000;

    /// <summary>Trimmed value. Throws when missing or too long.</summary>
    public static string Required(string? value, string field, int maxLength)
    {
        var v = Clean(value) ?? throw new DomainValidationException($"{field} is required.");
        return v.Length <= maxLength ? v : throw new DomainValidationException($"{field} must be at most {maxLength} characters.");
    }

    /// <summary>Trimmed value, or null when empty. Throws when too long.</summary>
    public static string? Optional(string? value, string field, int maxLength)
    {
        var v = Clean(value);
        return v is null || v.Length <= maxLength
            ? v
            : throw new DomainValidationException($"{field} must be at most {maxLength} characters.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
