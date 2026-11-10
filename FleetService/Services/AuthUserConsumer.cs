using LastMile.Messaging;
using LastMile.Messaging.Inbox;
using LastMile.Fleet.Data;
using Microsoft.Extensions.Options;

namespace LastMile.Fleet.Services;

/// <summary>
/// Kafka consumer of <c>auth.user.v1</c> (consumer group <c>fleet-service</c>). The base class provides inbox
/// de-duplication, retries and the DLQ; this class only routes events to <see cref="UserEventHandler"/>.
/// </summary>
public sealed class AuthUserConsumer(
    IServiceScopeFactory scopes,
    KafkaProducer kafka,
    MessagingService service,
    IOptions<MessagingOptions> options,
    ILogger<AuthUserConsumer> logger) : EventConsumer<FleetDbContext>(scopes, kafka, service, options, logger)
{
    protected override IReadOnlyCollection<string> Topics => [LastMile.Contracts.Topics.AuthUserV1];

    protected override Task HandleAsync(ConsumedEvent evt, FleetDbContext db, IServiceProvider services,
        CancellationToken ct) =>
        services.GetRequiredService<UserEventHandler>().HandleAsync(evt, ct);
}
