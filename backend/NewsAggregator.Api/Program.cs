using NewsAggregator.Api.Models;
using NewsAggregator.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register sentiment analyzer service
builder.Services.AddSingleton<SentimentAnalyzerService>();

// Configure Cosmos DB for local emulator by default
builder.Services.AddSingleton<ICosmosDbService>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();

    var endpoint = configuration["CosmosDb:Endpoint"] ?? "https://localhost:8081";
    var key = configuration["CosmosDb:Key"] ?? "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    var databaseName = configuration["CosmosDb:DatabaseName"] ?? "NewsAggregatorDb";
    var containerName = configuration["CosmosDb:ContainerName"] ?? "Articles";

    return new CosmosDbService(endpoint, key, databaseName, containerName);
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
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapControllers();

// Initialize Cosmos DB
var cosmosDbService = app.Services.GetRequiredService<ICosmosDbService>();
await cosmosDbService.InitializeAsync();

app.Run();
