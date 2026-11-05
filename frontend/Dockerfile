# Build context: repository root (see docker-compose.yml / release workflow).
# The build stage mirrors the repo layout (/src/frontend + /src/extension)
# so imports like '../../../extension/updates.json' from frontend/src resolve.
FROM node:22-alpine AS build
WORKDIR /src/frontend

# Install deps first so this layer is cached until the lockfile changes.
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci --no-audit --no-fund

# Shared with the extension (updates.json released version etc.); small, so copy the whole folder.
COPY extension/ /src/extension/
COPY frontend/ ./
RUN npm run build

# Runtime: unprivileged nginx (runs as uid 101, listens on 8080).
FROM nginxinc/nginx-unprivileged:stable-alpine
COPY --from=build /src/frontend/dist /usr/share/nginx/html

# SPA fallback, /api reverse proxy to the backend service, cache + security headers.
COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf

# Writes the runtime env.js (window.__BACKEND_URL) to /tmp/env.js, which nginx serves at /env.js.
COPY --chmod=755 frontend/docker-entrypoint.sh /usr/local/bin/app-entrypoint.sh

EXPOSE 8080
ENTRYPOINT ["/usr/local/bin/app-entrypoint.sh"]
CMD ["nginx", "-g", "daemon off;"]
