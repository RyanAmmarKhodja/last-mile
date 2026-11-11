using LastMile.Contracts;
using LastMile.Fleet.Data;
using LastMile.Messaging;
using LastMile.Messaging.Inbox;
using Microsoft.Extensions.Options;

namespace LastMile.Fleet.Services;

/// <summary>
/// Kafka consumer (group <c>fleet-service</c>) of <c>auth.user.v1</c> (driver profiles, D2) and
/// <c>shipment.lifecycle.v1</c> (availability, D3). The base class provides inbox de-duplication, retries and the DLQ;
/// this class only routes by topic.
/// </summary>
public sealed class FleetEventsConsumer(
    IServiceScopeFactory scopes,
    KafkaProducer kafka,
    MessagingService service,
    IOptions<MessagingOptions> options,
    ILogger<FleetEventsConsumer> logger) : EventConsumer<FleetDbContext>(scopes, kafka, service, options, logger)
{
    protected override IReadOnlyCollection<string> Topics => [LastMile.Contracts.Topics.AuthUserV1, LastMile.Contracts.Topics.ShipmentLifecycleV1];

    protected override Task HandleAsync(ConsumedEvent evt, FleetDbContext db, IServiceProvider services, CancellationToken ct) =>
        evt.Topic switch
        {
            LastMile.Contracts.Topics.AuthUserV1 => services.GetRequiredService<UserEventHandler>().HandleAsync(evt, ct),
            LastMile.Contracts.Topics.ShipmentLifecycleV1 => services.GetRequiredService<ShipmentEventHandler>().HandleAsync(evt, ct),
            _ => Task.CompletedTask
        };
}
