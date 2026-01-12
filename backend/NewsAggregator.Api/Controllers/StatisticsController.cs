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

            var statistics = new
            {
                totalArticles = totalCount,
                articlesBySource = countsBySource,
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
}
