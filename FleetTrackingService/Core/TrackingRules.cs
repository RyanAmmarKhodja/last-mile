using LastMile.Tracking.Enums;

namespace LastMile.Tracking.Core;

/// <summary>A validated GPS ping (T1: no client-supplied driver, vehicle or shipment context for DRIVER tokens).</summary>
public sealed record Ping(
    double Latitude,
    double Longitude,
    DateTimeOffset RecordedAt,
    double? SpeedKmh,
    double? HeadingDeg,
    double? AccuracyM);

/// <summary>
/// What the read model knows about a driver at ping time. <see cref="OnShift"/> implements decision T4:
/// ACTIVE and (available, or carrying an active shipment, because D3 sets is_available=false during a delivery).
/// </summary>
public sealed record DriverContext(
    Guid DriverId,
    Guid UserId,
    string Status,
    bool IsAvailable,
    Guid? VehicleId,
    Guid? ShipmentId,
    Guid? ShipmentVehicleId,
    string? TrackingNumber)
{
    public bool IsActive => Status == DriverStatuses.Active;
    public bool OnShift => IsActive && (IsAvailable || ShipmentId is not null);

    /// <summary>The vehicle snapshotted at dispatch wins over the fleet assignment while on a delivery.</summary>
    public Guid? EffectiveVehicleId => ShipmentVehicleId ?? VehicleId;
}

public static class TrackingRules
{
    public const double MaxLatitude = 85.05112878;

    /// <summary>Validates ranges and the timestamp window. Throws <see cref="DomainValidationException"/>.</summary>
    public static Ping Validate(double? latitude, double? longitude, DateTimeOffset? timestamp, double? speedKmh,
        double? headingDeg, double? accuracyM, DateTimeOffset now, TimeSpan maxFutureSkew, TimeSpan maxAge)
    {
        // Redis GEO (Web Mercator) accepts latitudes up to ±85.05112878°; that covers every delivery area.
        if (latitude is not { } lat || double.IsNaN(lat) || lat is < -MaxLatitude or > MaxLatitude)
            throw new DomainValidationException("latitude must be between -85.05 and 85.05.");
        if (longitude is not { } lon || double.IsNaN(lon) || lon is < -180 or > 180)
            throw new DomainValidationException("longitude must be between -180 and 180.");
        if (timestamp is not { } ts)
            throw new DomainValidationException("timestamp is required (ISO-8601).");
        if (ts > now + maxFutureSkew)
            throw new DomainValidationException("timestamp is in the future.");
        if (ts < now - maxAge)
            throw new DomainValidationException($"timestamp is older than {maxAge.TotalMinutes:0} minutes (stale ping).");
        if (speedKmh is { } s && (double.IsNaN(s) || s is < 0 or > 300))
            throw new DomainValidationException("speedKmh must be between 0 and 300.");
        if (headingDeg is { } h && (double.IsNaN(h) || h is < 0 or >= 360))
            throw new DomainValidationException("headingDeg must be in [0, 360).");
        if (accuracyM is { } a && (double.IsNaN(a) || a < 0))
            throw new DomainValidationException("accuracyM must be >= 0.");
        return new Ping(lat, lon, ts.ToUniversalTime(), speedKmh, headingDeg, accuracyM);
    }

    /// <summary>Throws when a ping from this driver must not be stored (404 unknown, 409 inactive / off shift).</summary>
    public static DriverContext RequireOnShift(DriverContext? ctx)
    {
        if (ctx is null) throw new NotFoundException("No driver profile is known for this driver.");
        if (!ctx.IsActive) throw new ConflictException("Driver is not active.");
        if (!ctx.OnShift) throw new ConflictException("Driver is not on shift.");
        return ctx;
    }

    /// <summary>Tracking numbers are stored upper-case (ShipmentService format <c>LM-XXXXXXXXXXXX</c>).</summary>
    public static string NormalizeTrackingNumber(string trackingNumber) => trackingNumber.Trim().ToUpperInvariant();
}
