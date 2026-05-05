using NewsAggregator.Api.Models;
using NewsAggregator.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

// Register the named HttpClient for PocketBase with the configured base URL
var pocketBaseUrl = (builder.Configuration["PocketBase:BaseUrl"] ?? "http://localhost:8090").TrimEnd('/') + "/";
builder.Services.AddHttpClient("pocketbase", client =>
{
    client.BaseAddress = new Uri(pocketBaseUrl);
});

// Configure PocketBase as the article storage backend
builder.Services.AddSingleton<IArticleService>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<PocketBaseService>>();
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();

    var collectionName = configuration["PocketBase:CollectionName"] ?? "articles";

    return new PocketBaseService(collectionName, httpClientFactory, logger, configuration);
});

// Configure CORS for local development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
            .WithOrigins("http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    //app.UseSwagger();
    //app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapControllers();

// Initialize the article service (no-op for PocketBase; logs startup confirmation)
var articleService = app.Services.GetRequiredService<IArticleService>();
await articleService.InitializeAsync();

app.Run();
