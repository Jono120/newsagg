# :computer: Quick Start Guide

This Quick Start gets the project running locally in five minutes. It covers prerequisites, a minimal setup (backend, frontend, scraper), common troubleshooting, and next steps.

**Repository root:**
- Backend: [backend/NewsAggregator.Api](backend/NewsAggregator.Api)
- Frontend: [frontend](frontend)
- Scraper: [scraper](scraper)

## Prerequisites
- Node.js v22+ (`node --version`)
- .NET 10 SDK (`dotnet --version`)
- Python 3.13+ (`python --version`)
- PostgreSQL 17+ (or a compatible hosted PostgreSQL instance)

## Quick Local Setup

Open four terminals, copy and paste the following commands (or use the orchestrator in `scripts/start.ps1`). Run one command block in each terminal.

Terminal 1 — PostgreSQL (database)
```powershell
# Start PostgreSQL locally or via Docker and ensure the `newsagg` database exists.
```
Expected: PostgreSQL is running and reachable on the connection string configured in `backend/NewsAggregator.Api/appsettings.json`.

The backend will create the `articles` table automatically on startup.

Terminal 2 — Backend API
```powershell
cd backend/NewsAggregator.Api
dotnet run
```
Expected: "Now listening on: http://localhost:5000" - default backend URL.

Terminal 3 — Frontend
```powershell
cd frontend
npm install
npm run dev
```
Expected: local dev URL (e.g. `http://localhost:3000`) - default frontend URL.

Terminal 4 — Scraper (one-shot)
```powershell
cd scraper
python -m venv venv
.\venv\Scripts\activate
pip install -r requirements.txt
# Copy and configure the environment (already done in root .env, but scraper uses root .env)
python main.py
```
The scraper posts articles to the backend API. Configuration is read from the root `.env` file.

Expected: "Loaded 4 scrapers" and successful API connection to `http://localhost:5000/api/articles`

## TL;DR single-command orchestrator
From the command line you can run the bundled PowerShell orchestrator to start backend, frontend and scraper together:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\start.ps1
```
Logs are written to `logs/`. If there are any issues, they will be added to the logs folder.

## Backend API (important endpoints)
- `GET /api/articles` — list articles
- `GET /api/articles?source={source}` — filter by source
- `GET /api/articles/{id}` — get a single article
- `POST /api/articles` — create article
- `PUT /api/articles/{id}` — update article
- `DELETE /api/articles/{id}` — delete article
- `POST /api/articles/{id}/analyze` — re-run text analytics for one article
- `POST /api/articles/analyze-missing` — batch re-analyze neutral/low-confidence articles

Swagger UI: `http://localhost:5000/swagger`

## PostgreSQL Setup

The backend creates the `articles` table and indexes automatically on startup. Ensure the connection string in `backend/NewsAggregator.Api/appsettings.json` or your environment variables points at a PostgreSQL instance and that the target database exists.

Example article record stored in PostgreSQL:
```json
{
  "id": "abc123def456xyz",
  "title": "...",
  "description": "...",
  "url": "https://...",
  "source": "Example News",
  "category": "World",
  "publishedDate": "2026-01-09 12:00:00.000Z",
  "scrapedDate": "2026-01-09 12:05:00.000Z",
  "sentimentLabel": "neutral",
  "sentimentScore": 0.0,
  "sentimentConfidence": 0.0,
  "positiveWords": [],
  "negativeWords": []
}
```

PostgreSQL configuration in `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "NewsAggregator": "Host=localhost;Port=5432;Database=newsagg;Username=postgres;Password=postgres"
  }
}
```

## Scraper: add a new source
1. Add a new class in `scraper/scrapers/` inheriting `BaseScraper`.
2. Register it in `scraper/main.py` in the `self.scrapers` list.

Example skeleton:
```python
from scrapers.base_scraper import BaseScraper

class MyNewsScraper(BaseScraper):
    def __init__(self):
        super().__init__(source="My News", base_url="https://example.com", category="General")

    def scrape(self):
        # return list of article dicts (title, url, publishedDate, ...)
        return []
```

## Running the scraper in scheduled mode
Run from `scraper`:
```powershell
py -3 main.py --scheduled
# override interval: --interval-minutes 10
```
Environment variables:
- `SCRAPE_INTERVAL_MINUTES` (default `30`)
- `SCRAPE_EXTRACT_CONTENT` (`1` to enable, `0` to disable)
- `ANALYZER_PROVIDER` (`azure` | `huggingface` | `rules`; default `azure`). Selects the text-analytics backend. Azure failures fall back to HuggingFace automatically.
- `AZURE_LANGUAGE_ENDPOINT` (Azure AI Language resource endpoint, used when `ANALYZER_PROVIDER=azure`)
- `AZURE_LANGUAGE_KEY` (Azure AI Language access key)
- `HF_TOKEN` (HuggingFace API token, used when `ANALYZER_PROVIDER=huggingface`)
- `HF_SENTIMENT_MODEL` (HuggingFace classification model for document sentiment)
- `HF_EXTRACTION_MODEL` (HuggingFace token-classification model for key terms)
- `HF_DOC_SENTIMENT_CONFIDENCE_MIN` (default `0.55`; below this, label is neutralized)
- `HF_WORD_SENTIMENT_CONFIDENCE_MIN` (default `0.65`; minimum confidence for term polarity)

The Azure AI Language provider returns document sentiment with opinion mining plus **key phrases** and **named entities**, which are stored on each article (`keyPhrases`, `entities`) and shown in the UI.

## Performance Tuning

### Fast Refresh Mode (Frontend)
For faster scraping refreshes without content extraction or sentiment analysis:
```powershell
# POST to http://localhost:5000/api/scraper/refresh with:
{
  "fastMode": true
}
```
This skips all enrichment work and just fetches raw articles from sources (2-5x faster).

### Incremental Enrichment
Control individual enrichment options via the API:
```powershell
{
  "extractContent": false,      # Skip HTML content extraction
  "analyzerProvider": "rules"   # Use the offline rules analyzer (azure | huggingface | rules)
}
```

### Re-analysing stored articles
Re-run text analytics against the configured provider without re-scraping:
```powershell
# Re-analyse a single article by id
Invoke-WebRequest -Uri "http://localhost:5000/api/articles/{id}/analyze" -Method POST -Headers @{ "X-Api-Key" = "<your-api-key>" }

# Re-analyse neutral / low-confidence articles (optional ?limit=N)
Invoke-WebRequest -Uri "http://localhost:5000/api/articles/analyze-missing?limit=50" -Method POST -Headers @{ "X-Api-Key" = "<your-api-key>" }
```

Notes:
- `POST /api/articles/{id}/analyze` and `POST /api/articles/analyze-missing` require the `X-Api-Key` header.
- `analyze-missing` clamps `limit` to `1-100` (default `50`).

### Optimisations applied
- **Parallel enrichment**: Content extraction and sentiment analysis run on 4 concurrent workers
- **Single Azure call set**: The Azure provider returns sentiment, opinions, key phrases and entities from one resource
- **Fast mode**: `fastMode: true` skips content extraction and uses the offline `rules` analyzer

## Troubleshooting (quick checks)
- Backend not starting: ensure PostgreSQL is running and the connection string is correct. Check port 5000 is free.
- Frontend can't reach backend: confirm backend URL and CORS in `Program.cs`, verify `vite.config.js` proxy.
- **Scraper can't find backend**: Verify `.env` file has `API_BASE_URL=http://localhost:5000` (not `http://backend:8080`). The backend URL must match where the API is actually running.
  - Local dev: `http://localhost:5000`
  - Docker deployment: `http://backend:8080`
- Scraper failing: ensure `.env` contains `API_BASE_URL`, virtualenv is active, and backend is reachable at that URL.
- No articles: run the scraper and inspect `logs/` and `refresh_response.json`. Check backend logs for API errors.

## Testing the API (PowerShell)
Get all articles:
```powershell
Invoke-WebRequest -Uri "http://localhost:5000/api/articles" -Method GET
```

Create a test article:
```powershell
$body = @{
  title = "Test Article"
  description = "This is a test"
  url = "https://example.com"
  source = "Test Source"
  category = "General"
  publishedDate = (Get-Date).ToString("o")
} | ConvertTo-Json

Invoke-WebRequest -Uri "http://localhost:5000/api/articles" -Method POST -Body $body -ContentType "application/json"
```

## Common issues
- Port conflicts: change backend port in `backend/NewsAggregator.Api/Properties/launchSettings.json` or frontend port in `frontend/vite.config.js`.
- PostgreSQL not running: start your local PostgreSQL server or container and verify the configured database is reachable.
- Missing Python packages: activate venv and run `pip install -r requirements.txt`.

## Next steps
- Add scrapers in `scraper/scrapers/`.
- Customize UI in `frontend/src/components/`.
- Deploy: run PostgreSQL as a persistent service alongside the .NET API.
- Production operations and rollback cadence: [Production-Runbook.md](./Production-Runbook.md)

## Where to get help
- Architecture and deeper docs: [README.md](../README.md)
- Add news sources guide: [NewsSources.md](NewsSources.md)
- API docs: [APIDoc.md](APIDoc.md)
