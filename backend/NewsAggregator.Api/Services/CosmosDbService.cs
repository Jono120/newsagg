using Microsoft.Azure.Cosmos;
using NewsAggregator.Api.Models;

namespace NewsAggregator.Api.Services;

public class CosmosDbService : ICosmosDbService
{
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseName;
    private readonly string _containerName;
    private Container? _container;

    public CosmosDbService(string endpoint, string key, string databaseName, string containerName)
    {
        _cosmosClient = new CosmosClient(endpoint, key);
        _databaseName = databaseName;
        _containerName = containerName;
    }

    public async Task InitializeAsync()
    {
        // Create database if it doesn't exist
        var database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName);

        // Create container with hierarchical partition key for better query performance
        // Using /source as partition key to group articles by source
        var containerProperties = new ContainerProperties
        {
            Id = _containerName,
            PartitionKeyPath = "/source"
        };

        _container = await database.Database.CreateContainerIfNotExistsAsync(
            containerProperties,
            throughput: 400); // 400 RU/s for local development (minimum)
    }

    public async Task<IEnumerable<Article>> GetArticlesAsync()
    {
        if (_container == null) throw new InvalidOperationException("Container not initialized");

        var query = _container.GetItemQueryIterator<Article>(
            new QueryDefinition("SELECT * FROM c ORDER BY c.publishedDate DESC"));

        var results = new List<Article>();
        while (query.HasMoreResults)
        {
            var response = await query.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    public async Task<Article?> GetArticleAsync(string id)
    {
        if (_container == null) throw new InvalidOperationException("Container not initialized");

        try
        {
            var query = _container.GetItemQueryIterator<Article>(
                new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                    .WithParameter("@id", id));

            if (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                return response.FirstOrDefault();
            }

            return null;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Article> AddArticleAsync(Article article)
    {
        if (_container == null) throw new InvalidOperationException("Container not initialized");

        article.Id = Guid.NewGuid().ToString();
        article.ScrapedDate = DateTimeOffset.UtcNow;

        var response = await _container.CreateItemAsync(article, new PartitionKey(article.Source));
        return response.Resource;
    }

    public async Task UpdateArticleAsync(string id, Article article)
    {
        if (_container == null) throw new InvalidOperationException("Container not initialized");

        await _container.ReplaceItemAsync(article, id, new PartitionKey(article.Source));
    }

    public async Task DeleteArticleAsync(string id)
    {
        if (_container == null) throw new InvalidOperationException("Container not initialized");

        // Need to query first to get the partition key value
        var article = await GetArticleAsync(id);
        if (article != null)
        {
            await _container.DeleteItemAsync<Article>(id, new PartitionKey(article.Source));
        }
    }

    public async Task<IEnumerable<Article>> GetArticlesBySourceAsync(string source)
    {
        if (_container == null) throw new InvalidOperationException("Container not initialized");

        // Efficient query - filters within a single partition
        var query = _container.GetItemQueryIterator<Article>(
            new QueryDefinition("SELECT * FROM c WHERE c.source = @source ORDER BY c.publishedDate DESC")
                .WithParameter("@source", source),
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(source) });

        var results = new List<Article>();
        while (query.HasMoreResults)
        {
            var response = await query.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    public async Task<IEnumerable<Article>> GetArticlesByCategoryAsync(string category)
    {
        if (_container == null) throw new InvalidOperationException("Container not initialized");

        // Cross-partition query - will check all partitions
        var query = _container.GetItemQueryIterator<Article>(
            new QueryDefinition("SELECT * FROM c WHERE c.category = @category ORDER BY c.publishedDate DESC")
                .WithParameter("@category", category));

        var results = new List<Article>();
        while (query.HasMoreResults)
        {
            var response = await query.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    public async Task<Article?> GetArticleByUrlAsync(string url)
    {
        if (_container == null) throw new InvalidOperationException("Container not initialized");

        try
        {
            var query = _container.GetItemQueryIterator<Article>(
                new QueryDefinition("SELECT * FROM c WHERE c.url = @url")
                    .WithParameter("@url", url));

            if (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                return response.FirstOrDefault();
            }

            return null;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<(int added, int skipped, List<string> errors)> AddArticlesBatchAsync(IEnumerable<Article> articles)
    {
        if (_container == null) throw new InvalidOperationException("Container not initialized");

        int added = 0;
        int skipped = 0;
        var errors = new List<string>();

        foreach (var article in articles)
        {
            try
            {
                // Check for duplicate by URL
                var existing = await GetArticleByUrlAsync(article.Url);
                if (existing != null)
                {
                    skipped++;
                    continue;
                }

                article.Id = Guid.NewGuid().ToString();
                article.ScrapedDate = DateTimeOffset.UtcNow;

                await _container.CreateItemAsync(article, new PartitionKey(article.Source));
                added++;
            }
            catch (Exception ex)
            {
                errors.Add($"Error adding article '{article.Title}': {ex.Message}");
            }
        }

        return (added, skipped, errors);
    }

    public async Task<Dictionary<string, int>> GetArticleCountsBySourceAsync()
    {
        if (_container == null) throw new InvalidOperationException("Container not initialized");

        var query = _container.GetItemQueryIterator<dynamic>(
            new QueryDefinition("SELECT c.source, COUNT(1) as count FROM c GROUP BY c.source"));

        var results = new Dictionary<string, int>();
        while (query.HasMoreResults)
        {
            var response = await query.ReadNextAsync();
            foreach (var item in response)
            {
                results[item.source.ToString()] = (int)item.count;
            }
        }

        return results;
    }

    public async Task<long> GetTotalArticleCountAsync()
    {
        if (_container == null) throw new InvalidOperationException("Container not initialized");

        var query = _container.GetItemQueryIterator<dynamic>(
            new QueryDefinition("SELECT VALUE COUNT(1) FROM c"));

        if (query.HasMoreResults)
        {
            var response = await query.ReadNextAsync();
            var value = response.FirstOrDefault();
            return value != null ? Convert.ToInt64(value) : 0L;
        }

        return 0L;
    }
}
