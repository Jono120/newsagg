# Deployment Guide

This guide covers deploying the News Aggregator using Docker Compose, which includes PocketBase, the backend API, and the frontend.

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
- **PocketBase** on `http://localhost:8090` (Admin UI: `/app/`)
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
newsagg-pocketbase-1          Up 2 minutes
newsagg-backend-1             Up 2 minutes
newsagg-frontend-1            Up 2 minutes
newsagg-scraper-1             Up 2 minutes
```

### 4. First-Time Setup

#### Access PocketBase Admin Panel

1. Open http://localhost:8090/app/
2. Create an admin account (username/password)
3. Create a collection named `articles` with these fields:
   - `title` (Text, required)
   - `description` (Text)
   - `url` (URL, required)
   - `source` (Text, required)
   - `category` (Text)
   - `publishedDate` (DateTime)
   - `scrapedDate` (DateTime, auto-set)

**Alternatively**, the backend will create the collection automatically on first run if it doesn't exist.

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

upstream pocketbase {
    server localhost:8090;
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

    # PocketBase (admin & API)
    location /api/pocketbase/ {
        proxy_pass http://pocketbase/;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
    }

    location /app/ {
        proxy_pass http://pocketbase/app/;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
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
# PocketBase
POCKETBASE_URL=http://localhost:8090
POCKETBASE_COLLECTION=articles

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
      - PocketBase__BaseUrl=${POCKETBASE_URL}
```

---

## Monitoring & Logs

### View Service Logs

```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f backend
docker-compose logs -f pocketbase

# Follow scraper logs
docker-compose logs -f scraper
```

### PocketBase Admin Panel

Access at: http://localhost:8090/app/
- Monitor article collections
- Manage user authentication
- View database statistics

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
- **PocketBase data** - `newsagg_pocketbase_data`
- **Frontend** - stateless
- **Backend** - stateless
- **Scraper** - stateless

To back up your data:
```bash
docker volume inspect newsagg_pocketbase_data
# Copy the path to your backup location
```

---

## Troubleshooting

### Backend can't connect to PocketBase
- Ensure PocketBase is running: `docker-compose ps`
- Check logs: `docker-compose logs pocketbase`
- Verify `POCKETBASE_URL` is correct in backend environment

### Articles not appearing
- Check scraper logs: `docker-compose logs scraper`
- Verify PocketBase `articles` collection exists
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
2. **Configure authentication** - Set up user accounts in PocketBase
3. **Setup SSL/TLS** - Use Let's Encrypt for HTTPS
4. **Enable backups** - Schedule PocketBase volume backups
5. **Monitor performance** - Track article ingestion and API response times

---

## Support

For issues or questions:
- Check logs: `docker-compose logs`
- Review [API Documentation](./APIDoc.md)
- See [Quickstart Guide](./Quickstart.md)
