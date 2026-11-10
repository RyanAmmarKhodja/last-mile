using LastMile.Messaging.Inbox;
using LastMile.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LastMile.Messaging;

/// <summary>EF Core mappings for the outbox / inbox tables (PostgreSQL). Call from <c>OnModelCreating</c>.</summary>
public static class ModelBuilderExtensions
{
    public const string OutboxTable = "outbox";
    public const string InboxTable = "inbox";

    /// <summary>For services that publish events.</summary>
    public static ModelBuilder AddOutbox(this ModelBuilder b)
    {
        b.Entity<OutboxMessage>(e =>
        {
            e.ToTable(OutboxTable);
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.EventId).HasColumnName("event_id");
            e.Property(x => x.Topic).HasColumnName("topic").HasMaxLength(200).IsRequired();
            e.Property(x => x.MessageKey).HasColumnName("message_key").HasMaxLength(100).IsRequired();
            e.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
            e.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
            e.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100);
            e.Property(x => x.OccurredAt).HasColumnName("occurred_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.PublishedAt).HasColumnName("published_at");
            e.Property(x => x.Attempts).HasColumnName("attempts").HasDefaultValue(0);
            e.Property(x => x.LastError).HasColumnName("last_error");
            e.HasIndex(x => x.EventId).IsUnique().HasDatabaseName("idx_outbox_event_id");
            e.HasIndex(x => x.Id).HasFilter("published_at IS NULL").HasDatabaseName("idx_outbox_unpublished");
            e.HasIndex(x => x.PublishedAt).HasFilter("published_at IS NOT NULL").HasDatabaseName("idx_outbox_published_at");
        });
        return b;
    }

    /// <summary>For services that consume events.</summary>
    public static ModelBuilder AddInbox(this ModelBuilder b)
    {
        b.Entity<InboxMessage>(e =>
        {
            e.ToTable(InboxTable);
            e.HasKey(x => x.EventId);
            e.Property(x => x.EventId).HasColumnName("event_id");
            e.Property(x => x.Topic).HasColumnName("topic").HasMaxLength(200).IsRequired();
            e.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
            e.Property(x => x.ProcessedAt).HasColumnName("processed_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => x.ProcessedAt).HasDatabaseName("idx_inbox_processed_at");
        });
        return b;
    }
}
