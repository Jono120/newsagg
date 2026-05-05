# GitHub Actions Workflows

This directory contains optional GitHub Actions workflows for automated Docker image building and deployment.

## Available Workflows

### `docker-build.yml` :white_check_mark: (Recommended)

**Status**: Enabled by default
**Purpose**: Build Docker images and save as artifacts

**Features**:
- Builds all three images: backend, frontend, scraper
- Saves images as GitHub artifacts
- No external dependencies (no Docker Hub account required)
- Artifacts retained for 7 days

**When it runs**:
- On push to `main` or `develop` branches
- When changes are made to `backend/`, `frontend/`, `scraper/`, or `docker-compose.yml`

**Cost**: Free tier included

---

### `docker-push-hub.yml.disabled` (Optional)

**Status**: Disabled by default (rename to enable)
**Purpose**: Automatically push images to Docker Hub

**Features**:
- Builds and pushes all three images to Docker Hub
- Tags with git commit SHA for version tracking
- Creates deployment summary in GitHub Actions

**When it runs** (after enabling):
- On push to `main` branch only
- When changes are made to `backend/`, `frontend/`, `scraper/`, or `docker-compose.yml`

**Requirements**:
1. Docker Hub account (free tier available)
2. GitHub Secrets configured:
   - `DOCKER_USERNAME`: Your Docker Hub username
   - `DOCKER_PASSWORD`: Docker Hub Personal Access Token

**Cost**: Free tier available on Docker Hub

---

## How to Enable Docker Hub Push Workflow

### 1. Create Docker Hub Account (if needed)
Visit [Docker Hub](https://hub.docker.com) and create a free account.

### 2. Generate Personal Access Token
- Log in to Docker Hub
- Go to Account Settings > Security
- Create a new Personal Access Token
- Copy the token (you won't see it again)

### 3. Add GitHub Secrets
- In your GitHub repository: Settings > Secrets and variables > Actions
- Click "New repository secret"
- Add two secrets:
  - Name: `DOCKER_USERNAME` | Value: `your-docker-hub-username`
  - Name: `DOCKER_PASSWORD` | Value: `your-personal-access-token`

### 4. Enable the Workflow
Rename the file in your repository:
```bash
git mv .github/workflows/docker-push-hub.yml.disabled .github/workflows/docker-push-hub.yml
git push
```

### 5. Update docker-compose.yml
Change your docker-compose.yml to use your Docker Hub images:
```yaml
services:
  backend:
    image: your-username/newsagg-backend:latest
  frontend:
    image: your-username/newsagg-frontend:latest
  scraper:
    image: your-username/newsagg-scraper:latest
```

---

## Manual Deployment

If you prefer not to use GitHub Actions:

### Option 1: Local Build and Deploy
```bash
# Clone repository
git clone https://github.com/yourusername/newsagg.git
cd newsagg

# Start with docker-compose (builds locally)
docker-compose up -d
```

### Option 2: Use GitHub Artifacts
1. Wait for `docker-build.yml` workflow to complete
2. Download the artifact from GitHub Actions
3. Extract images locally:
   ```bash
   docker load -i backend.tar
   docker load -i frontend.tar
   docker load -i scraper.tar
   ```
4. Deploy:
   ```bash
   docker-compose up -d
   ```

---

## Monitoring Workflow Runs

Check workflow status in GitHub:
- Click "Actions" tab in your repository
- Select the workflow to see run details
- Check logs for any issues

---

## Troubleshooting

### Workflow failed to build
- Check the "Jobs" tab in workflow details
- Review error messages in the build step
- Ensure Dockerfile syntax is correct
- Verify dependencies are available

### Docker push failed
- Verify Docker Hub credentials in GitHub Secrets
- Check Docker Hub account status
- Ensure Personal Access Token hasn't expired

---

## Next Steps

Choose your deployment strategy and go live!

- **Simple**: Just run `docker-compose up -d` locally
- **Automated**: Enable docker-push-hub.yml for automated Docker Hub pushes
- **Hybrid**: Use docker-build.yml for builds + manual deployment
