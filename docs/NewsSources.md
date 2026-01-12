# Adding New News Sources to the Aggregator

## Overview

The News Aggregator uses a template-based system for adding new sources. All scrapers inherit from `base_scraper.py` which provides common functionality and patterns.

## Quick Start - 3 Steps

### Step 1: Create Your Scraper File

Create a new file in `scraper/scrapers/` directory (e.g., `rnz_scraper.py`):

```python
import feedparser
import logging
from typing import List
from datetime import datetime
from models.article import Article
from scrapers.base_scraper import BaseScraper

logger = logging.getLogger(__name__)

class NewsSourceScraper(BaseScraper):
    def __init__(self):
        super().__init__(
            source="YOUR_CHOSEN_NEWS",
            base_url="https://www.example.com/rss/news",
            category="General"
        )
    
    def scrape(self) -> List[Article]:
        articles = []
        try:
            feed = feedparser.parse(self.base_url)
            
            for entry in feed.entries[:self.max_articles]:
                try:
                    title = entry.get('title', '').strip()
                    url = entry.get('link', '').strip()
                    
                    if not self._is_valid_article(title, url):
                        continue
                    
                    description = entry.get('summary', title).strip()
                    pub_date = None
                    if hasattr(entry, 'published_parsed') and entry.published_parsed:
                        pub_date = self._parse_iso_date(entry.published_parsed)
                    
                    article = self._create_article(
                        title=title,
                        description=description,
                        url=url,
                        published_date=pub_date
                    )
                    articles.append(article)
                    
                except Exception as e:
                    logger.warning(f"Error parsing entry: {e}")
                    continue
            
            logger.info(f"Scraped {len(articles)} articles from {self.source}")
            
        except Exception as e:
            logger.error(f"Error scraping {self.source}: {e}")
        
        return articles
```

### Step 2: Register Your Scraper

Add your scraper to `scraper/scrapers/__init__.py`:

```python
from scrapers.YOUR_CHOSEN_NEWS import YOUR_CHOSEN_NEWS

SCRAPER_REGISTRY = [
    StuffScraper,
    RNZScraper,
    OneNewsScraper,
    NZHeraldScraper,
    YOUR_CHOSEN_NEWS,  # Add your new scraper here
]
```

### Step 3: Test

Run the scraper:

```bash
cd scraper
python main.py
```

The scraper will automatically be loaded and executed!

---

## Detailed Guide

### 1. Choosing Your Scraping Method

#### Option A: RSS/ATOM Feed (RECOMMENDED :star:)

**Best for:** Most major news sources  
**Difficulty:** Easy  
**Reliability:** High

**How to find RSS feed:**
- Look for RSS icon on news site
- Try `domain.com/rss` or `domain.com/feed`
- Check `https://www.rssboard.org/feeds` for known feeds
- Use services like `RSS Feed Finder` or similar browser extension

**Example:**

```python
import feedparser

class NewsSourceScraper(BaseScraper):
    def __init__(self):
        super().__init__(
            source="News Source",
            base_url="https://example.com/rss"  # RSS feed URL
        )
    
    def scrape(self) -> List[Article]:
        articles = []
        try:
            feed = feedparser.parse(self.base_url)
            
            for entry in feed.entries[:self.max_articles]:
                title = entry.get('title', '').strip()
                url = entry.get('link', '').strip()
                description = entry.get('summary', title).strip()
                
                if self._is_valid_article(title, url):
                    pub_date = None
                    if hasattr(entry, 'published_parsed'):
                        pub_date = self._parse_iso_date(entry.published_parsed)
                    
                    articles.append(self._create_article(
                        title, description, url, pub_date
                    ))
        except Exception as e:
            logger.error(f"Error scraping {self.source}: {e}")
        return articles
```

#### Option B: HTML Scraping

**Best for:** Sites without RSS feeds  
**Difficulty:** Medium  
**Reliability:** Medium (can be problematic if site changes structure)

**Tools needed:** `requests`, `beautifulsoup4`

**Example:**

```python
import requests
from bs4 import BeautifulSoup

class HTMLNewsScraper(BaseScraper):
    def __init__(self):
        super().__init__(
            source="HTML News Site",
            base_url="https://example.com/news"
        )
    
    def scrape(self) -> List[Article]:
        articles = []
        try:
            response = requests.get(self.base_url, timeout=self.timeout)
            response.raise_for_status()
            soup = BeautifulSoup(response.content, 'html.parser')
            
            # Find article containers (inspect the HTML to find selector)
            article_elements = soup.select('div.article')[:self.max_articles]
            
            for elem in article_elements:
                title_elem = elem.select_one('h2.title')
                url_elem = elem.select_one('a.link')
                desc_elem = elem.select_one('p.summary')
                
                if not title_elem or not url_elem:
                    continue
                
                title = title_elem.get_text().strip()
                url = url_elem.get('href', '').strip()
                description = desc_elem.get_text().strip() if desc_elem else ""
                
                if self._is_valid_article(title, url):
                    articles.append(self._create_article(title, description, url))
        
        except Exception as e:
            logger.error(f"Error scraping {self.source}: {e}")
        return articles
```

**How to find CSS selectors:**
1. Open site in browser
2. Right-click on an article → "Inspect"
3. Find the HTML structure
4. Use CSS selectors (e.g., `div.article-card`, `h2.headline`, `a.article-link`)

#### Option C: JSON API

**Best for:** Sites with public APIs  
**Difficulty:** Harder to implement  
**Reliability:** High

**Tools needed:** `requests`

**Important:** Check API terms of service and rate limits!

**Example:**

```python
import requests

class APINewsScraper(BaseScraper):
    def __init__(self):
        super().__init__(
            source="API News Source",
            base_url="https://api.example.com/v1/articles"
        )
    
    def scrape(self) -> List[Article]:
        articles = []
        try:
            # Add headers and parameters as needed
            headers = {'User-Agent': 'NewsAggregator/1.0'}
            params = {'limit': self.max_articles, 'sort': 'date'}
            
            response = requests.get(
                self.base_url,
                headers=headers,
                params=params,
                timeout=self.timeout
            )
            response.raise_for_status()
            data = response.json()
            
            # Adjust based on API response structure
            items = data.get('articles', [])
            
            for item in items[:self.max_articles]:
                title = item.get('title', '').strip()
                url = item.get('url', '').strip()
                
                if self._is_valid_article(title, url):
                    description = item.get('content', item.get('description', ''))
                    pub_date = self._parse_iso_date(item.get('published_at'))
                    
                    articles.append(self._create_article(
                        title, description, url, pub_date
                    ))
        
        except Exception as e:
            logger.error(f"Error scraping {self.source}: {e}")
        return articles
```

### 2. Required Methods

All scrapers must implement:

```python
class YourScraper(BaseScraper):
    def __init__(self):
        super().__init__(
            source="Source Name",        # Displayed in app
            base_url="https://...",      # Primary URL or RSS feed
            category="General"           # Default category
        )
    
    def scrape(self) -> List[Article]:
        """Return list of Article objects"""
        pass
```

### 3. Available Helper Methods

From `BaseScraper`:

```python
# Create an Article object
article = self._create_article(
    title="Article Title",
    description="Article summary",
    url="https://...",
    published_date="2024-01-09T10:30:00",  # ISO format
    category="Article Category"  # Optional, uses self.category if not provided
)

# Validate article before adding
if self._is_valid_article(title, url):
    # ... process article

# Parse dates to ISO format
iso_date = self._parse_iso_date(date_tuple_or_datetime)
# Handles: tuples, datetime objects, ISO strings
```

### 4. Configuration Options

```python
super().__init__(
    source="News Source",           # Name for display
    base_url="https://...",         # URL or RSS feed
    category="General",             # Default category (optional)
    max_articles=20,                # Max articles per run (optional)
    timeout=10                      # Request timeout seconds (optional)
)
```

### 5. Best Practices

:bell: **Do:**
- Check `robots.txt` and terms of service
- Use RSS feeds when available (faster, more reliable)
- Handle errors gracefully with try/except
- Log important events with logger
- Use `self._is_valid_article()` to validate data
- Set reasonable timeouts
- Limit articles to avoid overloading

:no_bell: **Don't:**
- Ignore robots.txt or scraping restrictions
- Make requests in tight loops without delays
- Parse HTML with regex (use BeautifulSoup)
- Bypass authentication or rate limits
- Scrape data you don't have permission to use

### 6. Testing Your Scraper

```bash
cd scraper

# Test one scraper directly
python -c "from scrapers.bbc_scraper import BBCScraper; s = BBCScraper(); print(len(s.scrape()))"

# Run all scrapers
python main.py

# Check logs for errors
# Look for: [ERROR] or [WARNING] messages
```

### 7. Debugging Tips

**Issue: No articles found**
- Check URL is accessible (paste in browser)
- Verify RSS feed format with: `feedparser.parse(url).entries`
- Check HTML selectors with browser inspect tool

**Issue: Articles not being saved**
- Check for errors in batch import response
- Verify URL is not already in database
- Check article has required fields (title, url)

**Issue: Scraper too slow**
- Increase `max_articles` limit carefully
- Add delays between requests if many URLs
- Use async requests for multiple feeds

**Issue: Scraper frequently breaks**
- HTML scrapers are fragile; consider finding RSS feed
- Monitor site changes, adjust selectors when needed
- Use API if available

### 8. Common Scraper Patterns

**Category Detection from URL:**
```python
def _determine_category(self, url: str) -> str:
    if '/technology/' in url:
        return 'Technology'
    elif '/business/' in url:
        return 'Business'
    elif '/sports/' in url:
        return 'Sport'
    return self.category
```

**HTML Cleanup:**
```python
from bs4 import BeautifulSoup

description = entry.get('summary', '')
if '<' in description:
    description = BeautifulSoup(description, 'html.parser').get_text(strip=True)
```

**Request Headers for HTML Scraping:**
```python
headers = {
    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'
}
response = requests.get(url, headers=headers, timeout=self.timeout)
```

---

## File Structure

```
scraper/
├── main.py                  # Entry point (auto-discovers scrapers)
├── scrapers/
│   ├── __init__.py          # Registry of all scrapers
│   ├── base_scraper.py      # Base class (don't modify)
│   ├── template_scraper.py  # Template with examples
│   ├── stuff_scraper.py     # Example: Stuff NZ
│   ├── rnz_scraper.py       # Example: RNZ
│   └── your_scraper.py      # Your new scraper here
├── models/
│   └── article.py           # Article data model
└── services/
    └── article_service.py   # API communication
```

---

## Summary

1. **Find a data source** (RSS feed preferred)
2. **Copy template_scraper.py** or use examples above
3. **Implement scrape() method**
4. **Register in scrapers/__init__.py**
5. **Test with python main.py**
6. **Done!** Scraper auto-discovered and runs with others

For questions, check template_scraper.py or existing scrapers (stuff_scraper.py, rnz_scraper.py, onenews_scraper.py) for reference implementations.
