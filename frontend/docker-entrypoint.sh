#!/bin/sh
set -eu

# Write the runtime env file the SPA loads via <script src="/env.js">.
# BACKEND_URL empty (default) = same origin: the SPA calls /api on this host and
# nginx proxies it to the backend. Set it only when the API lives on another origin.
#
# The file goes to /tmp (writable by the unprivileged nginx user, uid 101);
# nginx.conf serves it at /env.js.
BACKEND_URL="${BACKEND_URL:-}"

# Escape backslashes and double quotes so the value is a valid JS string literal.
escaped=$(printf '%s' "$BACKEND_URL" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g')
printf 'window.__BACKEND_URL = "%s";\n' "$escaped" > /tmp/env.js

exec "$@"
