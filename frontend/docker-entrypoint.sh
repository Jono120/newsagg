#!/bin/sh
set -e

export BACKEND_URL="${BACKEND_URL:-http://backend:8080}"
escaped_backend_url=$(printf '%s' "$BACKEND_URL" | sed 's/[&|\\]/\\&/g')
sed "s|\${BACKEND_URL}|$escaped_backend_url|g" /etc/nginx/nginx.conf.template > /etc/nginx/nginx.conf
exec "$@"
