import feedparser
import logging
from typing import List
from datetime import datetime
from models.article import Article
from scrapers.base_scraper import BaseScraper

logger = logging.getLogger(__name__)

class StuffScraper(BaseScraper):
    def __init__(self):
        super().__init__(
            source="Stuff NZ",
            base_url="https://www.stuff.co.nz/rss",
            category="General"
        )
    
    def scrape(self) -> List[Article]:
        """Scrape Stuff NZ articles from RSS feed"""
        articles = []
        
        try:
            # Parse RSS feed
            feed = feedparser.parse(self.base_url)
            
            if not feed.entries:
                logger.warning("No entries found in Stuff RSS feed")
                return articles
            
            # Process entries (limit to 20 most recent)
            for entry in feed.entries[:20]:
                try:
                    # Extract required fields
                    title = entry.get('title', '').strip()
                    url = entry.get('link', '').strip()
                    
                    if not title or not url:
                        continue
                    
                    # Extract optional fields
                    description = entry.get('summary', title).strip()
                    
                    # Parse published date if available
                    pub_date = None
                    if hasattr(entry, 'published_parsed') and entry.published_parsed:
                        try:
                            pub_date = datetime(*entry.published_parsed[:6]).isoformat()
                        except:
                            pass
                    
                    # Determine category from URL
                    category = self.category
                    if '/nz-news/' in url:
                        category = 'NZ News'
                    elif '/world/' in url:
                        category = 'World'
                    elif '/sport/' in url:
                        category = 'Sport'
                    elif '/business/' in url:
                        category = 'Business'
                    elif '/entertainment/' in url:
                        category = 'Entertainment'
                    
                    # Create article
                    article = Article(
                        title=title,
                        description=description,
                        url=url,
                        source=self.source,
                        category=category,
                        published_date=pub_date
                    )
                    articles.append(article)
                    
                except Exception as e:
                    logger.warning("Error parsing RSS entry: %s", str(e))
                    continue
            
            logger.info("Scraped %d articles from %s", len(articles), self.source)
            
        except Exception as e:
            logger.error("Error scraping %s: %s", self.source, str(e))
        
        return articles
