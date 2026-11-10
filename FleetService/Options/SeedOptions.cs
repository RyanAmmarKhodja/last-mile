namespace LastMile.Fleet.Options;

/// <summary>Development seed (section <c>Seed</c>). Drivers are never seeded here: they come from <c>UserRegistered</c>.</summary>
public sealed class SeedOptions
{
    public const string Section = "Seed";

    /// <summary>Demo vehicles created at startup if their plate does not exist yet (each publishes <c>VehicleCreated</c>).</summary>
    public List<SeedVehicle> Vehicles { get; set; } = [];

    public sealed class SeedVehicle
    {
        public string LicensePlate { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public decimal CapacityKg { get; set; }
    }
}
