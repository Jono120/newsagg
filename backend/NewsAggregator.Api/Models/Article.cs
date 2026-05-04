using System.Text.Json.Serialization;

namespace NewsAggregator.Api.Models;

public class Article
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = "General";

    [JsonPropertyName("publishedDate")]
    public DateTimeOffset PublishedDate { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("scrapedDate")]
    public DateTimeOffset ScrapedDate { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("sentimentLabel")]
    public string SentimentLabel { get; set; } = "neutral";

    [JsonPropertyName("sentimentScore")]
    public double SentimentScore { get; set; } = 0.0;

    [JsonPropertyName("sentimentConfidence")]
    public double SentimentConfidence { get; set; } = 0.0;

    [JsonPropertyName("positiveWords")]
    public List<string> PositiveWords { get; set; } = new();

    [JsonPropertyName("negativeWords")]
    public List<string> NegativeWords { get; set; } = new();
}
