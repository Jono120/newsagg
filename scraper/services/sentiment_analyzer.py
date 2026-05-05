import os
import re
import json
from contextlib import suppress
from dataclasses import dataclass, field
from typing import Any, List, Optional
from huggingface_hub import InferenceClient

_SENTIMENT_MODEL = os.getenv(
    "HF_SENTIMENT_MODEL",
    "cardiffnlp/twitter-roberta-base-sentiment-latest",
)
_EXTRACTION_MODEL = os.getenv("HF_EXTRACTION_MODEL", "ml6team/keyphrase-extraction-kbir-inspec")
_GEMMA_MODEL = os.getenv("HF_GEMMA_MODEL", "google/gemma-3-270m")


def _env_flag(name: str, default: str = "1") -> bool:
    raw = (os.getenv(name, default) or "").strip().lower()
    return raw not in {"0", "false", "no", "off"}


_ENABLE_GEMMA_SENTIMENT = _env_flag("HF_ENABLE_GEMMA_SENTIMENT", "1")
_ENABLE_GEMMA_EXTRACTION = _env_flag("HF_ENABLE_GEMMA_EXTRACTION", "1")
_GEMMA_MAX_INPUT_CHARS = int(os.getenv("HF_GEMMA_MAX_INPUT_CHARS", "4000"))
_GEMMA_MAX_NEW_TOKENS = int(os.getenv("HF_GEMMA_MAX_NEW_TOKENS", "280"))

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

    if score < -1.0 or score > 1.0:
        score = max(-1.0, min(1.0, score))

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


def _gemma_sentiment_and_terms(text: str) -> tuple[Optional[str], float, float, List[str], List[str], List[str]]:
    compact_text = " ".join(text.split())[:_GEMMA_MAX_INPUT_CHARS]
    if not compact_text:
        return None, 0.0, 0.0, [], [], []

    prompt = (
        "You are a strict JSON generator. Analyze news text sentiment and terms. "
        "Return one JSON object only with keys: "
        "label, score, confidence, positive_words, negative_words, key_terms. "
        "Rules: label is positive|neutral|negative; score is float between -1 and 1; "
        "confidence is float between 0 and 1; word lists must be lowercase single words only and max 8 items; "
        "key_terms max 20 items and lowercase single words only.\n"
        "Text:\n"
        f"{compact_text}"
    )

    with suppress(Exception):
        generated = _client.text_generation(
            prompt,
            model=_GEMMA_MODEL,
            max_new_tokens=_GEMMA_MAX_NEW_TOKENS,
            temperature=0.1,
            top_p=0.9,
            return_full_text=False,
        )
        parsed = _extract_json_object(str(generated or ""))
        if not parsed:
            return None, 0.0, 0.0, [], [], []

        label = _normalize_label(parsed.get("label"))
        confidence = _clamp01(parsed.get("confidence"))
        score = _normalize_score(parsed.get("score"), label, confidence)
        positive_words = _normalize_terms(parsed.get("positive_words", []), limit=8)
        negative_words = _normalize_terms(parsed.get("negative_words", []), limit=8)
        key_terms = _normalize_terms(parsed.get("key_terms", []), limit=20)

        return label, score, confidence, positive_words, negative_words, key_terms

    return None, 0.0, 0.0, [], [], []


def _classify_document_with_sentiment_model(text: str) -> tuple[str, float, float]:
    with suppress(Exception):
        out = _client.text_classification(text, model=_SENTIMENT_MODEL)
        if out:
            top = out[0]
            label = _normalize_label(getattr(top, "label", "neutral"))
            confidence = _clamp01(getattr(top, "score", 0.0))
            score = _normalize_score(None, label, confidence)
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


def _classify_words_with_sentiment_model(text: str) -> tuple[List[str], List[str]]:
    positive_ranked: List[tuple[float, str]] = []
    negative_ranked: List[tuple[float, str]] = []

    for word in _extract_candidate_words(text):
        with suppress(Exception):
            out = _client.text_classification(word, model=_SENTIMENT_MODEL)
            if not out:
                continue
            top = out[0]
            label = _normalize_label(getattr(top, "label", "neutral"))
            confidence = _clamp01(getattr(top, "score", 0.0))
            if confidence < _WORD_SENTIMENT_CONFIDENCE_MIN:
                continue
            if label == "positive":
                positive_ranked.append((confidence, word))
            elif label == "negative":
                negative_ranked.append((confidence, word))

    positive_ranked.sort(key=lambda item: item[0], reverse=True)
    negative_ranked.sort(key=lambda item: item[0], reverse=True)

    return [w for _, w in positive_ranked[:8]], [w for _, w in negative_ranked[:8]]


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
    text = f"{(title or '').strip()} {(description or '').strip()}".strip()
    if not text:
        return SentimentTerms()

    # 1) Gemma-first inference for sentiment + extraction
    label = "neutral"
    score = 0.0
    confidence = 0.0
    positive_words: List[str] = []
    negative_words: List[str] = []
    extracted_terms: List[str] = []

    if _ENABLE_GEMMA_SENTIMENT or _ENABLE_GEMMA_EXTRACTION:
        (
            gemma_label,
            gemma_score,
            gemma_confidence,
            gemma_positive_words,
            gemma_negative_words,
            gemma_terms,
        ) = _gemma_sentiment_and_terms(text)

        if _ENABLE_GEMMA_SENTIMENT and gemma_label:
            label = gemma_label
            score = gemma_score
            confidence = gemma_confidence
            positive_words = gemma_positive_words
            negative_words = gemma_negative_words

        if _ENABLE_GEMMA_EXTRACTION and gemma_terms:
            extracted_terms = gemma_terms

    # 2) Fallback sentiment model if Gemma is disabled or uncertain
    if (not _ENABLE_GEMMA_SENTIMENT) or (confidence < _DOC_SENTIMENT_CONFIDENCE_MIN):
        fallback_label, fallback_score, fallback_confidence = _classify_document_with_sentiment_model(text)
        if fallback_confidence > confidence:
            label = fallback_label
            score = fallback_score
            confidence = fallback_confidence

    # 3) Extraction model fallback for key terms
    if not extracted_terms:
        extracted_terms = _extract_terms_with_keyphrase_model(text)

    # 4) Use extracted terms to strengthen positive/negative words
    if extracted_terms:
        term_positive, term_negative = _classify_words_with_sentiment_model(" ".join(extracted_terms))
        if not positive_words:
            positive_words = term_positive
        if not negative_words:
            negative_words = term_negative

    if not positive_words and not negative_words:
        positive_words, negative_words = _classify_words_with_sentiment_model(text)

    pos_count = len(positive_words)
    neg_count = len(negative_words)
    total_term_signal = pos_count + neg_count
    is_low_confidence = confidence < _DOC_SENTIMENT_CONFIDENCE_MIN
    is_weak_term_signal = 0 < total_term_signal < _TERM_SIGNAL_MIN_TOTAL
    is_near_balanced = (
        total_term_signal >= _TERM_SIGNAL_MIN_TOTAL
        and abs(pos_count - neg_count) <= _TERM_SIGNAL_BALANCE_DELTA
    )

    if is_low_confidence or is_weak_term_signal or is_near_balanced:
        label = "neutral"
        score = 0.0

    if label == "positive" and score < 0:
        score = abs(score)
    elif label == "negative" and score > 0:
        score = -abs(score)
    elif label == "neutral":
        score = 0.0

    confidence = _clamp01(confidence)
    score = max(-1.0, min(1.0, score))

    return SentimentTerms(
        label=label,
        score=score,
        confidence=confidence,
        positive_words=positive_words,
        negative_words=negative_words,
    )
