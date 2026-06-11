using System.Text.Json.Serialization;

namespace NewsAggregator.Api.Models;

public class AnalyticsEvent
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("eventName")]
    public string EventName { get; set; } = string.Empty;

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "direct";

    [JsonPropertyName("sourceContext")]
    public string SourceContext { get; set; } = "unknown";

    [JsonPropertyName("utmSource")]
    public string? UtmSource { get; set; }

    [JsonPropertyName("utmMedium")]
    public string? UtmMedium { get; set; }

    [JsonPropertyName("utmCampaign")]
    public string? UtmCampaign { get; set; }

    [JsonPropertyName("occurredAt")]
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
