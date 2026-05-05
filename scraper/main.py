import argparse
import logging
import os
import time
from concurrent.futures import ThreadPoolExecutor, as_completed

import schedule
from dotenv import load_dotenv
from scrapers import get_all_scrapers
from services.article_service import ArticleService
from services.content_extractor import extract_content
from services.sentiment_analyzer import analyze_text_sentiment_and_terms

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

# Suppress verbose dependency logs (e.g., per-request HTTP info from HF/httpx)
for noisy_logger in ("httpx", "httpcore", "huggingface_hub"):
    logging.getLogger(noisy_logger).setLevel(logging.WARNING)

# Load environment variables
load_dotenv()

def _enrich_article(article, extract_flag):
    """
    Enrich a single article with content extraction and sentiment analysis.
    This function is designed to run in parallel via ThreadPoolExecutor.
    
    Args:
        article: Article object to enrich
        extract_flag: Whether to extract content from the article URL
    
    Returns:
        tuple: (article, error_msg or None)
    """
    try:
        # Content extraction (if enabled)
        if extract_flag and not getattr(article, 'content', None):
            try:
                content = extract_content(article.url, timeout=10)
                if content:
                    article.content = content
            except Exception as e:
                logger.debug('Content extraction failed for %s: %s', article.url, str(e))
        
        # Sentiment analysis
        title = getattr(article, 'title', '')
        description = getattr(article, 'description', '')
        content = getattr(article, 'content', '')
        context_text = f"{description} {content[:2000]}".strip()
        
        try:
            sentiment = analyze_text_sentiment_and_terms(title, context_text)
            article.sentiment_label = sentiment.label
            article.sentiment_score = sentiment.score
            article.sentiment_confidence = sentiment.confidence
            article.positive_words = sentiment.positive_words
            article.negative_words = sentiment.negative_words
        except Exception as e:
            logger.debug('Sentiment analysis failed for %s: %s', article.url, str(e))
        
        return (article, None)
    except Exception as e:
        logger.warning('Error enriching article %s: %s', getattr(article, 'url', 'unknown'), str(e))
        return (article, str(e))

class ScraperOrchestrator:
    def __init__(self):
        self.article_service = ArticleService(
            base_url=os.getenv('API_BASE_URL', 'http://localhost:5000'),
            articles_endpoint=os.getenv('API_ARTICLES_ENDPOINT', '/api/articles')
        )
        
        # Validate backend connectivity early
        try:
            import requests
            health_url = f"{self.article_service.base_url}/api/health"
            response = requests.get(health_url, timeout=5)
            if response.status_code == 200:
                logger.info(f"Backend health check passed: {health_url}")
            else:
                logger.warning(f"Backend health check returned {response.status_code}; proceeding anyway")
        except Exception as e:
            logger.warning(f"Could not verify backend at {self.article_service.base_url}: {e}. Proceeding with scraping.")
        
        # Auto-load all registered scrapers
        self.scrapers = get_all_scrapers()
        logger.info(f"Loaded {len(self.scrapers)} scrapers: {', '.join(s.source for s in self.scrapers)}")
        logger.info(f"Backend API: {self.article_service.articles_url}")

    
    def scrape_all(self):
        """Run all scrapers and send articles to the API"""
        logger.info("Starting scraping cycle...")
        
        total_scraped = 0
        total_added = 0
        total_skipped = 0
        
        for scraper in self.scrapers:
            try:
                logger.info("Running %s scraper...", scraper.source)
                articles = scraper.scrape()
                
                if articles:
                    total_scraped += len(articles)

                    # Enrich articles in parallel (content extraction + sentiment analysis)
                    extract_flag = os.getenv('SCRAPE_EXTRACT_CONTENT', '1')
                    extract_enabled = extract_flag and extract_flag.lower() not in ('0', 'false', 'no')
                    
                    enrichment_start = time.time()
                    enriched_articles = []
                    
                    # Use ThreadPoolExecutor for parallel enrichment (max 4 workers to avoid rate limiting)
                    with ThreadPoolExecutor(max_workers=4) as executor:
                        futures = [
                            executor.submit(_enrich_article, art, extract_enabled)
                            for art in articles
                        ]
                        
                        for future in as_completed(futures):
                            try:
                                enriched_art, error = future.result(timeout=30)
                                enriched_articles.append(enriched_art)
                                if error:
                                    logger.debug('Article enrichment had minor error: %s', error)
                            except Exception as e:
                                logger.warning('Enrichment future failed: %s', str(e))
                    
                    enrichment_time = time.time() - enrichment_start
                    logger.info("Enriched %d articles in %.2f seconds", len(enriched_articles), enrichment_time)

                    # Use batch import for efficiency
                    result = self.article_service.create_articles_batch(enriched_articles)
                    total_added += result.get('added', 0)
                    total_skipped += result.get('skipped', 0)
                    
                    logger.info("Scraped %d articles from %s: %d added, %d skipped", 
                               len(enriched_articles), scraper.source, 
                               result.get('added', 0), result.get('skipped', 0))
                    
                    errors = result.get('errors') or []
                    if errors:
                        # Log a small sample of errors to aid debugging without flooding logs
                        sample = errors[:5]
                        try:
                            sample_str = str(sample)
                        except Exception:
                            sample_str = '<unrepresentable>'
                        if len(sample_str) > 1000:
                            sample_str = sample_str[:1000] + '...'
                        logger.warning("Batch had %d errors; sample: %s", len(errors), sample_str)
                else:
                    logger.warning("No articles found from %s", scraper.source)
                    
            except Exception as e:
                logger.error("Error scraping %s: %s", scraper.source, str(e), exc_info=True)
        
        logger.info("Scraping cycle completed: %d scraped, %d added, %d skipped", 
                   total_scraped, total_added, total_skipped)
        
        return {
            'scraped': total_scraped,
            'added': total_added,
            'skipped': total_skipped
        }
    
    def run_once(self):
        """Run scrapers once"""
        self.scrape_all()
    
    def run_scheduled(self):
        """Run scrapers on a schedule"""
        interval = int(os.getenv('SCRAPE_INTERVAL_MINUTES', '30'))
        logger.info("Scheduling scraper to run every %d minutes", interval)
        
        # Run immediately on start
        self.scrape_all()
        
        # Schedule periodic runs
        schedule.every(interval).minutes.do(self.scrape_all)
        
        while True:
            schedule.run_pending()
            time.sleep(60)  # Check every minute

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description='Scraper orchestrator')
    parser.add_argument('--scheduled', action='store_true', help='Run in scheduled mode')
    parser.add_argument('--interval-minutes', type=int, help='Override schedule interval (minutes)')
    args = parser.parse_args()

    orchestrator = ScraperOrchestrator()

    if args.interval_minutes:
        os.environ['SCRAPE_INTERVAL_MINUTES'] = str(args.interval_minutes)

    if args.scheduled:
        orchestrator.run_scheduled()
    else:
        orchestrator.run_once()
