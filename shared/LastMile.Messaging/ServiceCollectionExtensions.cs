using Confluent.SchemaRegistry;
using LastMile.Messaging.Inbox;
using LastMile.Messaging.Outbox;
using LastMile.Messaging.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace LastMile.Messaging;

public static class ServiceCollectionExtensions
{
    /// <summary>Kafka producer, Schema Registry client and options (section <c>Kafka</c>).</summary>
    /// <param name="serviceName">e.g. <c>auth-service</c>: envelope source, Kafka client id, default consumer group.</param>
    public static IServiceCollection AddKafkaMessaging(this IServiceCollection services, IConfiguration configuration,
        string serviceName)
    {
        services.Configure<MessagingOptions>(configuration.GetSection(MessagingOptions.Section));
        services.AddSingleton(new MessagingService(serviceName));
        services.AddSingleton<KafkaProducer>();
        services.AddSingleton<ISchemaRegistryClient>(sp =>
            new CachedSchemaRegistryClient(new SchemaRegistryConfig
            {
                Url = sp.GetRequiredService<IOptions<MessagingOptions>>().Value.SchemaRegistryUrl
            }));
        services.AddSingleton<EventSerializer>();
        services.AddHttpContextAccessor();
        return services;
    }

    /// <summary>
    /// Publisher side: <see cref="IEventOutbox"/> (scoped) and the <see cref="OutboxRelay{TDbContext}"/>.
    /// <typeparamref name="TDbContext"/> must call <c>modelBuilder.AddOutbox()</c>.
    /// </summary>
    public static IServiceCollection AddOutbox<TDbContext>(this IServiceCollection services) where TDbContext : DbContext
    {
        services.AddScoped<IEventOutbox, EventOutbox<TDbContext>>();
        services.AddHostedService<OutboxRelay<TDbContext>>();
        return services;
    }

    /// <summary>
    /// Consumer side: runs <typeparamref name="TConsumer"/> and the inbox cleanup.
    /// <typeparamref name="TDbContext"/> must call <c>modelBuilder.AddInbox()</c>.
    /// </summary>
    public static IServiceCollection AddEventConsumer<TConsumer, TDbContext>(this IServiceCollection services)
        where TConsumer : EventConsumer<TDbContext>
        where TDbContext : DbContext
    {
        services.AddHostedService<TConsumer>();
        if (services.All(d => d.ImplementationType != typeof(InboxCleanupService<TDbContext>)))
            services.AddHostedService<InboxCleanupService<TDbContext>>();
        return services;
    }

    /// <summary>Adds the outbox lag check (<see cref="OutboxHealthCheck{TDbContext}"/>) under the name <c>outbox</c>.</summary>
    public static IHealthChecksBuilder AddOutboxCheck<TDbContext>(this IHealthChecksBuilder builder)
        where TDbContext : DbContext =>
        builder.AddCheck<OutboxHealthCheck<TDbContext>>("outbox");
}
