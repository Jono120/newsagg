# :new_zealand: News Aggregator - sources for Aotearoa New Zealand

This was changed to a PostgresSQL database over a pocketbase for ease of management in the database and deployment.

A full-stack news aggregation platform that pulls articles from 4 Aotearoa New Zealand news sources, stores them in PostgreSQL, and displays them in a modern React interface.

## :rocket: Quick Start

See the [quickstart guide](./docs/Quickstart.md) for full setup and installation instructions.

For the containerized stack, run `scripts/start-compose.ps1`. For Azure, follow [Azure full-stack setup](./docs/Azure-Setup.md) (GitHub Actions or `scripts/deploy-azure.ps1`).

## :clipboard: Features

- :scroll: **4 Active Scrapers** extracting 80 articles (20 from each source)
- :sparkles: **RSS + HTML Parsing** for reliable article extraction
- :oncoming_automobile: **Auto-Discovery System** for easy scraper registration
- :symbols: **Template System** for adding new sources quickly
- :bullettrain_front: **React Frontend** with modern UI
- :notebook_with_decorative_cover: **ASP.NET Core Backend** with REST API
- :floppy_disk: **PostgreSQL** for persistent relational storage

## :newspaper: News Sources

| Source | Type | Articles | Status |
|--------|------|----------|--------|
| Stuff NZ | RSS | 20+ | :open_file_folder: Working |
| RNZ | RSS | 20+ | :open_file_folder: Working |
| 1News NZ | RSS | 20+ | :open_file_folder: Working |
| NZ Herald | HTML (Semantic) | 20+ | :open_file_folder: Working |

## :office: Architecture

- **Frontend**: React + Vite (Azure: `newsagg-ui` App Service + ACR)
- **Backend**: ASP.NET Core 10 Web API
- **Scraper**: Python 3.13+ with BeautifulSoup & feedparser
- **Database**: PostgreSQL 17

## :file_folder: Project Structure


```
newsagg/
├── frontend/                     # React frontend
│   └── src/                      # React components & styles
├── backend/                      # .NET Web API
│   └── NewsAggregator.Api/
│       ├── Controllers/          # API endpoints
│       ├── Models/               # Data models
│       └── Services/             # Business logic
├── scraper/                      # Python web scraper
│   ├── scrapers/                 # 4 individual scrapers
│   ├── scrapers/base_scraper.py  # Base template class
│   ├── services/                 # API communication
│   └── debug/                    # Test & debug utilities
└── docs/                         # Documentation
  ├── QUICKSTART.md               # Setup guide
  ├── ADDING_NEWS_SOURCES.md      # Add new scrapers
  └── API_DOCUMENTATION.md        # API reference
```