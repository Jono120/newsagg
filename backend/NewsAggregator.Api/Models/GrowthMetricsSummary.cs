namespace NewsAggregator.Api.Models;

public class GrowthMetricsSummary
{
    public int Days { get; set; }
    public long Visitors { get; set; }
    public long Signups { get; set; }
    public double ConversionRate { get; set; }
    public decimal AdSpendNzd { get; set; }
    public decimal CostPerSignupNzd { get; set; }
    public Dictionary<string, long> SignupsBySource { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
