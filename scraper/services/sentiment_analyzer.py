import json
import os
import re
from contextlib import suppress
from dataclasses import dataclass, field
from typing import List

from huggingface_hub import InferenceClient

_SENTIMENT_MODEL = os.getenv("HF_SENTIMENT_MODEL", "cardiffnlp/twitter-roberta-base-sentiment-latest")
_EXTRACTION_MODEL = os.getenv("HF_EXTRACTION_MODEL", "google/flan-t5-base")
_HF_TOKEN = (
    os.getenv("HF_TOKEN")
    or os.getenv("HUGGINGFACEHUB_API_TOKEN")
    or os.getenv("HF_API_TOKEN")
)

_client = InferenceClient(token=_HF_TOKEN) if _HF_TOKEN else InferenceClient()

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


def _parse_json_block(text: str) -> dict:
    m = re.search(r"\{.*\}", text, re.DOTALL)
    if not m:
        return {}
    with suppress(json.JSONDecodeError, TypeError, ValueError):
        return json.loads(m.group(0))
    return {}


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
            if confidence < 0.6:
                continue
            if label == "positive":
                positive_ranked.append((confidence, word))
            elif label == "negative":
                negative_ranked.append((confidence, word))

    positive_ranked.sort(key=lambda item: item[0], reverse=True)
    negative_ranked.sort(key=lambda item: item[0], reverse=True)

    return [w for _, w in positive_ranked[:8]], [w for _, w in negative_ranked[:8]]


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

    # 2) Inference pipeline for term extraction
    positive_words, negative_words = [], []
    prompt = (
        "Extract sentiment-bearing words from this news text.\n"
        "Return STRICT JSON only with keys: positive_words, negative_words.\n"
        "Each value must be an array of unique lowercase words.\n\n"
        f"TEXT: {text}"
    )
    with suppress(Exception):
        gen = _client.text_generation(prompt, model=_EXTRACTION_MODEL, max_new_tokens=180)
        data = _parse_json_block(gen or "")
        positive_words = list(dict.fromkeys(data.get("positive_words", [])))
        negative_words = list(dict.fromkeys(data.get("negative_words", [])))

    if not positive_words and not negative_words:
        positive_words, negative_words = _classify_words_with_sentiment_model(text)

    return SentimentTerms(
        label=label,
        score=score,
        confidence=confidence,
        positive_words=positive_words,
        negative_words=negative_words,
    )
