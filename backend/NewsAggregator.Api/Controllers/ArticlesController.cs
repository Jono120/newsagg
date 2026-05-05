using Microsoft.AspNetCore.Mvc;
using NewsAggregator.Api.Models;
using System.Linq;
using System;
using NewsAggregator.Api.Services;

namespace NewsAggregator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArticlesController : ControllerBase
{
    private readonly IArticleService _articleService;
    private readonly ILogger<ArticlesController> _logger;

    public ArticlesController(
        IArticleService articleService,
        ILogger<ArticlesController> logger)
    {
        _articleService = articleService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetArticles(
        [FromQuery] string? source = null,
        [FromQuery] string? category = null,
        [FromQuery] string? tz = null)
    {
        try
        {
            IEnumerable<Article> articles;

            if (!string.IsNullOrEmpty(source))
            {
                articles = await _articleService.GetArticlesBySourceAsync(source);
            }
            else if (!string.IsNullOrEmpty(category))
            {
                articles = await _articleService.GetArticlesByCategoryAsync(category);
            }
            else
            {
                articles = await _articleService.GetArticlesAsync();
            }

            if (!string.IsNullOrEmpty(tz) && tz.Equals("nz", StringComparison.OrdinalIgnoreCase))
            {
                var converted = articles.Select(a => ConvertArticleToNz(a));
                return Ok(converted);
            }

            return Ok(articles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving articles");
            return StatusCode(500, "An error occurred while retrieving articles");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetArticle(string id, [FromQuery] string? tz = null)
    {
        try
        {
            var article = await _articleService.GetArticleAsync(id);
            
            if (article == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(tz) && tz.Equals("nz", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(ConvertArticleToNz(article));
            }

            return Ok(article);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving article {ArticleId}", id);
            return StatusCode(500, "An error occurred while retrieving the article");
        }
    }

    private object? ConvertArticleToNz(Article a)
    {
        if (a == null) return null;

        TimeZoneInfo nz;
        try { nz = TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time"); }
        catch { nz = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland"); }

        var pubNz = TimeZoneInfo.ConvertTime(a.PublishedDate, nz);
        var scrapedNz = TimeZoneInfo.ConvertTime(a.ScrapedDate, nz);

        return new
        {
            id = a.Id,
            title = a.Title,
            description = a.Description,
            url = a.Url,
            source = a.Source,
            category = a.Category,
            publishedDate = pubNz.ToString("o"),
            scrapedDate = scrapedNz.ToString("o"),
            content = a.Content,
            sentimentLabel = a.SentimentLabel,
            sentimentScore = a.SentimentScore,
            sentimentConfidence = a.SentimentConfidence,
            positiveWords = a.PositiveWords,
            negativeWords = a.NegativeWords
        };
    }

    [HttpPost]
    public async Task<ActionResult<Article>> CreateArticle([FromBody] Article article)
    {
        try
        {
            // Check for duplicate by URL
            var existing = await _articleService.GetArticleByUrlAsync(article.Url);
            if (existing != null)
            {
                _logger.LogInformation("Article already exists: {Url}", article.Url);
                return Conflict(new { message = "Article with this URL already exists", existingId = existing.Id });
            }

            var createdArticle = await _articleService.AddArticleAsync(article);
            return CreatedAtAction(nameof(GetArticle), new { id = createdArticle.Id }, createdArticle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating article");
            return StatusCode(500, "An error occurred while creating the article");
        }
    }

    [HttpPost("batch")]
    public async Task<ActionResult> CreateArticlesBatch([FromBody] List<Article> articles)
    {
        try
        {
            if (articles == null || !articles.Any())
            {
                return BadRequest(new { message = "No articles provided" });
            }

            _logger.LogInformation("Receiving batch of {Count} articles", articles.Count);

            var (added, skipped, updated, errors) = await _articleService.AddArticlesBatchAsync(articles);

            var result = new
            {
                totalReceived = articles.Count,
                added,
                skipped,
                updated,
                errors = errors.Any() ? errors : null
            };

            _logger.LogInformation("Batch import complete: {Added} added, {Skipped} skipped, {Updated} updated, {Errors} errors",
                added, skipped, updated, errors.Count);

            if (errors.Any())
            {
                foreach (var error in errors)
                {
                    _logger.LogWarning("Batch import error: {Error}", error);
                }
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating articles batch");
            return StatusCode(500, "An error occurred while creating articles");
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateArticle(string id, [FromBody] Article article)
    {
        try
        {
            var existingArticle = await _articleService.GetArticleAsync(id);
            
            if (existingArticle == null)
            {
                return NotFound();
            }

            article.Id = id;
            await _articleService.UpdateArticleAsync(id, article);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating article {ArticleId}", id);
            return StatusCode(500, "An error occurred while updating the article");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteArticle(string id)
    {
        try
        {
            var existingArticle = await _articleService.GetArticleAsync(id);
            
            if (existingArticle == null)
            {
                return NotFound();
            }

            await _articleService.DeleteArticleAsync(id);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting article {ArticleId}", id);
            return StatusCode(500, "An error occurred while deleting the article");
        }
    }
}
