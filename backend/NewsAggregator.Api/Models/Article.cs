using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace NewsAggregator.Api.Models;

public class Article
{
    [JsonPropertyName("id")]
    [JsonProperty(PropertyName = "id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("title")]
    [JsonProperty(PropertyName = "title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonProperty(PropertyName = "description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    [JsonProperty(PropertyName = "url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    [JsonProperty(PropertyName = "source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    [JsonProperty(PropertyName = "category")]
    public string Category { get; set; } = "General";

    [JsonPropertyName("publishedDate")]
    [JsonProperty(PropertyName = "publishedDate")]
    public DateTimeOffset PublishedDate { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("scrapedDate")]
    [JsonProperty(PropertyName = "scrapedDate")]
    public DateTimeOffset ScrapedDate { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("content")]
    [JsonProperty(PropertyName = "content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("sentimentLabel")]
    [JsonProperty(PropertyName = "sentimentLabel")]
    public string SentimentLabel { get; set; } = "neutral";

    [JsonPropertyName("sentimentScore")]
    [JsonProperty(PropertyName = "sentimentScore")]
    public double SentimentScore { get; set; } = 0.0;

    [JsonPropertyName("sentimentConfidence")]
    [JsonProperty(PropertyName = "sentimentConfidence")]
    public double SentimentConfidence { get; set; } = 0.0;

    public List<string> PositiveWords { get; set; } = new();
    public List<string> NegativeWords { get; set; } = new();
}
