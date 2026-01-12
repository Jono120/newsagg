using Microsoft.AspNetCore.Mvc;
using NewsAggregator.Api.Services;

namespace NewsAggregator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(ICosmosDbService cosmosDbService, ILogger<HealthController> logger)
    {
        _cosmosDbService = cosmosDbService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> GetHealth()
    {
        try
        {
            // Test database connectivity
            var totalCount = await _cosmosDbService.GetTotalArticleCountAsync();
            
            var health = new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                database = new
                {
                    status = "connected",
                    articleCount = totalCount
                },
                version = "1.0.0"
            };

            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            
            var health = new
            {
                status = "unhealthy",
                timestamp = DateTime.UtcNow,
                database = new
                {
                    status = "error",
                    error = ex.Message
                },
                version = "1.0.0"
            };

            return StatusCode(503, health);
        }
    }

    [HttpGet("ping")]
    public IActionResult Ping()
    {
        return Ok(new { status = "pong", timestamp = DateTime.UtcNow });
    }
}
