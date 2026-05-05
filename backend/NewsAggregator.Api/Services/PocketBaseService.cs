using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NewsAggregator.Api.Models;

namespace NewsAggregator.Api.Services;

public class PocketBaseService : IArticleService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _collectionName;
    private readonly ILogger<PocketBaseService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string? _apiKeyHeader;
    private readonly string? _apiKeyValue;

    public PocketBaseService(string collectionName, IHttpClientFactory httpClientFactory, ILogger<PocketBaseService> logger, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _collectionName = collectionName;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Optional API key header to use for server-to-server calls to PocketBase
        // Configure via configuration keys: PocketBase:ApiKeyHeader (default: X-API-Key) and PocketBase:ApiKey
        _apiKeyHeader = configuration["PocketBase:ApiKeyHeader"] ?? configuration["PocketBase:ApiKeyHeaderName"] ?? "X-API-Key";
        _apiKeyValue = configuration["PocketBase:ApiKey"];
        if (!string.IsNullOrEmpty(_apiKeyValue))
        {
            _logger.LogInformation("PocketBase API key header configured: {Header}", _apiKeyHeader);
        }
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("pocketbase");
        if (!string.IsNullOrEmpty(_apiKeyValue) && !string.IsNullOrEmpty(_apiKeyHeader))
        {
            if (client.DefaultRequestHeaders.Contains(_apiKeyHeader))
                client.DefaultRequestHeaders.Remove(_apiKeyHeader);
            client.DefaultRequestHeaders.Add(_apiKeyHeader, _apiKeyValue);
        }
        return client;
    }

    private string CollectionEndpoint => $"api/collections/{_collectionName}/records";

    public Task InitializeAsync()
    {
        // PocketBase doesn't require runtime initialization.
        // The 'articles' collection must be created via the PocketBase admin UI
        // at http://localhost:8090/_/ before starting the application.
        _logger.LogInformation("PocketBase service initialized. Collection: {Collection}", _collectionName);
        return Task.CompletedTask;
    }

    private async Task<IEnumerable<Article>> FetchAllPagesAsync(string? filter = null, string sort = "-publishedDate")
    {
        var allArticles = new List<Article>();
        int page = 1;
        int totalPages = 1;

        using var client = CreateClient();
        do
        {
            var url = BuildListUrl(page, 500, filter, sort);
            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PocketBase list request failed. Status: {Status}", response.StatusCode);
                break;
            }

            var listResponse = await response.Content.ReadFromJsonAsync<PocketBaseListResponse>(_jsonOptions);
            if (listResponse == null) break;

            totalPages = listResponse.TotalPages;
            allArticles.AddRange(listResponse.Items.Select(MapToArticle));
            page++;
        }
        while (page <= totalPages);

        return allArticles;
    }

    private string BuildListUrl(int page, int perPage, string? filter, string sort)
    {
        var sb = new StringBuilder($"{CollectionEndpoint}?page={page}&perPage={perPage}");
        if (!string.IsNullOrEmpty(sort))
            sb.Append($"&sort={Uri.EscapeDataString(sort)}");
        if (!string.IsNullOrEmpty(filter))
            sb.Append($"&filter={Uri.EscapeDataString(filter)}");
        return sb.ToString();
    }

    public async Task<IEnumerable<Article>> GetArticlesAsync()
    {
        return await FetchAllPagesAsync(sort: "-publishedDate");
    }

    public async Task<Article?> GetArticleAsync(string id)
    {
        using var client = CreateClient();
        var response = await client.GetAsync($"{CollectionEndpoint}/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        var record = await response.Content.ReadFromJsonAsync<PocketBaseArticleRecord>(_jsonOptions);
        return record == null ? null : MapToArticle(record);
    }

    public async Task<Article> AddArticleAsync(Article article)
    {
        article.ScrapedDate = DateTimeOffset.UtcNow;
        var payload = MapToPayload(article);

        using var client = CreateClient();
        var response = await client.PostAsJsonAsync(CollectionEndpoint, payload, _jsonOptions);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<PocketBaseArticleRecord>(_jsonOptions);
        if (created == null) throw new InvalidOperationException("Failed to deserialize the created article response from PocketBase.");

        article.Id = created.Id;
        return article;
    }

    public async Task UpdateArticleAsync(string id, Article article)
    {
        var payload = MapToPayload(article);
        using var client = CreateClient();
        var response = await client.PatchAsJsonAsync($"{CollectionEndpoint}/{id}", payload, _jsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteArticleAsync(string id)
    {
        using var client = CreateClient();
        var response = await client.DeleteAsync($"{CollectionEndpoint}/{id}");
        if (response.StatusCode != HttpStatusCode.NotFound)
            response.EnsureSuccessStatusCode();
    }

    public async Task<IEnumerable<Article>> GetArticlesBySourceAsync(string source)
    {
        var filter = $"source = '{EscapeFilter(source)}'";
        return await FetchAllPagesAsync(filter, "-publishedDate");
    }

    public async Task<IEnumerable<Article>> GetArticlesByCategoryAsync(string category)
    {
        var filter = $"category = '{EscapeFilter(category)}'";
        return await FetchAllPagesAsync(filter, "-publishedDate");
    }

    public async Task<Article?> GetArticleByUrlAsync(string url)
    {
        var filter = $"url = '{EscapeFilter(url)}'";
        var listUrl = BuildListUrl(1, 1, filter, "");

        using var client = CreateClient();
        var response = await client.GetAsync(listUrl);
        if (!response.IsSuccessStatusCode) return null;

        var listResponse = await response.Content.ReadFromJsonAsync<PocketBaseListResponse>(_jsonOptions);
        var first = listResponse?.Items.FirstOrDefault();
        return first == null ? null : MapToArticle(first);
    }

    public async Task<(int added, int skipped, int updated, List<string> errors)> AddArticlesBatchAsync(IEnumerable<Article> articles)
    {
        int added = 0;
        int skipped = 0;
        int updated = 0;
        var errors = new List<string>();

        foreach (var article in articles)
        {
            try
            {
                var existing = await GetArticleByUrlAsync(article.Url);
                if (existing != null)
                {
                    // Decide whether to update the existing record
                    bool shouldUpdate = false;

                    // Prefer updating when incoming sentiment confidence is higher
                    if (article.SentimentConfidence > existing.SentimentConfidence + 0.01)
                        shouldUpdate = true;

                    // Or when incoming has content and existing doesn't
                    if (!string.IsNullOrEmpty(article.Content) && string.IsNullOrEmpty(existing.Content))
                        shouldUpdate = true;

                    // Or when incoming publishedDate is newer
                    if (article.PublishedDate > existing.PublishedDate)
                        shouldUpdate = true;

                    if (shouldUpdate)
                    {
                        article.Id = existing.Id;
                        await UpdateArticleAsync(existing.Id, article);
                        updated++;
                        continue;
                    }

                    skipped++;
                    continue;
                }

                await AddArticleAsync(article);
                added++;
            }
            catch (Exception ex)
            {
                errors.Add($"Error adding article '{article.Title}': {ex.Message}");
            }
        }

        return (added, skipped, updated, errors);
    }

    public async Task<Dictionary<string, int>> GetArticleCountsBySourceAsync()
    {
        var counts = new Dictionary<string, int>();
        int page = 1;
        int totalPages = 1;

        using var client = CreateClient();
        do
        {
            var url = $"{CollectionEndpoint}?page={page}&perPage=500&fields=source";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) break;

            var listResponse = await response.Content.ReadFromJsonAsync<PocketBaseListResponse>(_jsonOptions);
            if (listResponse == null) break;

            totalPages = listResponse.TotalPages;
            foreach (var item in listResponse.Items)
            {
                var source = item.Source ?? "";
                counts[source] = counts.TryGetValue(source, out var c) ? c + 1 : 1;
            }
            page++;
        }
        while (page <= totalPages);

        return counts;
    }

    public async Task<Dictionary<string, int>> GetArticleCountsBySentimentAsync()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["positive"] = 0,
            ["neutral"] = 0,
            ["negative"] = 0
        };

        int page = 1;
        int totalPages = 1;

        using var client = CreateClient();
        do
        {
            var url = $"{CollectionEndpoint}?page={page}&perPage=500&fields=sentimentLabel";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) break;

            var listResponse = await response.Content.ReadFromJsonAsync<PocketBaseListResponse>(_jsonOptions);
            if (listResponse == null) break;

            totalPages = listResponse.TotalPages;
            foreach (var item in listResponse.Items)
            {
                var label = (item.SentimentLabel ?? "neutral").Trim().ToLowerInvariant();
                if (!counts.ContainsKey(label)) label = "neutral";
                counts[label]++;
            }
            page++;
        }
        while (page <= totalPages);

        return counts;
    }

    public async Task<IEnumerable<Article>> GetArticlesSinceAsync(DateTimeOffset since)
    {
        // PocketBase date field stores and filters using "YYYY-MM-DD HH:MM:SS.mmmZ"
        var sinceStr = since.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.fffZ");
        var filter = $"publishedDate >= '{sinceStr}'";
        return await FetchAllPagesAsync(filter, "-publishedDate");
    }

    public async Task<long> GetTotalArticleCountAsync()
    {
        using var client = _httpClientFactory.CreateClient("pocketbase");
        var response = await client.GetAsync($"{CollectionEndpoint}?page=1&perPage=1");
        if (!response.IsSuccessStatusCode) return 0;

        var listResponse = await response.Content.ReadFromJsonAsync<PocketBaseListResponse>(_jsonOptions);
        return listResponse?.TotalItems ?? 0;
    }

    private static string EscapeFilter(string value) => value.Replace("'", "\\'");

    private static Article MapToArticle(PocketBaseArticleRecord record) => new()
    {
        Id = record.Id,
        Title = record.Title,
        Description = record.Description,
        Url = record.Url,
        Source = record.Source,
        Category = record.Category,
        PublishedDate = ParseDate(record.PublishedDate),
        ScrapedDate = ParseDate(record.ScrapedDate),
        Content = record.Content,
        SentimentLabel = record.SentimentLabel,
        SentimentScore = record.SentimentScore,
        SentimentConfidence = record.SentimentConfidence,
        PositiveWords = ParseJsonArray(record.PositiveWords),
        NegativeWords = ParseJsonArray(record.NegativeWords)
    };

    private static object MapToPayload(Article article) => new
    {
        title = article.Title,
        description = article.Description,
        url = article.Url,
        source = article.Source,
        category = article.Category,
        publishedDate = article.PublishedDate.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.fffZ"),
        scrapedDate = article.ScrapedDate.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.fffZ"),
        content = article.Content,
        sentimentLabel = article.SentimentLabel,
        sentimentScore = article.SentimentScore,
        sentimentConfidence = article.SentimentConfidence,
        positiveWords = article.PositiveWords,
        negativeWords = article.NegativeWords
    };

    private static DateTimeOffset ParseDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return DateTimeOffset.UtcNow;
        return DateTimeOffset.TryParse(dateStr, out var result) ? result : DateTimeOffset.UtcNow;
    }

    private static List<string> ParseJsonArray(JsonElement? element)
    {
        if (element == null || element.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return new List<string>();

        if (element.Value.ValueKind == JsonValueKind.Array)
        {
            return element.Value.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToList();
        }

        return new List<string>();
    }
}

internal class PocketBaseListResponse
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("perPage")]
    public int PerPage { get; set; }

    [JsonPropertyName("totalItems")]
    public int TotalItems { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("items")]
    public List<PocketBaseArticleRecord> Items { get; set; } = new();
}

internal class PocketBaseArticleRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

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
    public string? PublishedDate { get; set; }

    [JsonPropertyName("scrapedDate")]
    public string? ScrapedDate { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("sentimentLabel")]
    public string SentimentLabel { get; set; } = "neutral";

    [JsonPropertyName("sentimentScore")]
    public double SentimentScore { get; set; }

    [JsonPropertyName("sentimentConfidence")]
    public double SentimentConfidence { get; set; }

    [JsonPropertyName("positiveWords")]
    public JsonElement? PositiveWords { get; set; }

    [JsonPropertyName("negativeWords")]
    public JsonElement? NegativeWords { get; set; }
}
