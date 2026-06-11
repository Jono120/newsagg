FROM python:3.14.6-slim

WORKDIR /app

ENV PYTHONUNBUFFERED=1

COPY scraper/requirements.txt ./scraper/
RUN pip install --no-cache-dir -r scraper/requirements.txt

COPY scraper/ ./scraper/

WORKDIR /app/scraper

CMD ["python", "main.py", "--scheduled"]