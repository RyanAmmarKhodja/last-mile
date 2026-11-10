using System.Reflection;

namespace LastMile.Contracts;

/// <summary>Binds a payload record to its topic, event type name and schema version.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IntegrationEventAttribute(string topic, string type, int schemaVersion = 1) : Attribute
{
    public string Topic { get; } = topic;
    public string Type { get; } = type;
    public int SchemaVersion { get; } = schemaVersion;
}

/// <summary>Lookup of every <see cref="IntegrationEventAttribute"/> payload in this assembly.</summary>
public static class EventCatalog
{
    public sealed record Entry(string Topic, string Type, int SchemaVersion, Type PayloadType);

    private static readonly Lazy<IReadOnlyList<Entry>> Entries = new(() =>
        typeof(EventCatalog).Assembly.GetTypes()
            .Select(t => (Type: t, Attr: t.GetCustomAttribute<IntegrationEventAttribute>()))
            .Where(x => x.Attr is not null)
            .Select(x => new Entry(x.Attr!.Topic, x.Attr.Type, x.Attr.SchemaVersion, x.Type))
            .ToList());

    public static IReadOnlyList<Entry> All => Entries.Value;

    public static Entry For(Type payloadType) =>
        All.FirstOrDefault(e => e.PayloadType == payloadType)
        ?? throw new InvalidOperationException(
            $"{payloadType.Name} is not an integration event (missing [IntegrationEvent]).");

    public static Entry? Find(string topic, string type) =>
        All.FirstOrDefault(e => e.Topic == topic && e.Type == type);
}
