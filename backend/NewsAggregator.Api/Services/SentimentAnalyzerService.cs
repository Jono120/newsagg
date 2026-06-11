using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NewsAggregator.Api.Services;

public record SentimentResult(string Label, double Score, double Confidence);

/// <summary>
/// Offline, rule-based text analytics. Serves as the <c>rules</c> provider and
/// as the fallback when the Azure AI Language provider is unavailable.
/// </summary>
public class SentimentAnalyzerService : ITextAnalyticsService
{
    private static readonly HashSet<string> POSITIVE_WORDS = new(StringComparer.OrdinalIgnoreCase)
    {
        "gain", "gains", "growth", "grow", "rises", "rise", "surge", "surges",
        "wins", "win", "record", "strong", "improve", "improves", "improved",
        "booming", "boost", "breakthrough", "success", "positive", "upbeat", "hope"
    };

    private static readonly HashSet<string> NEGATIVE_WORDS = new(StringComparer.OrdinalIgnoreCase)
    {
        "fall", "falls", "drop", "drops", "slump", "slumps", "crash", "crashes",
        "loss", "losses", "weak", "decline", "declines", "recession", "cut", "cuts",
        "crisis", "fear", "warning", "risk", "negative", "concern", "concerns", "down",
        "lies", "fraud", "scandal", "secrecy", "corruption", "embezzlement", "bribery",
        "money laundering", "insider trading", "tax evasion", "cyber attack", "data breach",
        "lawsuit", "regulatory", "investigation", "bankruptcy", "default", "downgrade",
        "profits", "dies", "rape"
    };

    private static readonly HashSet<string> INTENSIFIERS = new(StringComparer.OrdinalIgnoreCase)
    {
        "very", "extremely", "highly", "sharply", "significantly", "big", "major",
        "massive", "huge", "dramatic", "dramatically", "strongly"
    };

    private static readonly HashSet<string> NEGATORS = new(StringComparer.OrdinalIgnoreCase)
    {
        "no", "not", "never", "none", "without", "hardly", "rarely", "n't"
    };

    private static readonly HashSet<string> CONTRAST_WORDS = new(StringComparer.OrdinalIgnoreCase)
    {
        "but", "however", "although", "though", "yet"
    };

    private static readonly HashSet<string> POSITIVE_PHRASES = new(StringComparer.OrdinalIgnoreCase)
    {
        "record high",
        "beats expectations",
        "strong growth",
        "turns around",
        "breakthrough success",
    };

    private static readonly HashSet<string> NEGATIVE_PHRASES = new(StringComparer.OrdinalIgnoreCase)
    {
        "data breach",
        "cyber attack",
        "money laundering",
        "insider trading",
        "tax evasion",
        "under investigation",
        "files bankruptcy",
        "profit warning",
        "misses expectations",
    };

    private const double HF_BLEND_WEIGHT = 0.45;

    /// <summary>
    /// Analyse sentiment from title text using rule-based heuristics.
    /// </summary>
    public SentimentResult AnalyzeTitleSentiment(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return new SentimentResult("neutral", 0.0, 0.0);
        }

        // Rule-based scoring.
        var (ruleScore, matched, tokenCount) = AnalyzeTokens(title);

        // ML integration may be added later. For now, this stays rule-based.
        var normalizedScore = Math.Tanh(ruleScore / 3.0);

        var label = normalizedScore switch
        {
            >= 0.2 => "positive",
            <= -0.2 => "negative",
            _ => "neutral"
        };

        var confidence = Math.Min(
            1.0,
            Math.Max(Math.Abs(normalizedScore), matched / Math.Max(1, tokenCount))
        );

        return new SentimentResult(
            label,
            Math.Round(normalizedScore, 4),
            Math.Round(confidence, 4)
        );
    }

    /// <summary>
    /// <see cref="ITextAnalyticsService"/> entry point. Wraps the rule-based
    /// sentiment scoring and surfaces matched positive/negative keywords.
    /// </summary>
    public Task<TextAnalyticsResult> AnalyzeAsync(string? title, string? description = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(AnalyzeText(title, description));

    /// <summary>
    /// Synchronous rule-based analysis used by the fallback path.
    /// </summary>
    public TextAnalyticsResult AnalyzeText(string? title, string? description = null)
    {
        var text = $"{(title ?? string.Empty).Trim()} {(description ?? string.Empty).Trim()}".Trim();
        var sentiment = AnalyzeTitleSentiment(text);

        var positiveWords = new List<string>();
        var negativeWords = new List<string>();
        foreach (var token in Tokenize(text).Distinct())
        {
            if (POSITIVE_WORDS.Contains(token))
            {
                positiveWords.Add(token);
            }
            else if (NEGATIVE_WORDS.Contains(token))
            {
                negativeWords.Add(token);
            }
        }

        return new TextAnalyticsResult(
            sentiment.Label,
            sentiment.Score,
            sentiment.Confidence,
            positiveWords,
            negativeWords,
            new List<string>(),
            new List<string>());
    }

    /// <summary>
    /// Check whether sentiment is low-confidence and warrants re-analysis.
    /// </summary>
    public bool ShouldReanalyze(string? label, double confidence)
    {
        // Re-analyse if neutral or low confidence (<0.3).
        return string.IsNullOrWhiteSpace(label)
            || label.Equals("neutral", StringComparison.OrdinalIgnoreCase)
            || confidence < 0.3;
    }

    private (double score, int matched, int tokenCount) AnalyzeTokens(string title)
    {
        var tokens = Tokenize(title);

        if (tokens.Count == 0)
        {
            return (0.0, 0, 0);
        }

        double rawScore = 0.0;
        int matched = 0;

        // Phrase-level hits.
        var normalizedText = string.Join(" ", tokens);
        var positiveHits = POSITIVE_PHRASES.Count(phrase => normalizedText.Contains(phrase, StringComparison.OrdinalIgnoreCase));
        var negativeHits = NEGATIVE_PHRASES.Count(phrase => normalizedText.Contains(phrase, StringComparison.OrdinalIgnoreCase));

        if (positiveHits > 0)
        {
            rawScore += positiveHits * 1.75;
            matched += positiveHits;
        }

        if (negativeHits > 0)
        {
            rawScore -= negativeHits * 1.75;
            matched += negativeHits;
        }

        // Find contrast word position.
        int contrastIdx = tokens.FindIndex(t => CONTRAST_WORDS.Contains(t));

        // Token-level sentiment.
        for (int idx = 0; idx < tokens.Count; idx++)
        {
            var token = tokens[idx];
            double weight = 1.0;

            // Intensifier boost.
            if (idx > 0 && INTENSIFIERS.Contains(tokens[idx - 1]))
            {
                weight = 1.5;
            }

            // Contrast boost (tokens after 'but', 'however', etc. have higher weight).
            if (contrastIdx != -1 && idx > contrastIdx)
            {
                weight *= 1.2;
            }

            // Negation handling.
            bool negated = false;
            if (idx > 0 && NEGATORS.Contains(tokens[idx - 1]))
            {
                negated = true;
            }
            else if (idx > 1 && NEGATORS.Contains(tokens[idx - 2]) && INTENSIFIERS.Contains(tokens[idx - 1]))
            {
                negated = true;
            }

            if (POSITIVE_WORDS.Contains(token))
            {
                rawScore += negated ? -weight : weight;
                matched++;
            }
            else if (NEGATIVE_WORDS.Contains(token))
            {
                rawScore -= negated ? -weight : weight;
                matched++;
            }
        }

        // Question damping.
        if (title.Contains("?"))
        {
            rawScore *= 0.9;
        }

        return (rawScore, matched, tokens.Count);
    }

    private List<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        // Extract words: alphanumeric plus apostrophe.
        var matches = Regex.Matches(text, @"[a-zA-Z']+", RegexOptions.IgnoreCase);
        return matches.Cast<Match>().Select(m => m.Value.ToLowerInvariant()).ToList();
    }
}
