using Microsoft.AspNetCore.Mvc;
using NewsAggregator.Api.Services;

namespace NewsAggregator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IArticleService _articleService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(IArticleService articleService, ILogger<HealthController> logger)
    {
        _articleService = articleService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> GetHealth()
    {
        try
        {
            // Test database connectivity.
            var totalCount = await _articleService.GetTotalArticleCountAsync();
            
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

    [HttpGet("ready")]
    public async Task<IActionResult> Readiness()
    {
        try
        {
            await _articleService.GetTotalArticleCountAsync();
            return Ok(new { status = "ready", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness check failed");
            return StatusCode(503, new { status = "not_ready", timestamp = DateTime.UtcNow });
        }
    }
}
