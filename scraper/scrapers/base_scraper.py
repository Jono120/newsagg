from abc import ABC, abstractmethod
from typing import List, Optional, Any
from datetime import datetime
from models.article import Article
import logging

logger = logging.getLogger(__name__)

class BaseScraper(ABC):
    """
    Base class for all news scrapers.
    
    Provides a template for adding new news sources to the aggregator.
    All scrapers must inherit from this class and implement the scrape() method.
    
    Attributes:
        source (str): Name of the news source (e.g., "CNN", "BBC")
        base_url (str): Primary URL or RSS feed for the source
        category (str): Default category for articles (e.g., "General", "Tech")
        max_articles (int): Maximum number of articles to scrape per run
        timeout (int): Request timeout in seconds
    
    Example:
        class MyNewsScraper(BaseScraper):
            def __init__(self):
                super().__init__(
                    source="My News",
                    base_url="https://mynews.com/rss",
                    category="General"
                )
            
            def scrape(self) -> List[Article]:
                articles = []
                # Your scraping logic here
                return articles
    """
    
    def __init__(self, source: str, base_url: str, category: str = "General", max_articles: int = 20, timeout: int = 10):
        self.source = source
        self.base_url = base_url
        self.category = category
        self.max_articles = max_articles
        self.timeout = timeout
    
    @abstractmethod
    def scrape(self) -> List[Article]:
        """Scrape articles from the source.
        
        Returns:
            List[Article]: List of Article objects scraped from the source
        """
        pass
    
    def _create_article(
        self,
        title: str,
        description: str,
        url: str,
        published_date: Optional[str] = None,
        category: Optional[str] = None
    ) -> Article:
        """Helper method to create an Article object.
        
        Args:
            title (str): Article title
            description (str): Article description/summary
            url (str): Article URL
            published_date (str, optional): ISO format date string
            category (str, optional): Article category (uses self.category if None)
        
        Returns:
            Article: Article object ready to be saved
        """
        return Article(
            title=title.strip(),
            description=description.strip(),
            url=url,
            source=self.source,
            category=category or self.category,
            published_date=published_date
        )
    
    def _is_valid_article(self, title: str, url: str) -> bool:
        """Validate article before adding.
        
        Args:
            title (str): Article title
            url (str): Article URL
        
        Returns:
            bool: True if article is valid, False otherwise
        """
        return bool(title.strip() and url.strip())
    
    def _parse_iso_date(self, date_obj: Any) -> Optional[str]:
        """Convert various date formats to ISO format string.
        
        Args:
            date_obj: Date object (tuple, datetime, string, etc.)
        
        Returns:
            str: ISO format date string or None if parsing fails
        """
        try:
            if isinstance(date_obj, tuple):
                # feedparser format: (year, month, day, hour, min, sec, ...)
                return datetime(*date_obj[:6]).isoformat()
            elif isinstance(date_obj, datetime):
                return date_obj.isoformat()
            elif isinstance(date_obj, str):
                # Try parsing ISO format
                return datetime.fromisoformat(date_obj.replace('Z', '+00:00')).isoformat()
            return None
        except Exception as e:
            logger.debug(f"Failed to parse date: {e}")
            return None
