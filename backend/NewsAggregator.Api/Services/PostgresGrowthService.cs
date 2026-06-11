using Npgsql;
using NewsAggregator.Api.Models;

namespace NewsAggregator.Api.Services;

public class PostgresGrowthService : IGrowthService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresGrowthService> _logger;

    public PostgresGrowthService(IConfiguration configuration, ILogger<PostgresGrowthService> logger)
    {
        var connectionString = configuration.GetConnectionString("NewsAggregator")
            ?? configuration["Postgres:ConnectionString"]
            ?? throw new InvalidOperationException("Missing PostgreSQL connection string. Set ConnectionStrings:NewsAggregator.");

        _dataSource = NpgsqlDataSource.Create(connectionString);
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS email_signups (
                id UUID PRIMARY KEY,
                email TEXT NOT NULL UNIQUE,
                source_context TEXT NOT NULL DEFAULT 'unknown',
                utm_source TEXT NULL,
                utm_medium TEXT NULL,
                utm_campaign TEXT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS analytics_events (
                id UUID PRIMARY KEY,
                event_name TEXT NOT NULL,
                channel TEXT NOT NULL DEFAULT 'direct',
                source_context TEXT NOT NULL DEFAULT 'unknown',
                utm_source TEXT NULL,
                utm_medium TEXT NULL,
                utm_campaign TEXT NULL,
                occurred_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            CREATE INDEX IF NOT EXISTS idx_email_signups_created_at ON email_signups (created_at DESC);
            CREATE INDEX IF NOT EXISTS idx_email_signups_utm_source ON email_signups (utm_source);
            CREATE INDEX IF NOT EXISTS idx_analytics_events_occurred_at ON analytics_events (occurred_at DESC);
            CREATE INDEX IF NOT EXISTS idx_analytics_events_name ON analytics_events (event_name);
            """;

        await ExecuteAsync(sql);
        _logger.LogInformation("PostgreSQL growth store initialized.");
    }

    public async Task<(bool created, EmailSignup signup)> CreateSignupAsync(EmailSignup signup)
    {
        signup.Email = signup.Email.Trim().ToLowerInvariant();
        signup.CreatedAt = DateTimeOffset.UtcNow;
        signup.Id = Guid.NewGuid();

        const string sql = """
            INSERT INTO email_signups (id, email, source_context, utm_source, utm_medium, utm_campaign, created_at)
            VALUES (@id, @email, @source_context, @utm_source, @utm_medium, @utm_campaign, @created_at)
            ON CONFLICT (email) DO NOTHING;
            """;

        var rows = await ExecuteAsync(sql, command =>
        {
            command.Parameters.AddWithValue("id", signup.Id);
            command.Parameters.AddWithValue("email", signup.Email);
            command.Parameters.AddWithValue("source_context", signup.SourceContext ?? "unknown");
            command.Parameters.AddWithValue("utm_source", (object?)signup.UtmSource ?? DBNull.Value);
            command.Parameters.AddWithValue("utm_medium", (object?)signup.UtmMedium ?? DBNull.Value);
            command.Parameters.AddWithValue("utm_campaign", (object?)signup.UtmCampaign ?? DBNull.Value);
            command.Parameters.AddWithValue("created_at", signup.CreatedAt);
        });

        return (rows > 0, signup);
    }

    public async Task TrackEventAsync(AnalyticsEvent analyticsEvent)
    {
        analyticsEvent.Id = Guid.NewGuid();
        analyticsEvent.OccurredAt = DateTimeOffset.UtcNow;

        const string sql = """
            INSERT INTO analytics_events (id, event_name, channel, source_context, utm_source, utm_medium, utm_campaign, occurred_at)
            VALUES (@id, @event_name, @channel, @source_context, @utm_source, @utm_medium, @utm_campaign, @occurred_at);
            """;

        await ExecuteAsync(sql, command =>
        {
            command.Parameters.AddWithValue("id", analyticsEvent.Id);
            command.Parameters.AddWithValue("event_name", analyticsEvent.EventName);
            command.Parameters.AddWithValue("channel", analyticsEvent.Channel ?? "direct");
            command.Parameters.AddWithValue("source_context", analyticsEvent.SourceContext ?? "unknown");
            command.Parameters.AddWithValue("utm_source", (object?)analyticsEvent.UtmSource ?? DBNull.Value);
            command.Parameters.AddWithValue("utm_medium", (object?)analyticsEvent.UtmMedium ?? DBNull.Value);
            command.Parameters.AddWithValue("utm_campaign", (object?)analyticsEvent.UtmCampaign ?? DBNull.Value);
            command.Parameters.AddWithValue("occurred_at", analyticsEvent.OccurredAt);
        });
    }

    public async Task<GrowthMetricsSummary> GetGrowthSummaryAsync(int days, decimal adSpendNzd)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, days));
        var summary = new GrowthMetricsSummary
        {
            Days = Math.Max(1, days),
            AdSpendNzd = adSpendNzd
        };

        summary.Visitors = await ReadLongAsync(
            "SELECT COUNT(*) FROM analytics_events WHERE event_name = 'page_view' AND occurred_at >= @since;",
            command => command.Parameters.AddWithValue("since", since));

        summary.Signups = await ReadLongAsync(
            "SELECT COUNT(*) FROM email_signups WHERE created_at >= @since;",
            command => command.Parameters.AddWithValue("since", since));

        summary.ConversionRate = summary.Visitors > 0
            ? (double)summary.Signups / summary.Visitors
            : 0;

        summary.CostPerSignupNzd = summary.Signups > 0
            ? decimal.Round(summary.AdSpendNzd / summary.Signups, 2)
            : 0;

        const string sourceSql = """
            SELECT COALESCE(NULLIF(utm_source, ''), 'direct') AS signup_source, COUNT(*)::BIGINT AS signup_count
            FROM email_signups
            WHERE created_at >= @since
            GROUP BY 1
            ORDER BY 2 DESC;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sourceSql, connection);
        command.Parameters.AddWithValue("since", since);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            summary.SignupsBySource[reader.GetString(0)] = reader.GetInt64(1);
        }

        return summary;
    }

    private async Task<long> ReadLongAsync(string sql, Action<NpgsqlCommand> configure)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        configure(command);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? 0 : Convert.ToInt64(result);
    }

    private async Task<int> ExecuteAsync(string sql, Action<NpgsqlCommand>? configure = null)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        configure?.Invoke(command);
        return await command.ExecuteNonQueryAsync();
    }
}
