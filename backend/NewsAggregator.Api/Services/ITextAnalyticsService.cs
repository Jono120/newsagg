namespace NewsAggregator.Api.Services;

/// <summary>
/// Rich text-analytics result covering sentiment plus optional opinion terms,
/// key phrases and named entities.
/// </summary>
public record TextAnalyticsResult(
    string Label,
    double Score,
    double Confidence,
    List<string> PositiveWords,
    List<string> NegativeWords,
    List<string> KeyPhrases,
    List<string> Entities)
{
    public static TextAnalyticsResult Neutral() =>
        new("neutral", 0.0, 0.0, new(), new(), new(), new());
}

/// <summary>
/// Abstraction over the configured text-analytics provider (Azure AI Language
/// or the offline rules engine).
/// </summary>
public interface ITextAnalyticsService
{
    Task<TextAnalyticsResult> AnalyzeAsync(string? title, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>True when a stored sentiment is weak enough to warrant re-analysis.</summary>
    bool ShouldReanalyze(string? label, double confidence);
}
