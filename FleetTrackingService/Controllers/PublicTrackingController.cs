using LastMile.Tracking.DTOs;
using LastMile.Tracking.Enums;
using LastMile.Tracking.Models;
using LastMile.Tracking.Options;
using LastMile.Tracking.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LastMile.Tracking.Controllers;

/// <summary>
/// Customer live map by tracking number (UC-2, decision T3). No login, rate-limited at NGINX. The position is shown
/// only while the shipment is DISPATCHED or IN_TRANSIT, and never with driver identity, vehicle or history.
/// </summary>
[ApiController]
[Route("api/v1/tracking/track/{trackingNumber}")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class PublicTrackingController(
    TrackingQueries queries,
    LivePositionStore live,
    PositionHub hub,
    IOptions<TrackingOptions> options) : ControllerBase
{
    /// <summary>Current status and live position (null when not in delivery or not live).</summary>
    [HttpGet]
    [ProducesResponseType<PublicPositionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PublicPositionDto>> Get(string trackingNumber, CancellationToken ct)
    {
        var shipment = await queries.ResolveTrackingAsync(trackingNumber, ct);
        if (shipment is null) return NotFoundProblem();

        var position = await CurrentPositionAsync(shipment);
        return new PublicPositionDto(shipment.TrackingNumber, shipment.Status, position is not null, position);
    }

    /// <summary>
    /// SSE stream: <c>status</c>, then <c>position</c> on every ping while in delivery, <c>heartbeat</c> every 15 s,
    /// and <c>ended</c> once the shipment is delivered, failed or cancelled.
    /// </summary>
    [HttpGet("stream")]
    [Produces("text/event-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task Stream(string trackingNumber, CancellationToken ct)
    {
        var shipment = await queries.ResolveTrackingAsync(trackingNumber, ct);
        if (shipment is null)
        {
            await NotFoundProblem().ExecuteResultAsync(ControllerContext);
            return;
        }

        var heartbeat = options.Value.HeartbeatInterval;
        DriverSink? sink = null;
        try
        {
            await SseWriter.StartAsync(Response, ct);
            await SseWriter.WriteAsync(Response, "status", new { status = shipment.Status }, ct);

            while (!ct.IsCancellationRequested)
            {
                if (ShipmentStatuses.IsFinal(shipment.Status))
                {
                    await SseWriter.WriteAsync(Response, "ended", new { status = shipment.Status }, ct);
                    return;
                }

                // Follow the driver only while the shipment is in delivery (T3).
                if (ShipmentStatuses.IsActive(shipment.Status) && shipment.DriverId is { } driverId && sink is null)
                {
                    sink = hub.SubscribeDriver(driverId);
                    if (await live.GetAsync(driverId) is { } current && current.ShipmentId == shipment.ShipmentId)
                        await SseWriter.WriteAsync(Response, "position", PublicPointDto.From(current), ct);
                }

                await PumpUntilAsync(sink, shipment.ShipmentId, heartbeat, ct);
                await SseWriter.WriteAsync(Response, "heartbeat", new { }, ct);

                var latest = await queries.ResolveTrackingAsync(shipment.TrackingNumber, ct) ?? shipment;
                if (latest.Status != shipment.Status)
                    await SseWriter.WriteAsync(Response, "status", new { status = latest.Status }, ct);
                shipment = latest;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // client went away
        }
        finally
        {
            sink?.Dispose();
        }
    }

    /// <summary>
    /// Forwards positions from <paramref name="sink"/> until <paramref name="duration"/> elapses. Only pings tagged with
    /// this shipment (tag derived on the server at ping time) are forwarded, so nothing leaks after delivery or before
    /// dispatch, even between two status re-checks.
    /// </summary>
    private async Task PumpUntilAsync(DriverSink? sink, Guid shipmentId, TimeSpan duration, CancellationToken ct)
    {
        if (sink is null)
        {
            await Task.Delay(duration, ct);
            return;
        }

        using var window = CancellationTokenSource.CreateLinkedTokenSource(ct);
        window.CancelAfter(duration);
        try
        {
            while (await sink.Reader.WaitToReadAsync(window.Token))
                while (sink.Reader.TryRead(out var msg))
                    if (msg is { Type: "position", Position: { } p } && p.ShipmentId == shipmentId)
                        await SseWriter.WriteAsync(Response, "position", PublicPointDto.From(p), ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // window elapsed: time for a heartbeat and a status re-check
        }
    }

    private async Task<PublicPointDto?> CurrentPositionAsync(TrackedShipmentEntity shipment) =>
        ShipmentStatuses.IsActive(shipment.Status) && shipment.DriverId is { } driverId &&
        await live.GetAsync(driverId) is { } p && p.ShipmentId == shipment.ShipmentId
            ? PublicPointDto.From(p)
            : null;

    private ObjectResult NotFoundProblem() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: "Unknown tracking number.");
}
