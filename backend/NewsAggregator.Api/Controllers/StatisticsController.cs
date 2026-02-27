using Microsoft.AspNetCore.Mvc;
using NewsAggregator.Api.Services;

namespace NewsAggregator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatisticsController : ControllerBase
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly ILogger<StatisticsController> _logger;

    public StatisticsController(ICosmosDbService cosmosDbService, ILogger<StatisticsController> logger)
    {
        _cosmosDbService = cosmosDbService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> GetStatistics()
    {
        try
        {
            var totalCount = await _cosmosDbService.GetTotalArticleCountAsync();
            var countsBySource = await _cosmosDbService.GetArticleCountsBySourceAsync();
            var countsBySentiment = await _cosmosDbService.GetArticleCountsBySentimentAsync();
            var sentimentTrends = await BuildSentimentTrendsAsync();

            var statistics = new
            {
                totalArticles = totalCount,
                articlesBySource = countsBySource,
                articlesBySentiment = countsBySentiment,
                sentimentTrends,
                sources = countsBySource.Keys.ToList(),
                timestamp = DateTime.UtcNow
            };

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving statistics");
            return StatusCode(500, "An error occurred while retrieving statistics");
        }
    }

    [HttpGet("sources")]
    public async Task<ActionResult> GetSources()
    {
        try
        {
            var countsBySource = await _cosmosDbService.GetArticleCountsBySourceAsync();
            
            var sources = countsBySource.Select(kvp => new
            {
                name = kvp.Key,
                articleCount = kvp.Value
            }).ToList();

            return Ok(sources);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sources");
            return StatusCode(500, "An error occurred while retrieving sources");
        }
    }

    private async Task<object> BuildSentimentTrendsAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var start14Days = now.AddDays(-13).Date;
        var start7Days = now.AddDays(-6).Date;
        var recentArticles = await _cosmosDbService.GetArticlesSinceAsync(start14Days);

        var last24Start = now.AddHours(-23);
        var hourlyBuckets = BuildBuckets(24);
        var sevenDayBuckets = BuildBuckets(7);
        var dailyBuckets = BuildBuckets(14);

        foreach (var article in recentArticles)
        {
            var published = article.PublishedDate.ToUniversalTime();
            var label = NormalizeSentiment(article.SentimentLabel);

            if (published >= last24Start)
            {
                var hourIndex = (int)Math.Floor((published - last24Start).TotalHours);
                if (hourIndex >= 0 && hourIndex < hourlyBuckets.Count)
                {
                    hourlyBuckets[hourIndex][label]++;
                }
            }

            var dayIndex = (int)Math.Floor((published.Date - start14Days).TotalDays);
            if (dayIndex >= 0 && dayIndex < dailyBuckets.Count)
            {
                dailyBuckets[dayIndex][label]++;
            }

            var day7Index = (int)Math.Floor((published.Date - start7Days).TotalDays);
            if (day7Index >= 0 && day7Index < sevenDayBuckets.Count)
            {
                sevenDayBuckets[day7Index][label]++;
            }
        }

        return new
        {
            last24Hours = hourlyBuckets.Select((counts, index) =>
            {
                var start = last24Start.AddHours(index);
                return new
                {
                    periodStart = start.ToString("o"),
                    label = start.ToString("HH:mm"),
                    positive = counts["positive"],
                    neutral = counts["neutral"],
                    negative = counts["negative"],
                    total = counts["positive"] + counts["neutral"] + counts["negative"]
                };
            }).ToList(),
            last7Days = sevenDayBuckets.Select((counts, index) =>
            {
                var start = start7Days.AddDays(index);
                return new
                {
                    periodStart = start.ToString("o"),
                    label = start.ToString("MMM dd"),
                    positive = counts["positive"],
                    neutral = counts["neutral"],
                    negative = counts["negative"],
                    total = counts["positive"] + counts["neutral"] + counts["negative"]
                };
            }).ToList(),
            last14Days = dailyBuckets.Select((counts, index) =>
            {
                var start = start14Days.AddDays(index);
                return new
                {
                    periodStart = start.ToString("o"),
                    label = start.ToString("MMM dd"),
                    positive = counts["positive"],
                    neutral = counts["neutral"],
                    negative = counts["negative"],
                    total = counts["positive"] + counts["neutral"] + counts["negative"]
                };
            }).ToList()
        };
    }

    private static List<Dictionary<string, int>> BuildBuckets(int count)
    {
        var buckets = new List<Dictionary<string, int>>(count);
        for (var index = 0; index < count; index++)
        {
            buckets.Add(new Dictionary<string, int>
            {
                ["positive"] = 0,
                ["neutral"] = 0,
                ["negative"] = 0
            });
        }

        return buckets;
    }

    private static string NormalizeSentiment(string? sentimentLabel)
    {
        var normalized = (sentimentLabel ?? "neutral").Trim().ToLowerInvariant();
        return normalized is "positive" or "neutral" or "negative" ? normalized : "neutral";
    }
}
