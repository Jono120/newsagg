"""
TEMPLATE SCRAPER - Use this as a starting point for new sources

To create a new scraper:
1. Copy this file and rename it (e.g., bbc_scraper.py)
2. Replace TemplateNewsScraper with your class name (e.g., BBCScraper)
3. Update the __init__ method with your source details
4. Implement the scrape() method with your logic
5. Import and add it to ScraperOrchestrator in main.py

Examples for different feed types:
- RSS Feed: Use feedparser library
- HTML Scraping: Use requests + BeautifulSoup
- JSON API: Use requests and parse JSON
"""

import logging
from typing import List
from datetime import datetime
from models.article import Article
from scrapers.base_scraper import BaseScraper

logger = logging.getLogger(__name__)


class TemplateNewsScraper(BaseScraper):
    """
    Template for adding new news sources.
    
    This scraper shows the standard pattern for implementing a new source.
    Choose your scraping method based on the source's available data:
    
    1. RSS Feeds (easiest): Most major news sites provide RSS feeds
    2. HTML Scraping: Sites without RSS might expose content via HTML
    3. JSON APIs: Some sites have public APIs (check docs and rate limits)
    
    Always check:
    - robots.txt and terms of service
    - Rate limits and robots meta tags
    - Whether RSS feed exists first (easier and more reliable)
    """
    
    def __init__(self):
        super().__init__(
            source="Template News",              # Name displayed in the app
            base_url="https://example.com/rss",  # Primary URL or RSS feed
            category="General",                   # Default category
            max_articles=20,                      # Max articles per run
            timeout=10                            # Request timeout
        )
    
    def scrape(self) -> List[Article]:
        """
        Main scraping method. Must return List[Article].
        
        Pattern:
        1. Fetch data from self.base_url
        2. Parse the response
        3. Extract articles
        4. Create Article objects
        5. Return list
        """
        articles = []
        
        try:
            logger.info(f"Starting scrape from {self.source}...")
            
            # STEP 1: Fetch data
            # For RSS: import feedparser; feed = feedparser.parse(self.base_url)
            # For HTML: import requests; response = requests.get(self.base_url, timeout=self.timeout)
            # For JSON: response = requests.get(api_url, timeout=self.timeout); data = response.json()
            
            # STEP 2: Parse and extract articles
            # This varies by source type - see examples below
            
            # STEP 3: Process articles (example loop)
            # for item in data_items[:self.max_articles]:
            #     article = self._extract_article(item)
            #     if article:
            #         articles.append(article)
            
            logger.info(f"Scraped {len(articles)} articles from {self.source}")
            
        except Exception as e:
            logger.error(f"Error scraping {self.source}: {str(e)}")
        
        return articles
    
    def _extract_article(self, item) -> Article or None:
        """
        Extract a single article from a data item.
        
        Returns:
            Article: Article object or None if extraction fails
        """
        try:
            # Extract fields - adjust based on your source's data structure
            title = item.get('title', '').strip()
            description = item.get('description', item.get('summary', title)).strip()
            url = item.get('link', item.get('url', '')).strip()
            published_date = self._parse_published_date(item)
            
            # Validate
            if not self._is_valid_article(title, url):
                return None
            
            # Determine category from URL or content
            category = self._determine_category(url, title)
            
            # Create and return article
            return self._create_article(
                title=title,
                description=description,
                url=url,
                published_date=published_date,
                category=category
            )
        
        except Exception as e:
            logger.warning(f"Error extracting article: {str(e)}")
            return None
    
    def _parse_published_date(self, item) -> str or None:
        """Parse publication date from item. Adjust for your source's format."""
        try:
            # Try common date field names
            for date_field in ['published', 'published_parsed', 'pubDate', 'date', 'timestamp']:
                if date_field in item:
                    return self._parse_iso_date(item[date_field])
            return None
        except:
            return None
    
    def _determine_category(self, url: str, title: str) -> str:
        """
        Determine article category from URL or title.
        
        Override in subclass with source-specific logic.
        """
        # Example: Check URL path
        # if '/technology/' in url:
        #     return 'Technology'
        # elif '/business/' in url:
        #     return 'Business'
        # elif '/sports/' in url:
        #     return 'Sport'
        
        return self.category


# ============================================================================
# EXAMPLES FOR DIFFERENT SCRAPING METHODS
# ============================================================================

# EXAMPLE 1: RSS Feed Scraper (RECOMMENDED - Easiest)
# ============================================================================
# import feedparser
# 
# class RSSNewsScraper(BaseScraper):
#     def __init__(self):
#         super().__init__(
#             source="News Source",
#             base_url="https://example.com/rss/news"
#         )
#     
#     def scrape(self) -> List[Article]:
#         articles = []
#         try:
#             feed = feedparser.parse(self.base_url)
#             for entry in feed.entries[:self.max_articles]:
#                 article = self._extract_article(entry)
#                 if article:
#                     articles.append(article)
#             logger.info(f"Scraped {len(articles)} articles from {self.source}")
#         except Exception as e:
#             logger.error(f"Error scraping {self.source}: {str(e)}")
#         return articles
#     
#     def _extract_article(self, entry) -> Article or None:
#         try:
#             title = entry.get('title', '').strip()
#             url = entry.get('link', '').strip()
#             if not self._is_valid_article(title, url):
#                 return None
#             
#             description = entry.get('summary', title).strip()
#             pub_date = None
#             if hasattr(entry, 'published_parsed') and entry.published_parsed:
#                 pub_date = self._parse_iso_date(entry.published_parsed)
#             
#             return self._create_article(title, description, url, pub_date)
#         except:
#             return None


# EXAMPLE 2: HTML Scraper (For sites without RSS)
# ============================================================================
# import requests
# from bs4 import BeautifulSoup
# 
# class HTMLNewsScraper(BaseScraper):
#     def __init__(self):
#         super().__init__(
#             source="HTML News Source",
#             base_url="https://example.com/news"
#         )
#     
#     def scrape(self) -> List[Article]:
#         articles = []
#         try:
#             response = requests.get(self.base_url, timeout=self.timeout)
#             response.raise_for_status()
#             soup = BeautifulSoup(response.content, 'html.parser')
#             
#             # Find article containers (adjust selector based on HTML structure)
#             article_elements = soup.select('article.news-item')[:self.max_articles]
#             
#             for elem in article_elements:
#                 article = self._extract_article(elem)
#                 if article:
#                     articles.append(article)
#             
#             logger.info(f"Scraped {len(articles)} articles from {self.source}")
#         except Exception as e:
#             logger.error(f"Error scraping {self.source}: {str(e)}")
#         return articles
#     
#     def _extract_article(self, element) -> Article or None:
#         try:
#             title = element.select_one('h2, h3')
#             url = element.select_one('a')
#             description = element.select_one('p.summary')
#             
#             if not title or not url:
#                 return None
#             
#             return self._create_article(
#                 title=title.get_text(),
#                 description=description.get_text() if description else "",
#                 url=url.get('href', '')
#             )
#         except:
#             return None


# EXAMPLE 3: JSON API Scraper
# ============================================================================
# import requests
# 
# class JSONAPINewsScraper(BaseScraper):
#     def __init__(self):
#         super().__init__(
#             source="JSON API News",
#             base_url="https://api.example.com/articles"
#         )
#     
#     def scrape(self) -> List[Article]:
#         articles = []
#         try:
#             response = requests.get(self.base_url, timeout=self.timeout)
#             response.raise_for_status()
#             data = response.json()
#             
#             # Adjust based on API response structure
#             items = data.get('articles', [])[:self.max_articles]
#             
#             for item in items:
#                 article = self._extract_article(item)
#                 if article:
#                     articles.append(article)
#             
#             logger.info(f"Scraped {len(articles)} articles from {self.source}")
#         except Exception as e:
#             logger.error(f"Error scraping {self.source}: {str(e)}")
#         return articles
#     
#     def _extract_article(self, item) -> Article or None:
#         try:
#             return self._create_article(
#                 title=item.get('title', ''),
#                 description=item.get('content', item.get('description', '')),
#                 url=item.get('url', ''),
#                 published_date=self._parse_iso_date(item.get('published_at'))
#             )
#         except:
#             return None
