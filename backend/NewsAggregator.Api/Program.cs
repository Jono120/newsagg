using NewsAggregator.Api.Models;
using NewsAggregator.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

// Configure PostgreSQL as the article storage backend
builder.Services.AddSingleton<IArticleService, PostgresArticleService>();

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

// Initialize the article service and ensure the PostgreSQL schema exists.
var articleService = app.Services.GetRequiredService<IArticleService>();
await articleService.InitializeAsync();

app.Run();
