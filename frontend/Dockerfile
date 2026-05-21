FROM node:22-alpine AS build
WORKDIR /app

COPY frontend/package*.json ./
RUN npm install

COPY frontend/ ./
RUN npm run build

FROM nginx:1.29-alpine
RUN apk add --no-cache gettext

COPY frontend/nginx.conf.template /etc/nginx/nginx.conf.template
COPY frontend/docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh
COPY --from=build /app/dist /usr/share/nginx/html

ENV BACKEND_URL=http://backend:8080
EXPOSE 80

ENTRYPOINT ["/docker-entrypoint.sh"]
CMD ["nginx", "-g", "daemon off;"]
