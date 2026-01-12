using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.IO;

namespace NewsAggregator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScraperController : ControllerBase
{
    private readonly ILogger<ScraperController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public ScraperController(
        ILogger<ScraperController> logger, 
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshArticles()
    {
        try
        {
            _logger.LogInformation("Scraper refresh requested from frontend");

            // Get the scraper path from configuration or use default
            var configPath = _configuration["Scraper:PythonScriptPath"];
            var scraperPath = configPath ?? System.IO.Path.Combine(
                _environment.ContentRootPath, 
                "..",
                "..",
                "scraper", 
                "main.py"
            );

            // Normalize path for cross-platform compatibility
            scraperPath = System.IO.Path.GetFullPath(scraperPath);

            // Check if the script exists
            if (!System.IO.File.Exists(scraperPath))
            {
                _logger.LogWarning($"Scraper script not found at: {scraperPath}");
                return NotFound(new { error = "Scraper script not found", path = scraperPath });
            }

            var workingDirectory = System.IO.Path.GetDirectoryName(scraperPath);
            _logger.LogInformation($"Starting scraper from: {scraperPath}, working dir: {workingDirectory}");

            // Start the scraper process asynchronously
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scraperPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory
                }
            };

            process.Start();

            // Log the output asynchronously without waiting for completion
            _ = Task.Run(async () =>
            {
                try
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    
                    if (!string.IsNullOrEmpty(output))
                        _logger.LogInformation($"Scraper output: {output}");
                    
                    if (!string.IsNullOrEmpty(error))
                        _logger.LogError($"Scraper error: {error}");
                    
                    await process.WaitForExitAsync();
                    _logger.LogInformation($"Scraper process completed with exit code: {process.ExitCode}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading scraper process output");
                }
            });

            return Ok(new 
            { 
                message = "Scraper refresh started",
                status = "processing",
                details = "Articles will be updated in the background. Please refresh the page in a moment."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering scraper refresh");
            return StatusCode(500, new { error = "Failed to start scraper", details = ex.Message });
        }
    }

    [HttpGet("status")]
    public IActionResult GetScraperStatus()
    {
        try
        {
            // Check if scraper is running
            var pythonProcesses = Process.GetProcessesByName("python");
            var isRunning = pythonProcesses.Length > 0;

            return Ok(new 
            { 
                status = isRunning ? "running" : "idle",
                processes = pythonProcesses.Length,
                message = isRunning ? "Scraper is currently running" : "Scraper is idle"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scraper status");
            return StatusCode(500, new { error = "Failed to get scraper status" });
        }
    }
}
