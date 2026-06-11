"""Azure AI Language (Text Analytics) provider.

Wraps the ``azure-ai-textanalytics`` SDK to produce the same
``SentimentTerms`` shape used by the HuggingFace path so the two providers
are interchangeable. One call covers document sentiment (with opinion
mining), key-phrase extraction and named-entity recognition.
"""
import os
import logging
from functools import lru_cache
from typing import List, Optional

from .sentiment_analyzer import SentimentTerms

logger = logging.getLogger(__name__)

_ENDPOINT = os.getenv("AZURE_LANGUAGE_ENDPOINT")
_KEY = os.getenv("AZURE_LANGUAGE_KEY")

# Azure Text Analytics rejects documents larger than 5120 chars per request.
_MAX_DOC_CHARS = 5000
_MAX_TERMS = 15


@lru_cache(maxsize=1)
def _get_client():
    """Build (and cache) a TextAnalyticsClient from env configuration."""
    if not _ENDPOINT or not _KEY:
        raise RuntimeError(
            "Azure Language not configured: set AZURE_LANGUAGE_ENDPOINT and AZURE_LANGUAGE_KEY"
        )

    # Import lazily so the package is only required when this provider is used.
    from azure.ai.textanalytics import TextAnalyticsClient
    from azure.core.credentials import AzureKeyCredential

    return TextAnalyticsClient(endpoint=_ENDPOINT, credential=AzureKeyCredential(_KEY))


def is_configured() -> bool:
    return bool(_ENDPOINT and _KEY)


def _normalize_label(sentiment: str) -> str:
    label = (sentiment or "").strip().lower()
    if label in {"positive", "negative", "neutral"}:
        return label
    # Azure returns "mixed" for documents with both signals; treat it as neutral.
    return "neutral"


def _dedupe(values: List[str], limit: int) -> List[str]:
    cleaned: List[str] = []
    seen = set()
    for value in values:
        normalized = (value or "").strip()
        if not normalized:
            continue
        key = normalized.lower()
        if key in seen:
            continue
        seen.add(key)
        cleaned.append(normalized)
        if len(cleaned) >= limit:
            break
    return cleaned


def analyze_text_sentiment_and_terms(title: str, description: str = "") -> SentimentTerms:
    """Analyse sentiment, opinions, key phrases and entities via Azure AI Language."""
    text = f"{(title or '').strip()} {(description or '').strip()}".strip()
    if not text:
        return SentimentTerms()

    client = _get_client()
    documents = [text[:_MAX_DOC_CHARS]]

    label = "neutral"
    score = 0.0
    confidence = 0.0
    positive_words: List[str] = []
    negative_words: List[str] = []

    sentiment_result = client.analyze_sentiment(documents, show_opinion_mining=True)[0]
    if getattr(sentiment_result, "is_error", False):
        raise RuntimeError(f"Azure sentiment error: {getattr(sentiment_result, 'error', 'unknown')}")

    label = _normalize_label(sentiment_result.sentiment)
    scores = sentiment_result.confidence_scores
    # Signed score in [-1, 1]: positive confidence minus negative confidence.
    score = round(float(scores.positive) - float(scores.negative), 4)
    confidence = float(getattr(scores, label, max(scores.positive, scores.neutral, scores.negative)))

    for sentence in sentiment_result.sentences:
        for opinion in getattr(sentence, "mined_opinions", []) or []:
            target = opinion.target
            target_text = (target.text or "").strip()
            if not target_text:
                continue
            if target.sentiment == "positive":
                positive_words.append(target_text)
            elif target.sentiment == "negative":
                negative_words.append(target_text)

    key_phrases: List[str] = []
    try:
        kp_result = client.extract_key_phrases(documents)[0]
        if not getattr(kp_result, "is_error", False):
            key_phrases = list(kp_result.key_phrases)
    except Exception as exc:  # noqa: BLE001 - key phrases are best-effort
        logger.debug("Azure key-phrase extraction failed: %s", exc)

    entities: List[str] = []
    try:
        ent_result = client.recognize_entities(documents)[0]
        if not getattr(ent_result, "is_error", False):
            entities = [e.text for e in ent_result.entities]
    except Exception as exc:  # noqa: BLE001 - entities are best-effort
        logger.debug("Azure entity recognition failed: %s", exc)

    result = SentimentTerms(
        label=label,
        score=max(-1.0, min(1.0, score)),
        confidence=max(0.0, min(1.0, confidence)),
        positive_words=_dedupe(positive_words, _MAX_TERMS),
        negative_words=_dedupe(negative_words, _MAX_TERMS),
        key_phrases=_dedupe(key_phrases, _MAX_TERMS),
        entities=_dedupe(entities, _MAX_TERMS),
    )

    logger.info(
        "Azure sentiment: %s (score=%.2f, confidence=%.2f, phrases=%d, entities=%d)",
        result.label, result.score, result.confidence, len(result.key_phrases), len(result.entities),
    )
    return result
