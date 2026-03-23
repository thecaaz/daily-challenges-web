#!/bin/sh
set -e

# Write runtime env file for SPA to pick up
BACKEND_URL=${BACKEND_URL:-http://backend:5000}
cat > /usr/share/nginx/html/env.js <<EOF
window.__BACKEND_URL = "${BACKEND_URL}";
EOF

exec "$@"
