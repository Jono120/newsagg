# Deployment Guide

This guide covers deploying the News Aggregator using Docker Compose for local orchestration and an Azure Web App + Function App deployment backed by PostgreSQL and Key Vault.

## Prerequisites

- Docker & Docker Compose installed
- Git (to clone the repository)
- ~2GB disk space for the full stack

## Quick Start with Docker Compose

### 1. Clone the Repository

```bash
git clone https://github.com/yourusername/newsagg.git
cd newsagg
```

### 2. Start the Full Stack

```bash
docker-compose up -d
```

This will start:
- **PostgreSQL** on `localhost:5432`
- **Backend API** on `http://localhost:5000`
- **Frontend** on `http://localhost:3000`
- **Scraper** (runs automatically on schedule)

### 3. Verify Services

Check that all services are running:

```bash
docker-compose ps
```

Expected output:
```
NAME                           STATUS
newsagg-postgres-1           Up 2 minutes
newsagg-backend-1             Up 2 minutes
newsagg-frontend-1            Up 2 minutes
newsagg-scraper-1             Up 2 minutes
```

### 4. First-Time Setup

#### Prepare PostgreSQL

1. Ensure PostgreSQL is reachable from the backend container.
2. Create the `newsagg` database if it does not already exist.
3. Let the backend create the `articles` table automatically on first run.

#### Test the Backend

```bash
# Get all articles
curl http://localhost:5000/api/articles

# Get API health
curl http://localhost:5000/api/health

# View Swagger docs
open http://localhost:5000/swagger
```

#### View the Frontend

Open http://localhost:3000 in your browser to see articles as they're scraped.

---

## Deployment Options

### Option 1: Self-Hosted on VPS

Deploy to a Linux VPS (DigitalOcean, Linode, etc.):

1. **SSH into your server**
   ```bash
   ssh root@your.server.ip
   ```

2. **Install Docker**
   ```bash
   curl -fsSL https://get.docker.com -o get-docker.sh
   sudo sh get-docker.sh
   sudo usermod -aG docker $USER
   ```

3. **Clone repository**
   ```bash
   git clone https://github.com/yourusername/newsagg.git
   cd newsagg
   ```

4. **Create environment configuration** (optional)
   ```bash
   cp .env.example .env
   # Edit .env with your domain, port, etc.
   nano .env
   ```

5. **Start services**
   ```bash
   docker-compose up -d
   ```

6. **Setup reverse proxy (Nginx)**
   ```bash
   # Install nginx
   sudo apt update && sudo apt install -y nginx

   # Create nginx config (see example below)
   sudo nano /etc/nginx/sites-available/newsagg
   sudo ln -s /etc/nginx/sites-available/newsagg /etc/nginx/sites-enabled/
   sudo nginx -t
   sudo systemctl restart nginx
   ```

#### Nginx Configuration Example

```nginx
upstream backend {
    server localhost:5000;
}

upstream frontend {
    server localhost:3000;
}

server {
    listen 80;
    server_name yourdomain.com;

    # Frontend
    location / {
        proxy_pass http://frontend;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }

    # Backend API
    location /api/ {
        proxy_pass http://backend;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }

}
```

7. **Enable SSL (optional but recommended)**
   ```bash
   sudo apt install -y certbot python3-certbot-nginx
   sudo certbot --nginx -d yourdomain.com
   ```

### Option 2: Docker Hub Automated Builds

Push your Docker images to Docker Hub for easier deployment:

1. **Build and tag images**
   ```bash
   docker build -t yourusername/newsagg-backend ./backend
   docker build -t yourusername/newsagg-scraper ./scraper
   ```

2. **Push to Docker Hub**
   ```bash
   docker push yourusername/newsagg-backend
   docker push yourusername/newsagg-scraper
   ```

3. **Update docker-compose.yml** to use your images:
   ```yaml
   services:
     backend:
       image: yourusername/newsagg-backend:latest
     scraper:
       image: yourusername/newsagg-scraper:latest
   ```

### Option 3: Lightweight Cloud Platforms

Popular free/cheap options for lightweight deployments:

- **Railway.app** - Free tier, easy GitHub integration
- **Render** - Free tier with auto-deploy
- **Fly.io** - Affordable, global deployment
- **DigitalOcean App Platform** - $5-12/month
- **AWS EC2 + Free Tier** - Free for 12 months

Each platform has Docker support; refer to their documentation for deployment.

---

## Environment Variables

Create a `.env` file in the repository root:

```bash
# PostgreSQL
POSTGRES_DB=newsagg
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
POSTGRES_PORT=5432
CONNECTIONSTRINGS__NEWSAGGREGATOR=Host=postgres;Port=5432;Database=newsagg;Username=postgres;Password=postgres

# Backend
BACKEND_PORT=5000
ASPNETCORE_ENVIRONMENT=Production

# Frontend
FRONTEND_PORT=3000
VITE_API_URL=http://localhost:5000

# Scraper
SCRAPER_INTERVAL_MINUTES=30
API_BASE_URL=http://localhost:5000
```

Load these in `docker-compose.yml`:
```yaml
services:
  backend:
    environment:
      - ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}
      - ConnectionStrings__NewsAggregator=${CONNECTIONSTRINGS__NEWSAGGREGATOR}
```

---

## Monitoring & Logs

### View Service Logs

```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f backend
docker-compose logs -f postgres

# Follow scraper logs
docker-compose logs -f scraper
```

### Database

- Monitor article rows using your preferred PostgreSQL client
- Inspect database health and connectivity

### Backend Health Check

```bash
curl http://localhost:5000/api/health
```

---

## Stopping & Restarting

```bash
# Stop all services (keep data)
docker-compose stop

# Start services again
docker-compose start

# Restart a specific service
docker-compose restart backend

# Remove all containers (keeps volumes/data)
docker-compose down

# Remove everything including data
docker-compose down -v
```

---

## Data Persistence

By default, volumes are created for:
- **PostgreSQL data** - `newsagg_postgres_data`
- **Frontend** - stateless
- **Backend** - stateless
- **Scraper** - stateless

To back up your data:
```bash
docker volume inspect newsagg_postgres_data
# Copy the path to your backup location
```

---

## Troubleshooting

### Backend can't connect to PostgreSQL
- Ensure PostgreSQL is running: `docker-compose ps`
- Check logs: `docker-compose logs postgres`
- Verify `ConnectionStrings__NewsAggregator` is correct in backend environment

### Articles not appearing
- Check scraper logs: `docker-compose logs scraper`
- Verify the `articles` table exists and the database is reachable
- Check backend health: `curl http://localhost:5000/api/health`

### Port already in use
- Change ports in `docker-compose.yml`:
  ```yaml
  services:
    backend:
      ports:
        - "5001:5000"  # Changed to 5001
  ```

### Disk space issues
- Clean up unused Docker images: `docker image prune -a`
- Remove unused volumes: `docker volume prune`

---

## Next Steps

1. **Customize scrapers** - See [Adding News Sources](./Frontend_Integration.md)
2. **Configure authentication** - Set up user accounts in your application or identity provider
3. **Setup SSL/TLS** - Use Let's Encrypt for HTTPS
4. **Enable backups** - Schedule PostgreSQL volume backups
5. **Monitor performance** - Track article ingestion and API response times

---

## Support

For issues or questions:
- Check logs: `docker-compose logs`
- Review [API Documentation](./APIDoc.md)
- See [Quickstart Guide](./Quickstart.md)
