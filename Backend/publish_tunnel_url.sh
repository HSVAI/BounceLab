#!/usr/bin/env bash
set -euo pipefail
for attempt in 1 2 3 4 5 6; do
  api_url=$(journalctl --user -u bouncelab-maps-tunnel.service -n 120 --output=cat \
    | grep -Eo 'https://[a-z0-9-]+\.trycloudflare\.com' | tail -n 1 || true)
  test -n "$api_url" && break
  sleep 5
done
test -n "${api_url:-}"
new_content=$(printf '{"baseUrl":"%s"}\n' "$api_url")
current_json=$(/usr/bin/gh api repos/ghtnql/BounceLab/contents/docs/api.json 2>/dev/null || true)
current_content=$(printf '%s' "$current_json" | /usr/bin/jq -r '.content // ""' | tr -d '\n' | base64 -d 2>/dev/null || true)
test "$new_content" = "$current_content" && exit 0
encoded=$(printf '%s' "$new_content" | base64 -w 0)
sha=$(printf '%s' "$current_json" | /usr/bin/jq -r '.sha // empty')
args=(--method PUT repos/ghtnql/BounceLab/contents/docs/api.json
      -f message='Update community maps API endpoint' -f content="$encoded" -f branch=main)
test -n "$sha" && args+=(-f sha="$sha")
/usr/bin/gh api "${args[@]}" >/dev/null
