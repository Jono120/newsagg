FROM python:3.10-slim

WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy requirements
COPY requirements.txt .

# Install Python dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Copy scraper code
COPY . .

# Health check
HEALTHCHECK --interval=30s --timeout=5s --retries=2 \
  CMD curl -f http://localhost:9000/ || exit 1

# Run scraper
CMD ["python", "-u", "main.py"]
