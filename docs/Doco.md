# Documentation Guide

A clean overview of all project documentation.

## :rocket: Getting Started

1. **[Quick Start Guide](Quickstart.md)** :star: **START HERE**
   - 5-minute setup guide
   - Prerequisites checklist
   - Daily development workflow

2. **[Read Me File](../README.md)**
   - Project overview and architecture
   - Feature list
   - Prerequisites and setup

## :book: Working with Scrapers

3. **[New Sources](NewsSources.md)** 
   - Template system for new scrapers
   - BaseScraper class reference
   - Auto-discovery registry
   - 3 complete working examples (RSS, HTML, JSON API)
   - 400+ lines of comprehensive guidance

## :bone: Backend & API

4. **[Backend APIs and Information](APIDoc.md)**
   - REST API endpoints
   - Query parameters
   - Request/response examples
   - Error codes

---

## :bar_chart: Summary

| Document | Purpose | Read Time |
|----------|---------|-----------|
| Quickstart | Setup in 5 mins | 3 min |
| README | Overview & architecture | 5 min |
| News Sources | Add new sources | 10 min |
| APIDoc| Backend API reference | 5 min |

---

## :test_tube: Testing

### Integration Tests
- **Scraper Connectivity Tests** - Validates live connectivity to all news sources (Stuff NZ, RNZ, 1News NZ, NZ Herald)
  - Confirms each scraper successfully returns articles
  - Verifies article parsing (title, URL, description extraction)
  - Tests timeout handling and error resilience

### Date/Timezone Tests
- **New Zealand Timezone Verification** - Ensures all scraped and published dates are correctly formatted in NZ timezone (+12:00)
  - Validates `publishedDate` and `scrapedDate` fields in ISO 8601 format with timezone offset
  - Confirms backend will parse `DateTimeOffset` strings correctly

### Test Files
- `scraper/services/tests/test_sentiment_analyzer.py` - Unit tests for sentiment analysis module

