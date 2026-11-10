namespace LastMile.Contracts;

/// <summary>
/// Kafka topic catalogue (documents/kafka-architecture.md §3). Naming: &lt;owner-service&gt;.&lt;aggregate&gt;.v&lt;major&gt;.
/// Only the owning service publishes to a topic; a breaking payload change means a new .vN topic.
/// </summary>
public static class Topics
{
    public const string AuthUserV1 = "auth.user.v1";
    public const string ProfileClientV1 = "profile.client.v1";
    public const string FleetDriverV1 = "fleet.driver.v1";
    public const string FleetVehicleV1 = "fleet.vehicle.v1";
    public const string ShipmentLifecycleV1 = "shipment.lifecycle.v1";

    public const string DeadLetterSuffix = ".dlq";

    /// <summary>Dead-letter topic for <paramref name="topic"/>.</summary>
    public static string DeadLetter(string topic) => topic + DeadLetterSuffix;
}
