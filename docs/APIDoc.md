# News Aggregator API Documentation

## Overview
The News Aggregator API provides endpoints for managing news articles from multiple RSS feed sources. Built with ASP.NET Core 8.0 and Azure Cosmos DB.

It connects with a python script that will connect and pull in the latest items from a news site. It'll then upload a link and basic information of this to the Cosmos DB table.

## Base Development URL
```
http://localhost:5000/api
```

## Features
:white_check_mark: **Duplicate Detection** - Prevents adding the same article twice (by URL)  
:white_check_mark: **Batch Import** - Efficient bulk article creation  
:white_check_mark: **Source Filtering** - Query articles by news source  
:white_check_mark: **Category Filtering** - Query articles by category  
:white_check_mark: **Statistics** - Get article counts and source information  
:white_check_mark: **Health Checks** - Monitor API and database status  

---

## Endpoints

### Articles

#### GET /api/articles
Get all articles or filter by source/category.

**Query Parameters:**
- `source` (optional) - Filter by source (e.g., "Stuff NZ", "RNZ", "1News NZ")
- `category` (optional) - Filter by category (e.g., "NZ News", "World", "Sport")

**Response:** `200 OK`
```json
[
  {
    "id": "abc123",
    "title": "Breaking News Story",
    "description": "Article description here",
    "url": "https://www.example.co.nz/article/123",
    "source": "Example NZ",
    "category": "NZ News",
    "publishedDate": "2026-01-09T10:00:00Z",
    "scrapedDate": "2026-01-09T10:05:00Z"
  }
]
```

**Local Development Examples:**
```bash
# Get all articles
curl http://localhost:5000/api/articles

# Get articles from specific source
curl "http://localhost:5000/api/articles?source=RNZ"

# Get articles by category
curl "http://localhost:5000/api/articles?category=Sport"
```

---

#### GET /api/articles/{id}
Get a specific article by ID.

**Response:** `200 OK` or `404 Not Found`
```json
{
  "id": "abc123",
  "title": "Breaking News Story",
  "description": "Article description here",
  "url": "https://www.example.co.nz/article/123",
  "source": "Example NZ",
  "category": "NZ News",
  "publishedDate": "2026-01-09T10:00:00Z",
  "scrapedDate": "2026-01-09T10:05:00Z"
}
```

---

#### POST /api/articles
Create a single article.

**Request Body:**
```json
{
  "title": "Breaking News Story",
  "description": "Article description",
  "url": "https://www.example.co.nz/article/",
  "source": "Example NZ",
  "category": "NZ News",
  "publishedDate": "2026-01-09T10:00:00Z"
}
```

**Response:** `201 Created` or `409 Conflict` (if duplicate)
```json
{
  "id": "abc123",
  "title": "Breaking News Story",
  ...
}
```

**Duplicate Response:** `409 Conflict`
```json
{
  "message": "Article with this URL already exists",
  "existingId": "xyz789"
}
```

---

#### POST /api/articles/batch
Create multiple articles in a single request. **Recommended for scrapers.**

**Request Body:**
```json
[
  {
    "title": "Article 1",
    "description": "Description 1",
    "url": "https://example.com/1",
    "source": "TNZ",
    "category": "National"
  },
  {
    "title": "Article 2",
    "description": "Description 2",
    "url": "https://example.com/2",
    "source": "TNZ",
    "category": "World"
  }
]
```

**Response:** `200 OK`
```json
{
  "totalReceived": 20,
  "added": 15,
  "skipped": 5,
  "errors": null
}
```

**Benefits:**
- Automatic duplicate detection (skips duplicates by URL)
- Single network request for multiple articles
- Returns detailed statistics
- Better performance than individual POST requests

---

#### PUT /api/articles/{id}
Update an existing article.

**Request Body:** (same as POST)

**Response:** `204 No Content` or `404 Not Found`

---

#### DELETE /api/articles/{id}
Delete an article.

**Response:** `204 No Content` or `404 Not Found`

---

### Statistics

#### GET /api/statistics
Get overall statistics about articles.

**Response:** `200 OK`
```json
{
  "totalArticles": 60,
  "articlesBySource": {
    "Stuff NZ": 20,
    "RNZ": 20,
    "1News NZ": 20,
    "NZ Hearld": 20
  },
  "sources": ["Stuff NZ", "RNZ", "1News NZ", "NZ Hearld"],
  "timestamp": "2026-01-09T10:30:00Z"
}
```

---

#### GET /api/statistics/sources
Get list of sources with article counts.

**Response:** `200 OK`
```json
[
  {
    "name": "Stuff NZ",
    "articleCount": 20
  },
  {
    "name": "RNZ",
    "articleCount": 20
  },
  {
    "name": "1News NZ",
    "articleCount": 20
  },
  {
    "name": "NZ Hearld",
    "articleCount": 20
  }
]
```

---

### Health

#### GET /api/health
Check API and database health.

**Response:** `200 OK` (healthy) or `503 Service Unavailable` (unhealthy)
```json
{
  "status": "healthy",
  "timestamp": "2026-01-09T10:30:00Z",
  "database": {
    "status": "connected",
    "articleCount": 60
  },
  "version": "1.0.0"
}
```

---

#### GET /api/health/ping
Simple ping endpoint for quick health check.

**Response:** `200 OK`
```json
{
  "status": "pong",
  "timestamp": "2026-01-09T10:30:00Z"
}
```

---

## Error Responses

### 400 Bad Request
Invalid request data.
```json
{
  "message": "No articles provided"
}
```

### 404 Not Found
Resource not found.

### 409 Conflict
Duplicate resource (article with same URL already exists).
```json
{
  "message": "Article with this URL already exists",
  "existingId": "abc123"
}
```

### 500 Internal Server Error
Server error occurred.
```json
"An error occurred while creating the article"
```

### 503 Service Unavailable
Service is unhealthy (typically database connection issue).

---

## Development Usage Examples

### Development Usage of cURL

```bash
# Get all articles
curl http://localhost:5000/api/articles

# Get statistics
curl http://localhost:5000/api/statistics

# Create single article
curl -X POST http://localhost:5000/api/articles \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Test Article",
    "description": "Test Description",
    "url": "https://test.com/article",
    "source": "Test Source",
    "category": "Test"
  }'

# Batch import
curl -X POST http://localhost:5000/api/articles/batch \
  -H "Content-Type: application/json" \
  -d '[
    {"title": "Article 1", "url": "https://test.com/1", "source": "Test", "description": "Desc 1"},
    {"title": "Article 2", "url": "https://test.com/2", "source": "Test", "description": "Desc 2"}
  ]'

# Health check
curl http://localhost:5000/api/health
```

### Using Python (scraper)

```python
import requests

# Batch import (recommended)
articles = [
    {
        "title": "Article 1",
        "url": "https://example.com/1",
        "source": "TNZ",
        "category": "National",
        "description": "Description 1"
    },
    # ... more articles
]

response = requests.post(
    "http://localhost:5000/api/articles/batch",
    json=articles,
    timeout=30
)

result = response.json()
print(f"Added: {result['added']}, Skipped: {result['skipped']}")
```

### Using JavaScript (React)

```javascript
// Fetch articles
const response = await fetch('http://localhost:5000/api/articles');
const articles = await response.json();

// Fetch articles by source
const stuffArticles = await fetch('http://localhost:5000/api/articles?source=Stuff%20NZ');
const data = await stuffArticles.json();

// Get statistics
const stats = await fetch('http://localhost:5000/api/statistics');
const statistics = await stats.json();
```

---

## Database Schema

### Article Model
```csharp
{
  "id": string,              // Unique identifier (GUID)
  "title": string,           // Article headline
  "description": string,     // Article summary
  "url": string,            // Article URL (must be unique)
  "source": string,         // News source (partition key)
  "category": string,       // Article category
  "publishedDate": DateTime, // When article was published
  "scrapedDate": DateTime   // When article was scraped
}
```

### Partition Key Strategy
- **Partition Key**: `/source`
- **Benefits**: 
  - Efficient queries by source
  - Good distribution across partitions
  - Supports multi-region writes

---

## Performance Considerations

### Batch Import vs Individual Posts
- **Batch Import**: 1 request for 60 articles (~2 seconds)
- **Individual Posts**: 60 requests for 60 articles (~2 minutes)

**Recommendation**: Always use `/api/articles/batch` for scraper operations.

### Query Performance
- **By Source**: Fast (single partition query)
- **By Category**: Slower (cross-partition query)
- **All Articles**: Slowest (full scan)

### Rate Limits
- Local development: No limits

---

## Integration & Scraper

### Scraper Refresh Endpoint
The API exposes a convenience endpoint that will start the repository's scraper process in the background:

- POST `/api/scraper/refresh` — triggers `scraper/main.py` to run and import results via `/api/articles/batch`.
- GET `/api/scraper/status` — returns whether a `python` process appears to be running on the host and a small status payload.

When running locally in Development, the API will look for a configured script path under `Scraper:PythonScriptPath` in `appsettings.Development.json`. If not configured, the controller will attempt to run `../../scraper/main.py` (two levels up) relative to the API content root. To ensure the API can find the repository scraper, add this to `backend/NewsAggregator.Api/appsettings.Development.json`:

```json
"Scraper": {
  "PythonScriptPath": "../../scraper/main.py"
}
```

Replace the path with your repository location or an absolute path on your machine. The orchestrator script (see below) sets this when running inside the repo.

### Orchestrator script (developer convenience)
There is a PowerShell helper script at `scripts/start_and_check.ps1` that will:

- create a `logs/` folder if missing
- start the backend (`dotnet run`) and capture `logs/backend.log` and `logs/backend.err`
- start the frontend (`npm run dev`) to `logs/frontend.log` and `logs/frontend.err` (on Windows it prefers `npm.cmd`/`npx.cmd`)
- wait for the backend and frontend to respond
- POST `/api/scraper/refresh` and save the response to `logs/refresh_response.json`
- optionally start the scraper directly and capture `logs/scraper.log` and `logs/scraper.err`

Run it from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\start_and_check.ps1
```

Logs are written under `logs/` in the repository root. Use these when diagnosing issues with the scraper, backend, or frontend.

### Notes on behavior
- The scraper posts batches to `/api/articles/batch`; the API performs duplicate detection and returns an object with `added`, `skipped`, and `errors` fields.
- When running the orchestrator the backend will log scraper stdout/stderr; scraper logs are also captured under `logs/scraper.*` for easier debugging.
- Production: Configure based on Cosmos DB throughput (RU/s)

---

## Swagger Documentation

When running in development mode, interactive API documentation is available at:
```
http://localhost:5000/swagger
```

Features:
- Try out endpoints directly from the browser
- View request/response schemas
- Test authentication (if configured)

---

## Development Configuration

### appsettings.json
```json
{
  "CosmosDb": {
    "Endpoint": "https://localhost:8081",
    "Key": "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
    "DatabaseName": "NewsAggregatorDb",
    "ContainerName": "Articles"
  }
}
```

### Environment Variables (Python Scraper)
```env
API_BASE_URL=http://localhost:5000
API_ARTICLES_ENDPOINT=/api/articles
SCRAPE_INTERVAL_MINUTES=30
```

---

## Next Steps

1. **Start the API**: `dotnet run`
2. **Run the scraper**: `python main.py`
3. **View in Swagger**: http://localhost:5000/swagger
4. **Check health**: http://localhost:5000/api/health
5. **View statistics**: http://localhost:5000/api/statistics

See the [quickstart guide](QUICKSTART.md) for complete setup instructions.
