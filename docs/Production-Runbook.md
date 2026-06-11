# Production Runbook (newzealandnorth)

This runbook finalizes the `newsagg-production` environment in `newzealandnorth` for operational reliability, observability, and rollback safety.

## Environment standard

- Azure region: `newzealandnorth`
- Resource group: `rg-newsagg-prod`
- GitHub environment: `newsagg-production`
- Deploy windows: Tuesday and Thursday, 10:00-11:00 NZST

## Monitoring and alerts

Enable Azure Monitor alerts for these MVP signals:

1. API uptime (`/api/health` and `/api/health/ready`) availability < 99% over 15m
2. Scraper refresh failures (`/api/scraper/refresh` non-2xx count > 3 in 15m)
3. Article ingestion volume drops (`POST /api/articles/batch` added count near zero for 24h)
4. Rate-limit saturation (`429` responses > 25 in 15m)
5. Signup funnel failures (`POST /api/growth/subscribe` 5xx > 5 in 15m)

## Rollback playbook

Use this when deployment causes regressions:

1. Pause traffic changes:
   - Disable paid campaign creatives that point to new release UTM tags.
   - Keep frontend online.
2. Identify last known good SHA:
   - In GitHub Actions, open the latest successful `newsagg-web`, `newsagg-func`, and `newsagg-ui` runs.
3. Redeploy previous artifact:
   - Re-run each successful workflow using the known-good commit.
4. Validate health:
   - `GET /api/health`
   - `GET /api/health/ready`
   - `GET /api/growth/summary?days=7`
5. Resume campaigns only after:
   - Health/readiness endpoints are stable.
   - Signup endpoint is returning 200/409 successfully.

## Smoke-test checklist after every deploy

- [ ] `https://newsagg-web.azurewebsites.net/docs` loads Swagger
- [ ] `https://newsagg-web.azurewebsites.net/api/health/ready` returns 200
- [ ] `POST /api/growth/events` accepts page-view event
- [ ] `POST /api/growth/subscribe` stores a test signup
- [ ] `GET /api/growth/scale-gate?days=30&adSpendNzd=300` returns decision payload
