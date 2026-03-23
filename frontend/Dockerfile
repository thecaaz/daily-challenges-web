FROM node:20-alpine AS build
WORKDIR /app

# Install deps
COPY frontend/package*.json ./
RUN npm ci --silent

# Copy source and build
COPY frontend/ .
RUN npm run build

FROM nginx:stable-alpine
COPY --from=build /app/dist /usr/share/nginx/html

# Use custom nginx config to enable SPA fallback and proxying to backend inside Docker
COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf

# Copy entrypoint that generates runtime env.js (window.__BACKEND_URL)
COPY frontend/docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh
RUN sed -i 's/\r$//' /docker-entrypoint.sh || true

EXPOSE 80
ENTRYPOINT ["/docker-entrypoint.sh"]
CMD ["nginx", "-g", "daemon off;"]
