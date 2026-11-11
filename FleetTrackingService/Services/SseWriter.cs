using System.Text.Json;

namespace LastMile.Tracking.Services;

/// <summary>Minimal Server-Sent Events writer (<c>text/event-stream</c>), unbuffered through NGINX.</summary>
public static class SseWriter
{
    public static async Task StartAsync(HttpResponse response, CancellationToken ct)
    {
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no"; // NGINX: pass events through immediately
        await response.Body.FlushAsync(ct);
    }

    public static async Task WriteAsync(HttpResponse response, string eventName, object data, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(data, LivePositionStore.Json);
        await response.WriteAsync($"event: {eventName}\ndata: {json}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }
}
