using LastMile.Tracking.Models;
using LastMile.Tracking.Services;

namespace LastMile.Tracking.DTOs;

/// <summary>
/// A GPS ping (T1). DRIVER tokens must not set <see cref="DriverId"/> (or set their own); M2M <c>tracking:ingest</c>
/// tokens must. Vehicle, shipment and tracking number are derived on the server.
/// </summary>
public sealed record PingDto(
    double? Latitude,
    double? Longitude,
    DateTimeOffset? Timestamp,
    double? SpeedKmh = null,
    double? HeadingDeg = null,
    double? AccuracyM = null,
    Guid? DriverId = null);

public sealed record PingBatchDto(IReadOnlyList<PingDto>? Pings);

public sealed record RejectedPingDto(int Index, string Reason);

public sealed record PingBatchResultDto(int Accepted, int Ignored, IReadOnlyList<RejectedPingDto> Rejected);

/// <summary>Latest position for ADMIN, M2M and the driver themselves.</summary>
public sealed record LivePositionDto(
    Guid DriverId,
    Guid? VehicleId,
    Guid? ShipmentId,
    string? TrackingNumber,
    double Latitude,
    double Longitude,
    DateTimeOffset RecordedAt,
    DateTimeOffset ReceivedAt,
    double? SpeedKmh,
    double? HeadingDeg,
    double? AccuracyM)
{
    public static LivePositionDto From(LivePosition p) => new(p.DriverId, p.VehicleId, p.ShipmentId, p.TrackingNumber,
        p.Latitude, p.Longitude, p.RecordedAt, p.ReceivedAt, p.SpeedKmh, p.HeadingDeg, p.AccuracyM);
}

public sealed record NearbyPositionDto(double DistanceKm, LivePositionDto Position);

/// <summary>Public position by tracking number (T3): no driver, vehicle or history.</summary>
public sealed record PublicPointDto(double Latitude, double Longitude, DateTimeOffset RecordedAt, double? SpeedKmh,
    double? HeadingDeg)
{
    public static PublicPointDto From(LivePosition p) => new(p.Latitude, p.Longitude, p.RecordedAt, p.SpeedKmh, p.HeadingDeg);
}

public sealed record PublicPositionDto(string TrackingNumber, string Status, bool Live, PublicPointDto? Position);

public sealed record HistoryPointDto(
    double Latitude,
    double Longitude,
    DateTimeOffset RecordedAt,
    float? SpeedKmh,
    float? HeadingDeg,
    Guid? VehicleId,
    Guid? ShipmentId)
{
    public static HistoryPointDto From(LocationHistoryEntity e) =>
        new(e.Latitude, e.Longitude, e.RecordedAt, e.SpeedKmh, e.HeadingDeg, e.VehicleId, e.ShipmentId);
}
