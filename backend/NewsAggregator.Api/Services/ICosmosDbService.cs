using NewsAggregator.Api.Models;

namespace NewsAggregator.Api.Services;

public interface ICosmosDbService
{
    Task InitializeAsync();
    Task<IEnumerable<Article>> GetArticlesAsync();
    Task<Article?> GetArticleAsync(string id);
    Task<Article> AddArticleAsync(Article article);
    Task UpdateArticleAsync(string id, Article article);
    Task DeleteArticleAsync(string id);
    Task<IEnumerable<Article>> GetArticlesBySourceAsync(string source);
    Task<IEnumerable<Article>> GetArticlesByCategoryAsync(string category);
    Task<Article?> GetArticleByUrlAsync(string url);
    Task<(int added, int skipped, List<string> errors)> AddArticlesBatchAsync(IEnumerable<Article> articles);
    Task<Dictionary<string, int>> GetArticleCountsBySourceAsync();
    Task<long> GetTotalArticleCountAsync();
}
