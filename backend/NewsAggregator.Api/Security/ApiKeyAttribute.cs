using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace NewsAggregator.Api.Security;

/// <summary>
/// Requires a valid <c>X-Api-Key</c> header matching the <c>Security:ApiKey</c>
/// configuration value. Fails closed: requests are rejected with 401 when no
/// key is configured, or when the header is missing or incorrect.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ApiKeyAttribute : Attribute, IAsyncActionFilter
{
    public const string HeaderName = "X-Api-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("NewsAggregator.Api.Security.ApiKey");

        var configuredKey = configuration["Security:ApiKey"];

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            logger.LogWarning(
                "Rejected request to {Path}: Security:ApiKey is not configured (failing closed)",
                context.HttpContext.Request.Path);
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var providedKey) ||
            !FixedTimeEquals(configuredKey, providedKey.ToString()))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        await next();
    }

    private static bool FixedTimeEquals(string expected, string provided)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
