using Azure;
using Azure.AI.TextAnalytics;

namespace NewsAggregator.Api.Services;

/// <summary>
/// Azure AI Language (Text Analytics) implementation. One resource provides
/// document sentiment with opinion mining, key-phrase extraction and entity
/// recognition.
/// </summary>
public class AzureTextAnalyticsService : ITextAnalyticsService
{
    private const int MaxDocChars = 5000;
    private const int MaxTerms = 15;

    private readonly TextAnalyticsClient _client;
    private readonly SentimentAnalyzerService _rulesFallback;
    private readonly ILogger<AzureTextAnalyticsService> _logger;

    public AzureTextAnalyticsService(
        TextAnalyticsClient client,
        SentimentAnalyzerService rulesFallback,
        ILogger<AzureTextAnalyticsService> logger)
    {
        _client = client;
        _rulesFallback = rulesFallback;
        _logger = logger;
    }

    public bool ShouldReanalyze(string? label, double confidence) =>
        _rulesFallback.ShouldReanalyze(label, confidence);

    public async Task<TextAnalyticsResult> AnalyzeAsync(string? title, string? description = null, CancellationToken cancellationToken = default)
    {
        var text = $"{(title ?? string.Empty).Trim()} {(description ?? string.Empty).Trim()}".Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return TextAnalyticsResult.Neutral();
        }

        if (text.Length > MaxDocChars)
        {
            text = text[..MaxDocChars];
        }

        try
        {
            var positiveWords = new List<string>();
            var negativeWords = new List<string>();

            DocumentSentiment sentiment = await _client.AnalyzeSentimentAsync(
                text,
                options: new AnalyzeSentimentOptions { IncludeOpinionMining = true },
                cancellationToken: cancellationToken);

            var label = NormalizeLabel(sentiment.Sentiment.ToString());
            var scores = sentiment.ConfidenceScores;
            var score = Math.Round(scores.Positive - scores.Negative, 4);
            var confidence = label switch
            {
                "positive" => scores.Positive,
                "negative" => scores.Negative,
                _ => Math.Max(scores.Neutral, Math.Max(scores.Positive, scores.Negative))
            };

            foreach (var s in sentiment.Sentences)
            {
                foreach (var opinion in s.Opinions)
                {
                    var target = opinion.Target.Text?.Trim();
                    if (string.IsNullOrEmpty(target))
                    {
                        continue;
                    }

                    if (opinion.Target.Sentiment == TextSentiment.Positive)
                    {
                        positiveWords.Add(target);
                    }
                    else if (opinion.Target.Sentiment == TextSentiment.Negative)
                    {
                        negativeWords.Add(target);
                    }
                }
            }

            var keyPhrases = new List<string>();
            try
            {
                var kp = await _client.ExtractKeyPhrasesAsync(text, cancellationToken: cancellationToken);
                keyPhrases.AddRange(kp.Value);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Azure key-phrase extraction failed");
            }

            var entities = new List<string>();
            try
            {
                var ents = await _client.RecognizeEntitiesAsync(text, cancellationToken: cancellationToken);
                entities.AddRange(ents.Value.Select(e => e.Text));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Azure entity recognition failed");
            }

            return new TextAnalyticsResult(
                label,
                Math.Clamp(score, -1.0, 1.0),
                Math.Clamp(confidence, 0.0, 1.0),
                Dedupe(positiveWords),
                Dedupe(negativeWords),
                Dedupe(keyPhrases),
                Dedupe(entities));
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Azure Language request failed; falling back to rules engine");
            return _rulesFallback.AnalyzeText(title, description);
        }
    }

    private static string NormalizeLabel(string sentiment)
    {
        var label = (sentiment ?? string.Empty).Trim().ToLowerInvariant();
        return label is "positive" or "negative" or "neutral" ? label : "neutral";
    }

    private static List<string> Dedupe(IEnumerable<string> values)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cleaned = new List<string>();
        foreach (var value in values)
        {
            var normalized = value?.Trim();
            if (string.IsNullOrEmpty(normalized) || !seen.Add(normalized))
            {
                continue;
            }

            cleaned.Add(normalized);
            if (cleaned.Count >= MaxTerms)
            {
                break;
            }
        }

        return cleaned;
    }
}
