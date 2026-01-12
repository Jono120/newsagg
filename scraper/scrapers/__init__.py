"""
Scrapers package - Automatically discovers and registers all scrapers.

To add a new scraper:
1. Create a new file (e.g., bbc_scraper.py)
2. Implement a class that inherits from BaseScraper
3. Register it in the SCRAPER_REGISTRY below
4. It will be auto-loaded by ScraperOrchestrator

Do NOT include TemplateNewsScraper in registry - it's for reference only.
"""

from scrapers.base_scraper import BaseScraper
from scrapers.stuff_scraper import StuffScraper
from scrapers.rnz_scraper import RNZScraper
from scrapers.onenews_scraper import OneNewsScraper
from scrapers.nzherald_scraper import NZHeraldScraper

# Registry of all active scrapers
SCRAPER_REGISTRY = [
    StuffScraper,
    RNZScraper,
    OneNewsScraper,
    NZHeraldScraper,
    # Add new scrapers here as you create them:
    # SkyNewsScraper,
    # BBCScraper,
    # CNNScraper,
]

def get_all_scrapers():
    """Get instances of all registered scrapers."""
    return [scraper_class() for scraper_class in SCRAPER_REGISTRY]

__all__ = [
    'BaseScraper',
    'StuffScraper',
    'RNZScraper',
    'OneNewsScraper',
    'NZHeraldScraper',
    'SCRAPER_REGISTRY',
    'get_all_scrapers',
]
