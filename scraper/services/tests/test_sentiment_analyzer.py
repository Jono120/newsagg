from scraper.services import sentiment_analyzer as sa
from scraper.services import text_analyzer
from scraper.services import azure_text_analytics as ata
from scraper.services.sentiment_analyzer import SentimentTerms


def test_dispatcher_uses_azure_when_configured(monkeypatch) -> None:
    monkeypatch.setenv("ANALYZER_PROVIDER", "azure")
    monkeypatch.setattr(ata, "is_configured", lambda: True)

    sentinel = SentimentTerms(label="positive", score=0.8, confidence=0.9, key_phrases=["growth"])
    monkeypatch.setattr(ata, "analyze_text_sentiment_and_terms", lambda title, description="": sentinel)

    result = text_analyzer.analyze("Great news", "everything is wonderful")

    assert result is sentinel
    assert result.label == "positive"


def test_dispatcher_falls_back_to_hf_when_azure_unconfigured(monkeypatch) -> None:
    monkeypatch.setenv("ANALYZER_PROVIDER", "azure")
    monkeypatch.setattr(ata, "is_configured", lambda: False)

    hf_result = SentimentTerms(label="negative", score=-0.5, confidence=0.7)
    monkeypatch.setattr(sa, "analyze_text_sentiment_and_terms", lambda title, description="": hf_result)

    result = text_analyzer.analyze("Bad", "terrible outcome")

    assert result is hf_result


def test_dispatcher_falls_back_to_hf_on_azure_error(monkeypatch) -> None:
    monkeypatch.setenv("ANALYZER_PROVIDER", "azure")
    monkeypatch.setattr(ata, "is_configured", lambda: True)

    def boom(title, description=""):
        raise RuntimeError("azure unavailable")

    monkeypatch.setattr(ata, "analyze_text_sentiment_and_terms", boom)

    hf_result = SentimentTerms(label="neutral")
    monkeypatch.setattr(sa, "analyze_text_sentiment_and_terms", lambda title, description="": hf_result)

    result = text_analyzer.analyze("x", "y")

    assert result is hf_result


def test_dispatcher_huggingface_provider_uses_hf(monkeypatch) -> None:
    monkeypatch.setenv("ANALYZER_PROVIDER", "huggingface")

    hf_result = SentimentTerms(label="positive", score=0.3, confidence=0.6)
    monkeypatch.setattr(sa, "analyze_text_sentiment_and_terms", lambda title, description="": hf_result)

    result = text_analyzer.analyze("a", "b")

    assert result is hf_result


def test_azure_provider_maps_sdk_result(monkeypatch) -> None:
    class Scores:
        positive = 0.7
        neutral = 0.2
        negative = 0.1

    class Target:
        def __init__(self, text, sentiment):
            self.text = text
            self.sentiment = sentiment

    class Opinion:
        def __init__(self, target):
            self.target = target

    class Sentence:
        def __init__(self, opinions):
            self.mined_opinions = opinions

    class SentimentDoc:
        is_error = False
        sentiment = "positive"
        confidence_scores = Scores()
        sentences = [
            Sentence([Opinion(Target("growth", "positive")), Opinion(Target("risk", "negative"))])
        ]

    class KeyPhraseDoc:
        is_error = False
        key_phrases = ["economic growth", "market"]

    class Entity:
        def __init__(self, text):
            self.text = text

    class EntityDoc:
        is_error = False
        entities = [Entity("Apple"), Entity("New Zealand")]

    class FakeClient:
        def analyze_sentiment(self, documents, show_opinion_mining=False):
            return [SentimentDoc()]

        def extract_key_phrases(self, documents):
            return [KeyPhraseDoc()]

        def recognize_entities(self, documents):
            return [EntityDoc()]

    monkeypatch.setattr(ata, "_get_client", lambda: FakeClient())

    result = ata.analyze_text_sentiment_and_terms("Title", "Body text")

    assert result.label == "positive"
    assert result.score == round(0.7 - 0.1, 4)
    assert "growth" in result.positive_words
    assert "risk" in result.negative_words
    assert "economic growth" in result.key_phrases
    assert "Apple" in result.entities


def test_empty_text_is_neutral(monkeypatch) -> None:
    monkeypatch.setattr(ata, "_get_client", lambda: None)
    result = ata.analyze_text_sentiment_and_terms("", "")

    assert result.label == "neutral"
    assert result.score == 0.0
    assert result.confidence == 0.0
