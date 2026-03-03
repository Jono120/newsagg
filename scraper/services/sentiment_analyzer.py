import os
import re
from contextlib import suppress
from dataclasses import dataclass, field
from typing import List

from huggingface_hub import InferenceClient

_SENTIMENT_MODEL = os.getenv(
    "HF_SENTIMENT_MODEL",
    "distilbert/distilbert-base-uncased-finetuned-sst-2-english",
)
_EXTRACTION_MODEL = os.getenv("HF_EXTRACTION_MODEL", "ml6team/keyphrase-extraction-distilbert-inspec")
_HF_TOKEN = (
    os.getenv("HF_TOKEN")
    or os.getenv("HUGGINGFACEHUB_API_TOKEN")
    or os.getenv("HF_API_TOKEN")
)

_client = InferenceClient(token=_HF_TOKEN) if _HF_TOKEN else InferenceClient()

_WORD_SENTIMENT_CONFIDENCE_MIN = float(os.getenv("HF_WORD_SENTIMENT_CONFIDENCE_MIN", "0.7"))
_DOC_SENTIMENT_CONFIDENCE_MIN = float(os.getenv("HF_DOC_SENTIMENT_CONFIDENCE_MIN", "0.995"))
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
            label = str(top.label).lower()
            confidence = float(top.score)
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
        return []

    terms.sort(key=lambda item: item[0], reverse=True)
    return [w for _, w in terms[:limit]]


def analyze_text_sentiment_and_terms(title: str, description: str = "") -> SentimentTerms:
    text = f"{(title or '').strip()} {(description or '').strip()}".strip()
    if not text:
        return SentimentTerms()

    # 1) Sentiment pipeline
    label = "neutral"
    score = 0.0
    confidence = 0.0
    with suppress(Exception):
        out = _client.text_classification(text, model=_SENTIMENT_MODEL)
        if out:
            top = out[0]
            label = str(top.label).lower()
            score = float(top.score)
            confidence = score

    # 2) Keyphrase extraction + sentiment classification of extracted terms
    positive_words, negative_words = [], []
    extracted_terms = _extract_terms_with_keyphrase_model(text)
    if extracted_terms:
        positive_words, negative_words = _classify_words_with_sentiment_model(" ".join(extracted_terms))

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

    return SentimentTerms(
        label=label,
        score=score,
        confidence=confidence,
        positive_words=positive_words,
        negative_words=negative_words,
    )
