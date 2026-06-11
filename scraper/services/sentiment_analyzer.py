import os
import re
import json
import logging
from contextlib import suppress
from dataclasses import dataclass, field
from typing import Any, List, Optional
from huggingface_hub import InferenceClient

logger = logging.getLogger(__name__)

# Use Twitter RoBERTa for sentiment (fast and reliable).
_SENTIMENT_MODEL = os.getenv(
    "HF_SENTIMENT_MODEL",
    "cardiffnlp/twitter-roberta-base-sentiment-latest",
)
# Use keyphrase extraction for key terms.
_EXTRACTION_MODEL = os.getenv("HF_EXTRACTION_MODEL", "ml6team/keyphrase-extraction-kbir-inspec")

_HF_TOKEN = (
    os.getenv("HF_TOKEN")
    or os.getenv("HUGGINGFACEHUB_API_TOKEN")
    or os.getenv("HF_API_TOKEN")
)

_client = InferenceClient(token=_HF_TOKEN) if _HF_TOKEN else InferenceClient()

_WORD_SENTIMENT_CONFIDENCE_MIN = float(os.getenv("HF_WORD_SENTIMENT_CONFIDENCE_MIN", "0.65"))
_DOC_SENTIMENT_CONFIDENCE_MIN = float(os.getenv("HF_DOC_SENTIMENT_CONFIDENCE_MIN", "0.55"))
_TERM_SIGNAL_MIN_TOTAL = int(os.getenv("HF_TERM_SIGNAL_MIN_TOTAL", "3"))
_TERM_SIGNAL_BALANCE_DELTA = int(os.getenv("HF_TERM_SIGNAL_BALANCE_DELTA", "1"))

_WORD_RE = re.compile(r"[a-zA-Z][a-zA-Z'-]{2,}")
_STOP_WORDS = {
    "the", "and", "for", "that", "with", "this", "from", "have", "has", "had", "were", "was",
    "are", "is", "but", "not", "you", "your", "their", "they", "them", "our", "ours", "its",
    "his", "her", "him", "she", "who", "what", "when", "where", "why", "how", "after", "before",
    "into", "onto", "over", "under", "about", "between", "against", "during", "while", "would",
    "could", "should", "will", "just", "than", "then", "there", "here", "also", "more", "most",
    "some", "such", "only", "very", "much", "many", "few", "all", "any", "each", "both", "news",
    "said", "says", "say", "new", "report", "reports", "today", "yesterday", "tomorrow", "committee"
}


@dataclass
class SentimentTerms:
    label: str = "neutral"
    score: float = 0.0
    confidence: float = 0.0
    positive_words: List[str] = field(default_factory=list)
    negative_words: List[str] = field(default_factory=list)
    key_phrases: List[str] = field(default_factory=list)
    entities: List[str] = field(default_factory=list)


def _clamp01(value: Any, default: float = 0.0) -> float:
    try:
        numeric = float(value)
    except (TypeError, ValueError):
        return default
    return max(0.0, min(1.0, numeric))


def _normalize_score(value: Any, label: str, confidence: float) -> float:
    try:
        score = float(value)
    except (TypeError, ValueError):
        score = confidence if label == "positive" else -confidence if label == "negative" else 0.0

    if score < -0.5 or score > 0.5:
        score = max(-0.5, min(0.5, score))

    if label == "positive" and score < 0:
        return abs(score)
    if label == "negative" and score > 0:
        return -abs(score)
    if label == "neutral":
        return 0.0
    return score


def _normalize_label(raw_label: Any) -> str:
    label = str(raw_label or "").strip().lower()
    if not label:
        return "neutral"

    if label in {"label_0", "0", "0 star", "1 star", "2 stars"}:
        return "negative"
    if label in {"label_1", "3 stars", "neutral"}:
        return "neutral"
    if label in {"label_2", "1", "4 stars", "5 stars"}:
        return "positive"

    if "neg" in label:
        return "negative"
    if "pos" in label:
        return "positive"
    return "neutral"


def _extract_json_object(raw: str) -> Optional[dict[str, Any]]:
    payload = raw.strip()
    if not payload:
        return None

    fenced = re.search(r"```(?:json)?\s*(\{.*?\})\s*```", payload, re.DOTALL | re.IGNORECASE)
    if fenced:
        payload = fenced.group(1).strip()
    else:
        start = payload.find("{")
        end = payload.rfind("}")
        if start != -1 and end != -1 and end > start:
            payload = payload[start : end + 1]

    with suppress(json.JSONDecodeError, TypeError, ValueError):
        parsed = json.loads(payload)
        if isinstance(parsed, dict):
            return parsed
    return None


def _normalize_terms(values: Any, limit: int) -> List[str]:
    raw_terms: List[str]
    if isinstance(values, list):
        raw_terms = [str(v) for v in values]
    elif isinstance(values, str):
        raw_terms = re.split(r"[,\n;]", values)
    else:
        return []

    cleaned: List[str] = []
    seen = set()
    for term in raw_terms:
        normalized = term.strip().lower()
        if not normalized:
            continue
        normalized = normalized.replace("##", "")
        if not _WORD_RE.fullmatch(normalized):
            continue
        if normalized in _STOP_WORDS or normalized in seen:
            continue
        seen.add(normalized)
        cleaned.append(normalized)
        if len(cleaned) >= limit:
            break
    return cleaned


def _classify_document_with_sentiment_model(text: str) -> tuple[str, float, float]:
    """Analyse sentiment using the Twitter RoBERTa model (fast and reliable)."""
    with suppress(Exception):
        out = _client.text_classification(text, model=_SENTIMENT_MODEL)
        if out:
            top = out[0]
            label = _normalize_label(getattr(top, "label", "neutral"))
            confidence = _clamp01(getattr(top, "score", 0.0))
            score = _normalize_score(None, label, confidence)
            logger.debug("Sentiment: %s (score=%.2f, confidence=%.2f)", label, score, confidence)
            return label, score, confidence
    return "neutral", 0.0, 0.0


def _extract_candidate_words(text: str, limit: int = 30) -> List[str]:
    words: List[str] = []
    seen = set()
    for token in _WORD_RE.findall(text.lower()):
        if token in _STOP_WORDS:
            continue
        if token in seen:
            continue
        seen.add(token)
        words.append(token)
        if len(words) >= limit:
            break
    return words


def _extract_terms_with_keyphrase_model(text: str, limit: int = 20) -> List[str]:
    terms: List[tuple[float, str]] = []
    seen = set()

    with suppress(Exception):
        entities = _client.token_classification(text, model=_EXTRACTION_MODEL)
        for entity in entities or []:
            word = str(getattr(entity, "word", "")).strip().lower().replace("##", "")
            if not word:
                continue
            if not _WORD_RE.fullmatch(word):
                continue
            if word in _STOP_WORDS or word in seen:
                continue
            seen.add(word)
            score = float(getattr(entity, "score", 0.0) or 0.0)
            terms.append((score, word))

    if not terms:
        return _extract_candidate_words(text, limit=limit)

    terms.sort(key=lambda item: item[0], reverse=True)
    return [w for _, w in terms[:limit]]


def analyze_text_sentiment_and_terms(title: str, description: str = "") -> SentimentTerms:
    """
    Analyse sentiment and extract key terms using fast, reliable HF models.
    
    Pipeline:
    - Twitter RoBERTa for sentiment classification (fast)
    - Keyphrase extraction model for terms (accurate)
    - Fallback to candidate word extraction if keyphrase fails
    """
    text = f"{(title or '').strip()} {(description or '').strip()}".strip()
    if not text:
        logger.debug("Empty text for analysis, returning defaults")
        return SentimentTerms()

    # 1) Sentiment analysis using Twitter RoBERTa.
    label, score, confidence = _classify_document_with_sentiment_model(text)

    # 2) Key-term extraction using keyphrase model.
    extracted_terms = _extract_terms_with_keyphrase_model(text)

    # 3) Extract positive/negative words based on sentiment context.
    positive_words: List[str] = []
    negative_words: List[str] = []
    
    if label == "positive":
        # Find positive words in the text.
        positive_words = _extract_candidate_words(text, limit=10)
    elif label == "negative":
        # Find negative words in the text.
        negative_words = _extract_candidate_words(text, limit=10)

    # 4) Validate and neutralise if uncertain.
    pos_count = len(positive_words)
    neg_count = len(negative_words)
    total_term_signal = pos_count + neg_count
    is_low_confidence = confidence < _DOC_SENTIMENT_CONFIDENCE_MIN
    is_weak_term_signal = 0 < total_term_signal < _TERM_SIGNAL_MIN_TOTAL
    is_near_balanced = (
        total_term_signal >= _TERM_SIGNAL_MIN_TOTAL
        and abs(pos_count - neg_count) <= _TERM_SIGNAL_BALANCE_DELTA
    )

    # Neutralise if uncertain.
    if is_low_confidence or is_weak_term_signal or is_near_balanced:
        logger.debug("Low confidence/weak signal detected; neutralizing. "
                    "confidence=%.2f, pos=%d, neg=%d, balanced=%s",
                    confidence, pos_count, neg_count, is_near_balanced)
        label = "neutral"
        score = 0.0

    # Normalise score sign based on label.
    if label == "positive" and score < 0:
        score = abs(score)
    elif label == "negative" and score > 0:
        score = -abs(score)
    elif label == "neutral":
        score = 0.0

    confidence = _clamp01(confidence)
    score = max(-1.0, min(1.0, score))

    logger.info("Article sentiment: %s (score=%.2f, confidence=%.2f, terms=%d, pos=%d, neg=%d)",
                label, score, confidence, len(extracted_terms), len(positive_words), len(negative_words))

    return SentimentTerms(
        label=label,
        score=score,
        confidence=confidence,
        positive_words=positive_words,
        negative_words=negative_words,
    )
