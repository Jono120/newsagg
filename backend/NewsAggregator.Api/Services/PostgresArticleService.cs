using Npgsql;
using NewsAggregator.Api.Models;

namespace NewsAggregator.Api.Services;

public class PostgresArticleService : IArticleService
{
    private const string TableName = "articles";

    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresArticleService> _logger;

    public PostgresArticleService(IConfiguration configuration, ILogger<PostgresArticleService> logger)
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
            CREATE TABLE IF NOT EXISTS articles (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                description TEXT NOT NULL DEFAULT '',
                url TEXT NOT NULL UNIQUE,
                source TEXT NOT NULL DEFAULT '',
                category TEXT NOT NULL DEFAULT 'General',
                published_date TIMESTAMPTZ NOT NULL,
                scraped_date TIMESTAMPTZ NOT NULL,
                content TEXT NOT NULL DEFAULT '',
                sentiment_label TEXT NOT NULL DEFAULT 'neutral',
                sentiment_score DOUBLE PRECISION NOT NULL DEFAULT 0,
                sentiment_confidence DOUBLE PRECISION NOT NULL DEFAULT 0,
                positive_words TEXT[] NOT NULL DEFAULT '{}'::TEXT[],
                negative_words TEXT[] NOT NULL DEFAULT '{}'::TEXT[]
            );

            CREATE INDEX IF NOT EXISTS idx_articles_source ON articles (source);
            CREATE INDEX IF NOT EXISTS idx_articles_category ON articles (category);
            CREATE INDEX IF NOT EXISTS idx_articles_published_date ON articles (published_date DESC);
            CREATE INDEX IF NOT EXISTS idx_articles_sentiment_label ON articles (sentiment_label);
            """;

        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("PostgreSQL article store initialized. Table: {Table}", TableName);
    }

    public Task<IEnumerable<Article>> GetArticlesAsync() => ReadArticlesAsync($"SELECT * FROM {TableName} ORDER BY published_date DESC, scraped_date DESC, id DESC");

    public async Task<Article?> GetArticleAsync(string id)
    {
        var articles = await ReadArticlesAsync($"SELECT * FROM {TableName} WHERE id = @id LIMIT 1", command => command.Parameters.AddWithValue("id", id));
        return articles.FirstOrDefault();
    }

    public async Task<Article> AddArticleAsync(Article article)
    {
        article.Id = string.IsNullOrWhiteSpace(article.Id) ? Guid.NewGuid().ToString() : article.Id;
        article.ScrapedDate = DateTimeOffset.UtcNow;

        const string sql = $"""
            INSERT INTO {TableName} (
                id,
                title,
                description,
                url,
                source,
                category,
                published_date,
                scraped_date,
                content,
                sentiment_label,
                sentiment_score,
                sentiment_confidence,
                positive_words,
                negative_words
            ) VALUES (
                @id,
                @title,
                @description,
                @url,
                @source,
                @category,
                @published_date,
                @scraped_date,
                @content,
                @sentiment_label,
                @sentiment_score,
                @sentiment_confidence,
                @positive_words,
                @negative_words
            );
            """;

        await ExecuteAsync(sql, command => BindArticleParameters(command, article));
        return article;
    }

    public async Task UpdateArticleAsync(string id, Article article)
    {
        const string sql = $"""
            UPDATE {TableName}
            SET
                title = @title,
                description = @description,
                url = @url,
                source = @source,
                category = @category,
                published_date = @published_date,
                scraped_date = @scraped_date,
                content = @content,
                sentiment_label = @sentiment_label,
                sentiment_score = @sentiment_score,
                sentiment_confidence = @sentiment_confidence,
                positive_words = @positive_words,
                negative_words = @negative_words
            WHERE id = @id;
            """;

        article.Id = id;
        await ExecuteAsync(sql, command => BindArticleParameters(command, article));
    }

    public async Task DeleteArticleAsync(string id)
    {
        const string sql = $"DELETE FROM {TableName} WHERE id = @id;";
        await ExecuteAsync(sql, command => command.Parameters.AddWithValue("id", id));
    }

    public Task<IEnumerable<Article>> GetArticlesBySourceAsync(string source) =>
        ReadArticlesAsync($"SELECT * FROM {TableName} WHERE source = @source ORDER BY published_date DESC, scraped_date DESC, id DESC", command => command.Parameters.AddWithValue("source", source));

    public Task<IEnumerable<Article>> GetArticlesByCategoryAsync(string category) =>
        ReadArticlesAsync($"SELECT * FROM {TableName} WHERE category = @category ORDER BY published_date DESC, scraped_date DESC, id DESC", command => command.Parameters.AddWithValue("category", category));

    public async Task<Article?> GetArticleByUrlAsync(string url)
    {
        var articles = await ReadArticlesAsync($"SELECT * FROM {TableName} WHERE url = @url LIMIT 1", command => command.Parameters.AddWithValue("url", url));
        return articles.FirstOrDefault();
    }

    public async Task<(int added, int skipped, int updated, List<string> errors)> AddArticlesBatchAsync(IEnumerable<Article> articles)
    {
        int added = 0;
        int skipped = 0;
        int updated = 0;
        var errors = new List<string>();

        foreach (var article in articles)
        {
            try
            {
                var existing = await GetArticleByUrlAsync(article.Url);
                if (existing != null)
                {
                    var shouldUpdate = false;

                    if (article.SentimentConfidence > existing.SentimentConfidence + 0.01)
                        shouldUpdate = true;

                    if (!string.IsNullOrEmpty(article.Content) && string.IsNullOrEmpty(existing.Content))
                        shouldUpdate = true;

                    if (article.PublishedDate > existing.PublishedDate)
                        shouldUpdate = true;

                    if (shouldUpdate)
                    {
                        article.Id = existing.Id;
                        await UpdateArticleAsync(existing.Id, article);
                        updated++;
                        continue;
                    }

                    skipped++;
                    continue;
                }

                await AddArticleAsync(article);
                added++;
            }
            catch (Exception ex)
            {
                errors.Add($"Error adding article '{article.Title}': {ex.Message}");
            }
        }

        return (added, skipped, updated, errors);
    }

    public async Task<Dictionary<string, int>> GetArticleCountsBySourceAsync()
    {
        var counts = new Dictionary<string, int>();
        const string sql = $"SELECT COALESCE(source, '') AS source, COUNT(*)::INT AS article_count FROM {TableName} GROUP BY source ORDER BY source;";

        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var source = reader.GetString(0);
            var count = reader.GetInt32(1);
            counts[source] = count;
        }

        return counts;
    }

    public async Task<Dictionary<string, int>> GetArticleCountsBySentimentAsync()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["positive"] = 0,
            ["neutral"] = 0,
            ["negative"] = 0
        };

        const string sql = $"SELECT COALESCE(NULLIF(lower(trim(sentiment_label)), ''), 'neutral') AS sentiment_label, COUNT(*)::INT AS article_count FROM {TableName} GROUP BY 1;";

        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var label = reader.GetString(0);
            var count = reader.GetInt32(1);

            if (!counts.ContainsKey(label))
            {
                label = "neutral";
            }

            counts[label] += count;
        }

        return counts;
    }

    public Task<IEnumerable<Article>> GetArticlesSinceAsync(DateTimeOffset since) =>
        ReadArticlesAsync($"SELECT * FROM {TableName} WHERE published_date >= @since ORDER BY published_date DESC, scraped_date DESC, id DESC", command => command.Parameters.AddWithValue("since", since));

    public async Task<long> GetTotalArticleCountAsync()
    {
        const string sql = $"SELECT COUNT(*) FROM {TableName};";
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? 0 : Convert.ToInt64(result);
    }

    private async Task<IEnumerable<Article>> ReadArticlesAsync(string sql, Action<NpgsqlCommand>? configureCommand = null)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        configureCommand?.Invoke(command);

        var articles = new List<Article>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            articles.Add(MapArticle(reader));
        }

        return articles;
    }

    private async Task ExecuteAsync(string sql, Action<NpgsqlCommand>? configureCommand = null)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        configureCommand?.Invoke(command);
        await command.ExecuteNonQueryAsync();
    }

    private static void BindArticleParameters(NpgsqlCommand command, Article article)
    {
        command.Parameters.AddWithValue("id", article.Id);
        command.Parameters.AddWithValue("title", article.Title ?? string.Empty);
        command.Parameters.AddWithValue("description", article.Description ?? string.Empty);
        command.Parameters.AddWithValue("url", article.Url ?? string.Empty);
        command.Parameters.AddWithValue("source", article.Source ?? string.Empty);
        command.Parameters.AddWithValue("category", article.Category ?? string.Empty);
        command.Parameters.AddWithValue("published_date", article.PublishedDate);
        command.Parameters.AddWithValue("scraped_date", article.ScrapedDate);
        command.Parameters.AddWithValue("content", article.Content ?? string.Empty);
        command.Parameters.AddWithValue("sentiment_label", article.SentimentLabel ?? "neutral");
        command.Parameters.AddWithValue("sentiment_score", article.SentimentScore);
        command.Parameters.AddWithValue("sentiment_confidence", article.SentimentConfidence);
        command.Parameters.AddWithValue("positive_words", article.PositiveWords?.ToArray() ?? Array.Empty<string>());
        command.Parameters.AddWithValue("negative_words", article.NegativeWords?.ToArray() ?? Array.Empty<string>());
    }

    private static Article MapArticle(NpgsqlDataReader reader)
    {
        return new Article
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            Title = reader.GetString(reader.GetOrdinal("title")),
            Description = reader.GetString(reader.GetOrdinal("description")),
            Url = reader.GetString(reader.GetOrdinal("url")),
            Source = reader.GetString(reader.GetOrdinal("source")),
            Category = reader.GetString(reader.GetOrdinal("category")),
            PublishedDate = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("published_date")),
            ScrapedDate = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("scraped_date")),
            Content = reader.GetString(reader.GetOrdinal("content")),
            SentimentLabel = reader.GetString(reader.GetOrdinal("sentiment_label")),
            SentimentScore = reader.GetDouble(reader.GetOrdinal("sentiment_score")),
            SentimentConfidence = reader.GetDouble(reader.GetOrdinal("sentiment_confidence")),
            PositiveWords = ReadStringArray(reader, "positive_words"),
            NegativeWords = ReadStringArray(reader, "negative_words")
        };
    }

    private static List<string> ReadStringArray(NpgsqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return new List<string>();
        }

        return reader.GetFieldValue<string[]>(ordinal).ToList();
    }
}