# Frontend Integration

## Feature Overview

The frontend's **Refresh** button now triggers the Python scraper to pull the latest articles from all news sources directly from the UI.

## How It Works

### User Flow

1. User clicks the **"⟳ Refresh"** button in the header
2. Frontend sends POST request to backend: `/api/scraper/refresh`
3. Backend spawns Python scraper process in the background
4. Frontend displays "Scraper started!" message
5. After 2 seconds, frontend fetches updated articles
6. Success message appears: "Articles updated successfully!"

### Technical Architecture

**Frontend (React):**
- `App.jsx` - `handleRefresh()` function
- Sends POST request to `/api/scraper/refresh`
- Displays status messages during the process
- Auto-refreshes articles after scraper completes

**Backend (.NET):**
- `ScraperController.cs` - New controller with 2 endpoints:
  - `POST /api/scraper/refresh` - Triggers scraper
  - `GET /api/scraper/status` - Checks if scraper is running

**Configuration:**
- `appsettings.json` - Path to Python scraper script

## New Files/Changes

### Created
- `backend/NewsAggregator.Api/Controllers/ScraperController.cs`

### Modified
- `frontend/src/App.jsx` - Added refresh logic & UI state
- `frontend/src/index.css` - Added refresh message styling
- `backend/NewsAggregator.Api/appsettings.json` - Added scraper config

## API Endpoints

### POST /api/scraper/refresh

**Request:**
```http
POST http://localhost:5000/api/scraper/refresh
```

**Response (Success):**
```json
{
  "message": "Scraper refresh started",
  "status": "processing",
  "details": "Articles will be updated in the background. Please refresh the page in a moment."
}
```

**Response (Error):**
```json
{
  "error": "Failed to start scraper",
  "details": "error message here"
}
```

### GET /api/scraper/status

**Request:**
```http
GET http://localhost:5000/api/scraper/status
```

**Response:**
```json
{
  "status": "idle",
  "processes": 0,
  "message": "Scraper is idle"
}
```

## UI Feedback

The refresh button provides visual feedback through:

1. **Button States:**
   - Normal: "⟳ Refresh"
   - Loading: "⟳ Loading..."
   - Scraping: "⟳ Scraping..."

2. **Status Messages:**
   - **Info (Blue):** "Scraper started! Articles will update in a moment..."
   - **Success (Green):** "Articles updated successfully!"
   - **Error (Red):** "Failed to refresh articles. Make sure the scraper is available."

## Features

:white_check_mark: **Async Processing** - Scraper runs in background, doesn't block UI  
:white_check_mark: **Status Feedback** - User sees what's happening  
:white_check_mark: **Auto-Refresh** - Articles update automatically after scraper completes  
:white_check_mark: **Error Handling** - Graceful failure messages  
:white_check_mark: **Process Detection** - Can check if scraper is currently running  

## How to Use

### For End Users
1. Open the News Aggregator in browser
2. Click the **"⟳ Refresh"** button in the top right
3. Wait for the success message
4. Articles list updates with latest content

### For Developers
Test the endpoint manually:
```bash
# Trigger scraper refresh
curl -X POST http://localhost:5000/api/scraper/refresh

# Check scraper status
curl http://localhost:5000/api/scraper/status
```

## Configuration

Edit `appsettings.json` to change the scraper script path:
```json
"Scraper": {
  "PythonScriptPath": "../../scraper/main.py"
}
```

## Logging

Backend logs scraper activity:
```
[INFO] Scraper refresh requested from frontend
[INFO] Scraper output: Scraped 80 articles...
[INFO] Scraper process completed
```

## Requirements

- Python must be in system PATH
- Scraper script must exist at configured path
- Backend must be running
- Cosmos DB must be accessible for both API and scraper

## Troubleshooting

**"Failed to refresh articles"**
- Ensure Python is installed and in PATH
- Check if scraper path is correct in appsettings.json
- Verify Cosmos DB is running

**Articles not updating**
- Wait a moment - scraper takes ~17-20 seconds
- Check backend logs for errors
- Manually verify scraper works: `cd scraper && python main.py`

**"Scraper script not found"**
- Verify file exists at configured path
- Check path is relative to backend directory
