"""Provider dispatcher for text analytics / sentiment.

Selects the analyzer backend from the ``ANALYZER_PROVIDER`` env var:

- ``azure``       -> Azure AI Language (default)
- ``huggingface`` -> HuggingFace Inference models
- ``rules``       -> offline keyword heuristics

On any error the dispatcher falls back to the HuggingFace path so a single
provider outage does not stop enrichment.
"""
import os
import logging

from . import sentiment_analyzer
from .sentiment_analyzer import SentimentTerms

logger = logging.getLogger(__name__)


def _provider() -> str:
    return (os.getenv("ANALYZER_PROVIDER") or "azure").strip().lower()


def analyze(title: str, description: str = "") -> SentimentTerms:
    """Analyse text using the configured provider, falling back to HuggingFace."""
    provider = _provider()

    if provider == "azure":
        try:
            from . import azure_text_analytics

            if azure_text_analytics.is_configured():
                return azure_text_analytics.analyze_text_sentiment_and_terms(title, description)
            logger.warning("ANALYZER_PROVIDER=azure but Azure Language is not configured; using HuggingFace fallback")
        except Exception as exc:  # noqa: BLE001 - degrade gracefully to fallback
            logger.warning("Azure analyzer failed (%s); using HuggingFace fallback", exc)

    return sentiment_analyzer.analyze_text_sentiment_and_terms(title, description)
