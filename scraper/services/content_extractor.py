import logging
from typing import Optional
import requests
from bs4 import BeautifulSoup

logger = logging.getLogger(__name__)


def _clean_text(s: str) -> str:
    # Normalize whitespace
    return ' '.join(s.split())


def extract_content(url: str, timeout: int = 10, max_chars: int = 20000) -> Optional[str]:
    """Fetch a URL and try to extract the main article text.

    Strategy:
    - Fetch HTML via requests
    - Parse with BeautifulSoup
    - Remove script/style
    - Prefer <article> or <main> tags
    - Otherwise choose the largest text-bearing tag (div/p) by text length
    - Return cleaned text truncated to `max_chars`
    """
    try:
        resp = requests.get(url, timeout=timeout, headers={"User-Agent": "newsagg-scraper/1.0"})
        resp.raise_for_status()
        html = resp.text
    except requests.RequestException as e:
        logger.debug("Failed to fetch URL for extraction %s: %s", url, e)
        return None

    try:
        soup = BeautifulSoup(html, 'lxml')

        # Remove scripts/styles
        for tag in soup(['script', 'style', 'noscript', 'iframe']):
            tag.decompose()

        # Prefer semantic article or main tags
        main_candidate = soup.find(['article', 'main'])
        if main_candidate:
            text = main_candidate.get_text(separator=' ', strip=True)
            cleaned = _clean_text(text)
            return cleaned[:max_chars]

        # Fallback: find the largest text-bearing node among divs and sections
        candidates = soup.find_all(['div', 'section', 'article', 'p'])
        best = ''
        for c in candidates:
            try:
                t = c.get_text(separator=' ', strip=True)
                if len(t) > len(best):
                    best = t
            except Exception:
                continue

        if best:
            cleaned = _clean_text(best)
            return cleaned[:max_chars]

        # Ultimate fallback: page title + meta description
        title = soup.title.string if soup.title and soup.title.string else ''
        meta = ''
        desc = soup.find('meta', attrs={'name': 'description'}) or soup.find('meta', attrs={'property': 'og:description'})
        if desc and desc.get('content'):
            meta = desc.get('content')

        combined = _clean_text(f"{title} {meta}").strip()
        return combined[:max_chars] if combined else None

    except Exception as e:
        logger.exception("Error extracting content from %s: %s", url, e)
        return None
