using System.Threading.RateLimiting;
using Azure;
using Azure.AI.TextAnalytics;
using Microsoft.AspNetCore.RateLimiting;
using NewsAggregator.Api.Models;
using NewsAggregator.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure PostgreSQL as the article storage backend.
builder.Services.AddSingleton<IArticleService, PostgresArticleService>();
builder.Services.AddSingleton<IGrowthService, PostgresGrowthService>();

// Text analytics / sentiment provider (azure | rules). The rules engine is
// always available and doubles as the offline fallback for the Azure provider.
builder.Services.AddSingleton<SentimentAnalyzerService>();

var analyzerProvider = (builder.Configuration["AzureLanguage:Provider"] ?? "azure").Trim().ToLowerInvariant();
var languageEndpoint = builder.Configuration["AzureLanguage:Endpoint"];
var languageKey = builder.Configuration["AzureLanguage:Key"];

if (analyzerProvider == "azure" && !string.IsNullOrWhiteSpace(languageEndpoint) && !string.IsNullOrWhiteSpace(languageKey))
{
    builder.Services.AddSingleton(_ => new TextAnalyticsClient(new Uri(languageEndpoint), new AzureKeyCredential(languageKey)));
    builder.Services.AddSingleton<ITextAnalyticsService, AzureTextAnalyticsService>();
}
else
{
    builder.Services.AddSingleton<ITextAnalyticsService>(sp => sp.GetRequiredService<SentimentAnalyzerService>());
}

// Rate limit expensive POST routes (analysis, scraper refresh) per client IP.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("expensive", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// Configure CORS for local development.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
            .WithOrigins("http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "docs";
    options.DocumentTitle = "NewsAggregator API";
});

app.UseCors("AllowFrontend");

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

// Initialize the article service and ensure the PostgreSQL schema exists.
var articleService = app.Services.GetRequiredService<IArticleService>();
await articleService.InitializeAsync();
var growthService = app.Services.GetRequiredService<IGrowthService>();
await growthService.InitializeAsync();

app.Run();
