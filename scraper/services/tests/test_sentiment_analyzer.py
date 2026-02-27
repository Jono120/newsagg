from scraper.services import sentiment_analyzer as sa


def test_empty_title_is_neutral() -> None:
    result = sa.analyze_title_sentiment("")
    assert result.label == "neutral"
    assert result.score == 0.0
    assert result.confidence == 0.0


def test_positive_scraper_headline() -> None:
    title = "Tech company reports strong growth and record high revenue"
    result = sa.analyze_title_sentiment(title)

    assert result.label == "positive"
    assert result.score > 0
    assert 0.0 <= result.confidence <= 1.0


def test_negative_scraper_headline() -> None:
    title = "Bank files bankruptcy after data breach and fraud investigation"
    result = sa.analyze_title_sentiment(title)

    assert result.label == "negative"
    assert result.score < 0
    assert 0.0 <= result.confidence <= 1.0


def test_neutral_scraper_headline() -> None:
    title = "Company announces a new office opening in Seattle"
    result = sa.analyze_title_sentiment(title)

    assert result.label == "neutral"
    assert result.score == 0.0


def test_hf_signal_blends_with_rule_score(monkeypatch) -> None:
    # Force HF to dominate so behavior is deterministic.
    monkeypatch.setattr(sa, "HF_BLEND_WEIGHT", 1.0)
    monkeypatch.setattr(sa, "_hf_sentiment_signal", lambda _title: (-0.95, 0.95))

    title = "Company reports strong growth and record high profits"
    result = sa.analyze_title_sentiment(title)

    assert result.label == "negative"
    assert result.score < 0
    assert 0.0 <= result.confidence <= 1.0