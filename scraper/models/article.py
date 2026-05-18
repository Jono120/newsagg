from datetime import datetime
from typing import List, Optional, Union
from dateutil import parser, tz

class Article:
    def __init__(
        self,
        title: str,
        description: str,
        url: str,
        source: str,
        category: str = "General",
        published_date: Optional[Union[datetime, str]] = None,
        content: str = '',
        sentiment_label: str = 'neutral',
        sentiment_score: float = 0.0,
        sentiment_confidence: float = 0.0,
        positive_words: Optional[List[str]] = None,
        negative_words: Optional[List[str]] = None
    ):
        self.title = title
        self.description = description
        self.url = url
        self.source = source
        self.category = category
        # default to UTC-aware datetime if not provided
        utc = tz.UTC
        if published_date is None:
            self.published_date = datetime.now(utc)
        else:
            self.published_date = published_date
        self.content = content
        self.sentiment_label = sentiment_label
        self.sentiment_score = sentiment_score
        self.sentiment_confidence = sentiment_confidence
        self.positive_words = positive_words or []
        self.negative_words = negative_words or []

    def to_dict(self):
        """Convert article to dictionary for API submission.

        Ensures `publishedDate` and `scrapedDate` are in New Zealand timezone
        with proper offset (+12:00 or +13:00 depending on DST), formatted as
        ISO 8601 strings that the backend can parse as DateTimeOffset.
        """
        pacific = tz.gettz("Pacific/Auckland")

        # Normalize input to an aware datetime
        dt: Optional[datetime] = None
        if isinstance(self.published_date, datetime):
            dt = self.published_date
        elif isinstance(self.published_date, str):
            try:
                dt = parser.parse(self.published_date)
            except (ValueError, TypeError, OverflowError):
                dt = None

        if dt is None:
            # fallback to current time in NZ
            dt = datetime.now(pacific)
        else:
            # If naive, assume UTC
            if dt.tzinfo is None:
                dt = dt.replace(tzinfo=tz.UTC)
            # Convert to NZ timezone (handles DST automatically)
            dt = dt.astimezone(pacific)

        # Format as ISO 8601 with NZ offset (e.g., "2026-05-06T14:30:00+12:00")
        # This preserves timezone info and is parseable by DateTimeOffset.Parse()
        pub_date = dt.isoformat()

        # ScrapedDate is always "now" in NZ timezone
        scraped_dt = datetime.now(pacific)
        scraped_date = scraped_dt.isoformat()

        return {
            "title": self.title,
            "description": self.description,
            "url": self.url,
            "source": self.source,
            "category": self.category,
            "publishedDate": pub_date,
            "scrapedDate": scraped_date,
            "content": self.content,
            "sentimentLabel": self.sentiment_label,
            "sentimentScore": self.sentiment_score,
            "sentimentConfidence": self.sentiment_confidence,
            "positiveWords": self.positive_words,
            "negativeWords": self.negative_words
        }

    def __repr__(self):
        return f"Article(title='{self.title[:50]}...', source='{self.source}')"
