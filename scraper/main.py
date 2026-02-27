import argparse
import logging
import os
import time

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

# Load environment variables
load_dotenv()

class ScraperOrchestrator:
    def __init__(self):
        self.article_service = ArticleService(
            base_url=os.getenv('API_BASE_URL', 'http://localhost:5000'),
            articles_endpoint=os.getenv('API_ARTICLES_ENDPOINT', '/api/articles')
        )
        
        # Auto-load all registered scrapers
        self.scrapers = get_all_scrapers()
        logger.info(f"Loaded {len(self.scrapers)} scrapers: {', '.join(s.source for s in self.scrapers)}")
    
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

                    for art in articles:
                        sentiment = analyze_text_sentiment_and_terms(
                            getattr(art, 'title', ''),
                            getattr(art, 'description', '')
                        )
                        art.sentiment_label = sentiment.label
                        art.sentiment_score = sentiment.score
                        art.sentiment_confidence = sentiment.confidence
                        art.positive_words = sentiment.positive_words
                        art.negative_words = sentiment.negative_words

                    # Optionally extract article content before sending
                    extract_flag = os.getenv('SCRAPE_EXTRACT_CONTENT', '1')
                    if extract_flag and extract_flag.lower() not in ('0', 'false', 'no'):
                        for art in articles:
                            try:
                                # Only attempt extraction if content not already present
                                if getattr(art, 'content', None):
                                    continue
                                content = extract_content(art.url, timeout=10)
                                if content:
                                    art.content = content
                            except Exception:
                                logger.debug('Content extraction failed for %s', art.url, exc_info=True)

                    # Use batch import for efficiency
                    result = self.article_service.create_articles_batch(articles)
                    total_added += result.get('added', 0)
                    total_skipped += result.get('skipped', 0)
                    
                    logger.info("Scraped %d articles from %s: %d added, %d skipped", 
                               len(articles), scraper.source, 
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
