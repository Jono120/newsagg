import requests
import logging
from typing import List
from datetime import datetime
from bs4 import BeautifulSoup
from models.article import Article
from scrapers.base_scraper import BaseScraper

logger = logging.getLogger(__name__)


class NZHeraldScraper(BaseScraper):
    """
    Scraper for NZ Herald news articles.
    
    Uses HTML scraping since NZ Herald doesn't provide a public RSS feed.
    Scrapes the latest news page and extracts article information.
    """
    
    def __init__(self):
        super().__init__(
            source="NZ Herald",
            base_url="https://www.nzherald.co.nz/latest-news/",
            category="General"
        )
    
    def scrape(self) -> List[Article]:
        """Scrape NZ Herald latest news page using HTML parsing"""
        articles = []
        
        try:
            logger.info(f"Starting scrape from {self.source}...")
            
            # Fetch the page with a proper user agent
            headers = {
                'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'
            }
            response = requests.get(self.base_url, headers=headers, timeout=self.timeout)
            response.raise_for_status()
            
            # Parse HTML
            soup = BeautifulSoup(response.content, 'html.parser')
            
            # Find all <article> tags - these are semantic HTML for news articles
            article_elements = soup.find_all('article')
            
            if not article_elements:
                logger.warning(f"No <article> tags found on {self.source} page")
                return articles
            
            logger.debug(f"Found {len(article_elements)} article tags")
            
            # Extract articles (limit to max_articles)
            seen_urls = set()
            for article_elem in article_elements:
                if len(articles) >= self.max_articles:
                    break
                    
                try:
                    article = self._extract_article_from_element(article_elem)
                    if article:
                        # Skip duplicates
                        if article.url not in seen_urls:
                            articles.append(article)
                            seen_urls.add(article.url)
                            
                except Exception as e:
                    logger.debug(f"Error extracting article from element: {e}")
                    continue
            
            logger.info(f"Scraped {len(articles)} articles from {self.source}")
            
        except requests.exceptions.RequestException as e:
            logger.error(f"Network error scraping {self.source}: {e}")
        except Exception as e:
            logger.error(f"Error scraping {self.source}: {e}")
        
        return articles
    
    def _extract_article_from_element(self, article_elem) -> Article or None:
        """Extract article from an <article> HTML element"""
        try:
            # Find the main article link
            # NZ Herald structures: multiple links in each <article> tag
            # The longest URL (100+ chars) is usually the article link
            article_links = article_elem.find_all('a', href=True)
            
            # Find the longest URL (likely the article)
            longest_link = None
            longest_href = None
            max_len = 0
            
            for link in article_links:
                href = link.get('href', '')
                
                # Skip if no URL or too short
                if not href or len(href) < 80:
                    continue
                
                # Skip meta/navigation URLs
                if any(x in href for x in ['/photo-sales', '/about-', '/help-', '/terms', '/privacy', 
                                          '/subscribe', '/newsletters', '/connect/', '/topic/', '/section/']):
                    continue
                
                # Track the longest URL
                if len(href) > max_len:
                    max_len = len(href)
                    longest_link = link
                    longest_href = href
            
            if not longest_href or not longest_link:
                return None
            
            # Make URL absolute if it's relative
            if longest_href.startswith('/'):
                longest_href = f"https://www.nzherald.co.nz{longest_href}"
            
            # Get title - try multiple sources
            title = ""
            
            # 1. Try link text first
            title = longest_link.get_text(strip=True)
            
            # 2. If no title, look for heading in the entire article
            if not title or len(title.split()) < 2:
                for tag in ['h1', 'h2', 'h3', 'h4']:
                    heading = article_elem.find(tag)
                    if heading:
                        heading_text = heading.get_text(strip=True)
                        if heading_text and len(heading_text.split()) >= 2:
                            title = heading_text
                            break
            
            # 3. If still no title, try nested heading in the link
            if not title or len(title.split()) < 2:
                for tag in ['h1', 'h2', 'h3', 'h4']:
                    heading = longest_link.find(tag)
                    if heading:
                        nested_title = heading.get_text(strip=True)
                        if nested_title and len(nested_title.split()) >= 2:
                            title = nested_title
                            break
            
            # 4. Last resort: extract from URL slug
            if not title or len(title.split()) < 2:
                parts = longest_href.rstrip('/').split('/')
                if len(parts) >= 2:
                    # Article slug is typically 2nd to last part
                    slug = parts[-2] if parts[-1].isupper() else parts[-1]
                    if slug and '-' in slug and not slug.isupper():
                        title = slug.replace('-', ' ').title()
            
            # Validate title (require at least 2 words now, was 3)
            if not title or len(title.split()) < 2:
                return None
            
            # Validate article
            if not self._is_valid_article(title, longest_href):
                return None
            
            # Try to extract description
            description = self._extract_description(article_elem, title)
            
            # Determine category from URL
            category = self._determine_category(longest_href)
            
            # Create and return article
            return self._create_article(
                title=title,
                description=description,
                url=longest_href,
                category=category
            )
        
        except Exception as e:
            logger.debug(f"Error extracting article from element: {e}")
            return None
    
    def _extract_article(self, element) -> Article or None:
        """Legacy method - Extract article information from a link element"""
        try:
            # Get URL
            url = element.get('href', '').strip()
            if not url:
                return None
            
            # Make URL absolute if it's relative
            if url.startswith('/'):
                url = f"https://www.nzherald.co.nz{url}"
            
            # Skip meta/navigation URLs early
            if any(x in url for x in ['/photo-sales', '/about-', '/help-', '/terms', '/privacy', '/subscribe', '/newsletters', '/business-reports/', '/connect/']):
                return None
            
            # Get title from link text and nested elements
            title = element.get_text(strip=True)
            
            # Filter out common non-article titles
            non_articles = {'Premium', 'Story', 'story', 'Link', 'N/A', '', '...', 'More', 'View', 'Read', 'Videos', 'Live', 'Updates', 'Watch', 'Get our daily news'}
            if title in non_articles:
                return None
            
            # If title is empty or too short, try nested elements
            if not title or len(title.split()) < 3:
                # Look for heading tags within the link
                for tag in ['h1', 'h2', 'h3', 'h4']:
                    heading = element.find(tag)
                    if heading:
                        nested_title = heading.get_text(strip=True)
                        if nested_title and len(nested_title.split()) >= 3:
                            title = nested_title
                            break
            
            # Extract from URL slug if still no good title
            if not title or len(title.split()) < 3:
                # Extract from URL: /category/article-slug-with-many-words/ID/
                url_parts = url.replace('https://www.nzherald.co.nz', '').split('/')
                # Get the slug (usually right before the ID)
                if len(url_parts) >= 4:
                    slug = url_parts[-2] if url_parts[-1] in ['', '#'] else url_parts[-1]
                    if slug and len(slug) > 8 and not slug.isupper() and '-' in slug:
                        title = slug.replace('-', ' ').title()
            
            # Validate article title length and content
            if not title or len(title.split()) < 3 or len(title) < 15:
                return None
            
            # Validate article
            if not self._is_valid_article(title, url):
                return None
            
            # Try to extract description from nearby elements
            description = self._extract_description(element, title)
            
            # Determine category from URL
            category = self._determine_category(url)
            
            # Create and return article
            return self._create_article(
                title=title,
                description=description,
                url=url,
                category=category
            )
        
        except Exception as e:
            logger.debug(f"Error extracting article: {e}")
            return None
    
    def _extract_description(self, element, title: str) -> str:
        """Extract article description/summary from element"""
        try:
            # Look for summary/description text
            # NZ Herald typically has description text following the headline
            description_elem = element.find('p', class_=['story-card-body', 'summary'])
            
            if description_elem:
                description = description_elem.get_text().strip()
                if description:
                    return description
            
            # Fallback: use title as description
            return title
        
        except:
            return title
    
    def _determine_category(self, url: str) -> str:
        """Determine article category from URL"""
        # Extract category from URL path
        url_lower = url.lower()
        
        category_map = {
            '/nz/': 'New Zealand',
            '/sport/': 'Sport',
            '/world/': 'World',
            '/business/': 'Business',
            '/entertainment/': 'Entertainment',
            '/lifestyle/': 'Lifestyle',
            '/travel/': 'Travel',
            '/politics/': 'Politics',
            '/opinion/': 'Opinion',
            '/auckland/': 'Auckland',
            '/wellington/': 'Wellington',
            '/sport/rugby/': 'Rugby',
            '/sport/cricket/': 'Cricket',
            '/sport/tennis/': 'Tennis',
            '/sport/boxing/': 'Boxing',
            '/sport/racing/': 'Racing',
            '/viva/': 'Lifestyle',
            '/kahu/': 'Kahu',
        }
        
        # Check URL against category patterns (order matters - more specific first)
        for pattern, category in sorted(category_map.items(), key=lambda x: len(x[0]), reverse=True):
            if pattern in url_lower:
                return category
        
        return self.category
