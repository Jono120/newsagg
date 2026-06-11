using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using NewsAggregator.Api.Models;
using NewsAggregator.Api.Services;

namespace NewsAggregator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GrowthController : ControllerBase
{
    private readonly IGrowthService _growthService;

    public GrowthController(IGrowthService growthService)
    {
        _growthService = growthService;
    }

    [HttpPost("subscribe")]
    public async Task<ActionResult> Subscribe([FromBody] SubscribeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        var signup = new EmailSignup
        {
            Email = request.Email,
            SourceContext = request.SourceContext ?? "unknown",
            UtmSource = request.UtmSource,
            UtmMedium = request.UtmMedium,
            UtmCampaign = request.UtmCampaign
        };

        var (created, entity) = await _growthService.CreateSignupAsync(signup);
        var status = created ? "created" : "already_subscribed";
        return Ok(new { status, signupId = entity.Id, createdAt = entity.CreatedAt });
    }

    [HttpPost("events")]
    public async Task<IActionResult> TrackEvent([FromBody] TrackEventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EventName))
        {
            return BadRequest(new { message = "EventName is required." });
        }

        await _growthService.TrackEventAsync(new AnalyticsEvent
        {
            EventName = request.EventName,
            Channel = request.Channel ?? "direct",
            SourceContext = request.SourceContext ?? "unknown",
            UtmSource = request.UtmSource,
            UtmMedium = request.UtmMedium,
            UtmCampaign = request.UtmCampaign
        });

        return Accepted();
    }

    [HttpGet("summary")]
    public async Task<ActionResult<GrowthMetricsSummary>> GetSummary(
        [FromQuery] int days = 30,
        [FromQuery] decimal adSpendNzd = 0)
    {
        var summary = await _growthService.GetGrowthSummaryAsync(days, adSpendNzd);
        return Ok(summary);
    }

    [HttpGet("scale-gate")]
    public async Task<ActionResult> EvaluateScaleGate(
        [FromQuery] int days = 30,
        [FromQuery] decimal adSpendNzd = 0,
        [FromQuery] double minConversionRate = 0.05,
        [FromQuery] decimal maxCostPerSignupNzd = 5)
    {
        var summary = await _growthService.GetGrowthSummaryAsync(days, adSpendNzd);
        var meetsConversion = summary.ConversionRate >= minConversionRate;
        var meetsCps = summary.Signups > 0 && summary.CostPerSignupNzd <= maxCostPerSignupNzd;
        var decision = meetsConversion && meetsCps ? "scale" : "reposition";

        return Ok(new
        {
            decision,
            rationale = new
            {
                meetsConversion,
                meetsCps,
                minConversionRate,
                maxCostPerSignupNzd
            },
            metrics = summary
        });
    }

    [HttpGet("channel-tests")]
    public ActionResult GetChannelTests()
    {
        var channels = new[]
        {
            new { channel = "meta_instagram", budgetNzd = 175, objective = "email-signup", cadence = "weekly" },
            new { channel = "google_search", budgetNzd = 175, objective = "email-signup", cadence = "weekly" },
            new { channel = "organic_community", budgetNzd = 150, objective = "email-signup", cadence = "weekly" }
        };

        return Ok(new
        {
            monthlyBudgetNzd = 500,
            weeklyOptimization = "Pause low-converting ads, iterate creatives, shift budget to top audiences.",
            channels
        });
    }

    public class SubscribeRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        public string? SourceContext { get; set; }
        public string? UtmSource { get; set; }
        public string? UtmMedium { get; set; }
        public string? UtmCampaign { get; set; }
    }

    public class TrackEventRequest
    {
        [Required]
        public string EventName { get; set; } = string.Empty;
        public string? Channel { get; set; }
        public string? SourceContext { get; set; }
        public string? UtmSource { get; set; }
        public string? UtmMedium { get; set; }
        public string? UtmCampaign { get; set; }
    }
}
