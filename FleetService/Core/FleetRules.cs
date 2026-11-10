namespace LastMile.Fleet.Core;

/// <summary>
/// Field rules for drivers and vehicles. Name/phone/license limits match AuthService's registration rules
/// (AuthService/Core/RegistrationProfileRules.cs), so data accepted at registration is valid here too.
/// </summary>
public static class FleetRules
{
    public const int MaxNameLength = 100;
    public const int MaxPhoneLength = 50;
    public const int MaxLicenseLength = 100;
    public const int MaxPlateLength = 50;
    public const int MaxModelLength = 100;
    public const decimal MaxCapacityKg = 100_000m;

    /// <summary>Trimmed value. Throws when missing or too long.</summary>
    public static string Required(string? value, string field, int maxLength)
    {
        var v = string.IsNullOrWhiteSpace(value) ? throw new DomainValidationException($"{field} is required.") : value.Trim();
        return v.Length <= maxLength ? v : throw new DomainValidationException($"{field} must be at most {maxLength} characters.");
    }

    /// <summary>
    /// Identifier normalization for license numbers and plates: trimmed, inner whitespace collapsed, upper-case, so
    /// uniqueness ignores formatting ("dz-12 3" and "DZ-12 3" are the same license).
    /// </summary>
    public static string Identifier(string? value, string field, int maxLength)
    {
        var v = Required(value, field, maxLength);
        return string.Join(' ', v.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    }

    public static string NormalizeLicense(string? value) => Identifier(value, "License number", MaxLicenseLength);
    public static string NormalizePlate(string? value) => Identifier(value, "License plate", MaxPlateLength);

    public static decimal Capacity(decimal? value)
    {
        if (value is null or <= 0) throw new DomainValidationException("Capacity must be greater than 0 kg.");
        if (value > MaxCapacityKg) throw new DomainValidationException($"Capacity must be at most {MaxCapacityKg} kg.");
        return decimal.Round(value.Value, 2);
    }
}
