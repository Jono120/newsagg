import feedparser
import logging
from typing import List
from datetime import datetime
from models.article import Article
from scrapers.base_scraper import BaseScraper

logger = logging.getLogger(__name__)

class OneNewsScraper(BaseScraper):
    def __init__(self):
        # Using Google News RSS as fallback since 1News doesn't have RSS
        super().__init__(
            source="1News NZ",
            base_url="https://news.google.com/rss/search?q=site:1news.co.nz&hl=en-NZ&gl=NZ&ceid=NZ:en",
            category="Latest"
        )
    
    def scrape(self) -> List[Article]:
        """Scrape 1News NZ articles from Google News RSS feed"""
        articles = []
        
        try:
            # Parse RSS feed
            feed = feedparser.parse(self.base_url)
            
            if not feed.entries:
                logger.warning("No entries found in 1News Google News RSS feed")
                return articles
            
            # Process entries (limit to 20 most recent)
            for entry in feed.entries[:20]:
                try:
                    # Extract required fields
                    title = entry.get('title', '').strip()
                    url = entry.get('link', '').strip()
                    
                    if not title or not url:
                        continue
                    
                    # Google News URLs are redirect links, extract actual URL
                    # The actual 1news.co.nz URL is in the entry
                    if 'news.google.com' in url and hasattr(entry, 'links'):
                        for link in entry.links:
                            if 'url' in link and '1news.co.nz' in link.get('url', ''):
                                url = link['url']
                                break
                    
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
                    if '/new-zealand/' in url:
                        category = 'New Zealand'
                    elif '/world/' in url:
                        category = 'World'
                    elif '/politics/' in url:
                        category = 'Politics'
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
