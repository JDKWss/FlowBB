#!/bin/sh
set -eu

api_url="${VITE_API_URL:-${FLOWBB_DEFAULT_API_URL:-http://localhost:8080}}"

case "$api_url" in
  *"
"* | *""*)
    echo "VITE_API_URL must not contain line breaks." >&2
    exit 1
    ;;
esac

escaped_api_url=$(printf '%s' "$api_url" | sed 's/\\/\\\\/g; s/"/\\"/g')

printf 'window.__FLOWBB_CONFIG__ = Object.freeze({ API_URL: "%s" });\n' \
  "$escaped_api_url" \
  > /usr/share/nginx/html/runtime-config.js

