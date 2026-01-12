# :computer: Quick Start Guide

This Quick Start gets the project running locally in five minutes. It covers prerequisites, a minimal setup (backend, frontend, scraper), common troubleshooting, and next steps.

**Repository root:**
- Backend: [backend/NewsAggregator.Api](backend/NewsAggregator.Api)
- Frontend: [frontend](frontend)
- Scraper: [scraper](scraper)

## Prerequisites
- Node.js v18+ (`node --version`)
- .NET 8 SDK (`dotnet --version`)
- Python 3.10+ (`python --version`)
- Azure Cosmos DB Emulator (for local Cosmos DB) or an Azure Cosmos DB account

If you plan to use the emulator on Windows, open the "Azure Cosmos DB Emulator" app and wait until it's fully started.

## Quick Local Setup

Open three terminals, copy and paste the following commands (or use the orchestrator in `scripts/start.ps1`). Run one command block in each terminal.

Terminal 1 — Backend API
```powershell
cd backend/NewsAggregator.Api
dotnet run
```
Expected: "Now listening on: http://localhost:5000" - default backend URL

Terminal 2 — Frontend
```powershell
cd frontend
npm install
npm run dev
```
Expected: local dev URL (e.g. `http://localhost:3000`) - default frontend URL

Terminal 3 — Scraper (one-shot)
```powershell
cd scraper
python -m venv venv
.\venv\Scripts\activate
pip install -r requirements.txt
copy .env.example .env
python main.py
```
The scraper posts articles to the backend API. For continuous scraping, run with `--scheduled` (see below).

Visit the frontend at: `http://localhost:3000`

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

Swagger UI: `http://localhost:5000/swagger`

## Cosmos DB notes
- Container: `Articles`
- Partition key: `/source` (keeps queries by source efficient)

Example document:
```json
{
  "id": "<guid>",
  "title": "...",
  "description": "...",
  "url": "https://...",
  "source": "Example News",
  "category": "World",
  "publishedDate": "2026-01-09T12:00:00Z",
  "scrapedDate": "2026-01-09T12:05:00Z"
}
```

If using the Cosmos DB Emulator, the endpoint is typically `https://localhost:8081`.

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

## Troubleshooting (quick checks)
- Backend not starting: ensure Cosmos DB emulator is running, and port 5000 is free. Check `backend/NewsAggregator.Api/Properties/launchSettings.json`.
- Frontend can't reach backend: confirm backend URL and CORS in `Program.cs`, verify `vite.config.js` proxy.
- Scraper failing: ensure `.env` contains `API_BASE_URL`, virtualenv is active, and backend is reachable.
- No articles: run the scraper and inspect `logs/` and `refresh_response.json`.

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
- Cosmos DB connection: verify the emulator or connection string and endpoint (`https://localhost:8081`).
- Missing Python packages: activate venv and run `pip install -r requirements.txt`.

## Next steps
- Add scrapers in `scraper/scrapers/`.
- Customize UI in `frontend/src/components/`.
- Deploy: consider Azure Static Web Apps + Azure Functions and an Azure Cosmos DB account.

## Where to get help
- Architecture and deeper docs: [README.md](../README.md)
- Add news sources guide: [NewsSources.md](NewsSources.md)
- API docs: [APIDoc.md](APIDoc.md)
