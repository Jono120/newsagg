using NewsAggregator.Api.Models;

namespace NewsAggregator.Api.Services;

public interface IGrowthService
{
    Task InitializeAsync();
    Task<(bool created, EmailSignup signup)> CreateSignupAsync(EmailSignup signup);
    Task TrackEventAsync(AnalyticsEvent analyticsEvent);
    Task<GrowthMetricsSummary> GetGrowthSummaryAsync(int days, decimal adSpendNzd);
}
