using System.Text.Json.Serialization;

namespace NewsAggregator.Api.Models;

public class EmailSignup
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("sourceContext")]
    public string SourceContext { get; set; } = "unknown";

    [JsonPropertyName("utmSource")]
    public string? UtmSource { get; set; }

    [JsonPropertyName("utmMedium")]
    public string? UtmMedium { get; set; }

    [JsonPropertyName("utmCampaign")]
    public string? UtmCampaign { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
