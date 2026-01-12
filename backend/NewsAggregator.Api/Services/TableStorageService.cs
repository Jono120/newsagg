using Azure;
using Azure.Data.Tables;
using NewsAggregator.Api.Models;

namespace NewsAggregator.Api.Services;

public class TableStorageService : ICosmosDbService
{
    private readonly TableClient _tableClient;
    private readonly ILogger<TableStorageService> _logger;

    public TableStorageService(string connectionString, ILogger<TableStorageService> logger)
    {
        _logger = logger;
        try
        {
            // Use direct connection string for Azurite
            var tableServiceClient = new TableServiceClient(new Uri("http://127.0.0.1:10102/devstoreaccount1"));
            _tableClient = tableServiceClient.GetTableClient("articles");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Table Storage");
            throw;
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _tableClient.CreateAsync();
            _logger.LogInformation("Table Storage initialized successfully");
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 409)
        {
            // Table already exists
            _logger.LogInformation("Table Storage already exists");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Table Storage");
            throw;
        }
    }

    public async Task<IEnumerable<Article>> GetArticlesAsync()
    {
        try
        {
            var articles = new List<Article>();
            await foreach (var entity in _tableClient.QueryAsync<ArticleTableEntity>())
            {
                articles.Add(entity.ToArticle());
            }
            return articles.OrderByDescending(a => a.PublishedDate).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving articles from Table Storage");
            throw;
        }
    }

    public async Task<Article?> GetArticleAsync(string id)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<ArticleTableEntity>("Article", id);
            return response.Value.ToArticle();
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving article {ArticleId} from Table Storage", id);
            throw;
        }
    }

    public async Task<Article> AddArticleAsync(Article article)
    {
        try
        {
            article.Id = Guid.NewGuid().ToString();
            var entity = ArticleTableEntity.FromArticle(article);
            await _tableClient.AddEntityAsync(entity);
            _logger.LogInformation("Article {ArticleId} created in Table Storage", article.Id);
            return article;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 409)
        {
            throw new InvalidOperationException($"Article with this URL already exists", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating article in Table Storage");
            throw;
        }
    }

    public async Task UpdateArticleAsync(string id, Article article)
    {
        try
        {
            var entity = ArticleTableEntity.FromArticle(article);
            entity.RowKey = id;
            await _tableClient.UpdateEntityAsync(entity, ETag.All);
            _logger.LogInformation("Article {ArticleId} updated in Table Storage", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating article {ArticleId} in Table Storage", id);
            throw;
        }
    }

    public async Task DeleteArticleAsync(string id)
    {
        try
        {
            await _tableClient.DeleteEntityAsync("Article", id);
            _logger.LogInformation("Article {ArticleId} deleted from Table Storage", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting article {ArticleId} from Table Storage", id);
            throw;
        }
    }

    public async Task<Article?> GetArticleByUrlAsync(string url)
    {
        try
        {
            await foreach (var entity in _tableClient.QueryAsync<ArticleTableEntity>(e => e.Url == url))
            {
                return entity.ToArticle();
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for duplicate URL in Table Storage");
            throw;
        }
    }

    public async Task<IEnumerable<Article>> GetArticlesBySourceAsync(string source)
    {
        try
        {
            var articles = new List<Article>();
            await foreach (var entity in _tableClient.QueryAsync<ArticleTableEntity>(e => e.Source == source))
            {
                articles.Add(entity.ToArticle());
            }
            return articles.OrderByDescending(a => a.PublishedDate).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving articles by source {Source}", source);
            throw;
        }
    }

    public async Task<IEnumerable<Article>> GetArticlesByCategoryAsync(string category)
    {
        try
        {
            var articles = new List<Article>();
            await foreach (var entity in _tableClient.QueryAsync<ArticleTableEntity>(e => e.Category == category))
            {
                articles.Add(entity.ToArticle());
            }
            return articles.OrderByDescending(a => a.PublishedDate).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving articles by category {Category}", category);
            throw;
        }
    }

    public async Task<(int added, int skipped, List<string> errors)> AddArticlesBatchAsync(IEnumerable<Article> articles)
    {
        var added = 0;
        var skipped = 0;
        var errors = new List<string>();

        try
        {
            foreach (var article in articles)
            {
                try
                {
                    // Check for duplicates
                    var existingArticle = await GetArticleByUrlAsync(article.Url);
                    if (existingArticle != null)
                    {
                        skipped++;
                        continue;
                    }

                    await AddArticleAsync(article);
                    added++;
                }
                catch (InvalidOperationException)
                {
                    skipped++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Error adding article '{article.Title}': {ex.Message}");
                }
            }

            _logger.LogInformation("Batch import completed: {Added} added, {Skipped} skipped, {Errors} errors",
                added, skipped, errors.Count);

            return (added, skipped, errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during batch import");
            throw;
        }
    }

    public async Task<Dictionary<string, int>> GetArticleCountsBySourceAsync()
    {
        try
        {
            var counts = new Dictionary<string, int>();
            await foreach (var article in _tableClient.QueryAsync<ArticleTableEntity>())
            {
                if (!counts.ContainsKey(article.Source))
                {
                    counts[article.Source] = 0;
                }
                counts[article.Source]++;
            }
            return counts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting article counts by source");
            throw;
        }
    }

    public async Task<long> GetTotalArticleCountAsync()
    {
        try
        {
            long count = 0;
            await foreach (var _ in _tableClient.QueryAsync<ArticleTableEntity>())
            {
                count++;
            }
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting total article count");
            throw;
        }
    }
}

public class ArticleTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "Article";
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public DateTimeOffset PublishedDate { get; set; }
    public DateTimeOffset ScrapedDate { get; set; }

    public static ArticleTableEntity FromArticle(Article article)
    {
        return new ArticleTableEntity
        {
            RowKey = article.Id,
            Title = article.Title,
            Url = article.Url,
            Source = article.Source,
            Description = article.Description,
            Category = article.Category,
            PublishedDate = article.PublishedDate,
            ScrapedDate = article.ScrapedDate
        };
    }

    public Article ToArticle()
    {
        return new Article
        {
            Id = RowKey,
            Title = Title,
            Url = Url,
            Source = Source,
            Description = Description ?? string.Empty,
            Category = Category ?? "General",
            PublishedDate = PublishedDate,
            ScrapedDate = ScrapedDate
        };
    }
}
