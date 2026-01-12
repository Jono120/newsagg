import feedparser
import logging
from typing import List
from datetime import datetime
from models.article import Article
from scrapers.base_scraper import BaseScraper

logger = logging.getLogger(__name__)

class RNZScraper(BaseScraper):
    def __init__(self):
        super().__init__(
            source="RNZ",
            base_url="https://www.rnz.co.nz/rss/news",
            category="News"
        )
    
    def scrape(self) -> List[Article]:
        """Scrape RNZ articles from RSS feed"""
        articles = []
        
        try:
            # Parse RSS feed
            feed = feedparser.parse(self.base_url)
            
            if not feed.entries:
                logger.warning("No entries found in RNZ RSS feed")
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
                    # Remove HTML tags from description if present
                    if '<' in description:
                        from bs4 import BeautifulSoup
                        description = BeautifulSoup(description, 'html.parser').get_text(strip=True)
                    
                    # Parse published date if available
                    pub_date = None
                    if hasattr(entry, 'published_parsed') and entry.published_parsed:
                        try:
                            pub_date = datetime(*entry.published_parsed[:6]).isoformat()
                        except:
                            pass
                    
                    # Determine category from URL
                    category = self.category
                    if '/national/' in url:
                        category = 'National'
                    elif '/world/' in url:
                        category = 'World'
                    elif '/political/' in url:
                        category = 'Politics'
                    elif '/business/' in url:
                        category = 'Business'
                    elif '/sport/' in url:
                        category = 'Sport'
                    
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
