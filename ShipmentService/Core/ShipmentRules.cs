namespace LastMile.Shipments.Core;

/// <summary>Field rules for shipments. Address limit matches ProfileService (clients.address VARCHAR(1000)).</summary>
public static class ShipmentRules
{
    public const int MaxAddressLength = 1000;
    public const int MaxNotesLength = 1000;
    public const decimal MaxWeightKg = 100_000m;

    public static string Address(string? value, string field)
    {
        var v = string.IsNullOrWhiteSpace(value) ? throw new DomainValidationException($"{field} is required.") : value.Trim();
        return v.Length <= MaxAddressLength ? v : throw new DomainValidationException($"{field} must be at most {MaxAddressLength} characters.");
    }

    public static decimal Weight(decimal? value)
    {
        if (value is null or <= 0) throw new DomainValidationException("Weight must be greater than 0 kg.");
        if (value > MaxWeightKg) throw new DomainValidationException($"Weight must be at most {MaxWeightKg} kg.");
        return decimal.Round(value.Value, 2);
    }

    public static string? Notes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        return v.Length <= MaxNotesLength ? v : throw new DomainValidationException($"Notes must be at most {MaxNotesLength} characters.");
    }
}
