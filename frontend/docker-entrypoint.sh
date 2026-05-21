#!/bin/sh
set -e

export BACKEND_URL="${BACKEND_URL:-http://backend:8080}"
envsubst '${BACKEND_URL}' < /etc/nginx/nginx.conf.template > /etc/nginx/nginx.conf
exec "$@"
