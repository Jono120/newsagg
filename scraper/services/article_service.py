import requests
import logging
from typing import Dict, List, Optional, Any

logger = logging.getLogger(__name__)


def _truncate(s: Any, length: int = 500) -> str:
    """Safely convert a value to string and truncate for logging."""
    try:
        text = str(s)
    except Exception:
        text = '<unrepresentable>'
    if len(text) > length:
        return text[:length] + '...'
    return text

class ArticleService:
    def __init__(self, base_url: str, articles_endpoint: str):
        self.base_url = base_url.rstrip('/')
        self.articles_endpoint = articles_endpoint
        self.articles_url = f"{self.base_url}{self.articles_endpoint}"
    
    def create_article(self, article) -> bool:
        """Send an article to the backend API"""
        try:
            payload = article.to_dict()
            response = requests.post(
                self.articles_url,
                json=payload,
                timeout=10
            )

            if response.status_code in [200, 201]:
                logger.info("Successfully created article: %s...", _truncate(article.title, 50))
                return True
            elif response.status_code == 409:
                logger.debug("Article already exists (duplicate): %s", article.url)
                return False
            else:
                logger.error(
                    "Failed to create article. URL=%s Status=%d PayloadSize=%d Response=%s",
                    self.articles_url,
                    response.status_code,
                    len(_truncate(payload)),
                    _truncate(response.text)
                )
                logger.debug("Failed payload (truncated): %s", _truncate(payload, 1000))
                return False

        except requests.exceptions.RequestException:
            logger.exception("Request exception while creating article. URL=%s PayloadSize=%d",
                             self.articles_url, len(_truncate(article.to_dict())))
            return False
    
    def create_articles_batch(self, articles: List) -> Dict:
        """Send multiple articles to the backend API in a single batch request"""
        try:
            batch_url = f"{self.articles_url}/batch"
            articles_data = [article.to_dict() for article in articles]
            
            response = requests.post(
                batch_url,
                json=articles_data,
                timeout=30
            )
            
            if response.status_code == 200:
                try:
                    result = response.json()
                except ValueError:
                    logger.error("Invalid JSON in batch response; treating as empty result. URL=%s Response=%s",
                                 batch_url, _truncate(response.text))
                    result = {}

                # Ensure we have a dict to call .get on (protect against None or non-object JSON)
                if not isinstance(result, dict):
                    logger.warning("Batch response JSON is not an object; treating as empty result. URL=%s ResponseType=%s",
                                   batch_url, type(result))
                    result = {}

                added = result.get('added', 0)
                skipped = result.get('skipped', 0)
                errors = result.get('errors', []) or []

                logger.info("Batch import: %d added, %d skipped, %d errors (URL=%s)",
                           added,
                           skipped,
                           len(errors),
                           batch_url)

                if errors:
                    # Log a small sample of errors for debugging
                    sample = errors[:5]
                    logger.warning("Batch errors sample (first %d): %s", len(sample), _truncate(sample, 2000))

                return {'added': added, 'skipped': skipped, 'errors': errors}
            else:
                logger.error(
                    "Failed to create articles batch. URL=%s Status=%d Response=%s",
                    batch_url, response.status_code, _truncate(response.text)
                )
                logger.debug("Batch payload sample (truncated): %s", _truncate(articles_data, 2000))
                return {'added': 0, 'skipped': 0, 'errors': [_truncate(response.text)]}
                
        except requests.exceptions.RequestException:
            logger.exception("Request exception while creating articles batch. URL=%s PayloadCount=%d",
                             batch_url, len(articles))
            return {'added': 0, 'skipped': 0, 'errors': ['request exception']}
    
    def get_articles(self) -> Optional[List[Dict]]:
        """Retrieve all articles from the backend API"""
        try:
            response = requests.get(self.articles_url, timeout=10)
            
            if response.status_code == 200:
                try:
                    return response.json()
                except ValueError:
                    logger.error("Invalid JSON when retrieving articles from %s: %s", self.articles_url, _truncate(response.text))
                    return None
            else:
                logger.error("Failed to retrieve articles. Status: %d", response.status_code)
                return None
                
        except requests.exceptions.RequestException:
            logger.exception("Request error while retrieving articles from %s", self.articles_url)
            return None
