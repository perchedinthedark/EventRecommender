#!/usr/bin/env bash
# Smoke tests for EventRecommender (ASP.NET + React)
# Usage: bash smoke.sh
# Env overrides (optional):
#   BASE_URL=http://localhost:5210 EVENT_ID=1 bash smoke.sh

set +e  # don't exit on first failure
export LC_ALL=C

BASE_URL="${BASE_URL:-http://localhost:5210}"
EVENT_ID="${EVENT_ID:-}"           # if empty, we'll pick from /api/trending
COOK1="cookie1.txt"
COOK2="cookie2.txt"
TMPDIR="$(mktemp -d)"

# start clean (avoid stale cookies from earlier runs)
rm -f "$COOK1" "$COOK2"

# generate unique emails so the script is idempotent
STAMP="$(date +%s)$RANDOM"
U1_EMAIL="smoke1+$STAMP@example.com"
U2_EMAIL="smoke2+$STAMP@example.com"
PASSWORD="P@ssw0rd!"

PASS=0
FAIL=0

green() { printf "\033[32m%s\033[0m" "$1"; }
red()   { printf "\033[31m%s\033[0m" "$1"; }
info()  { printf "• %s\n" "$1"; }
ok()    { PASS=$((PASS+1)); printf "  %s %s\n" "$(green '✅')" "$1"; }
bad()   { FAIL=$((FAIL+1)); printf "  %s %s\n" "$(red '❌')" "$1"; }

# ---- helpers ----
# Extract HTTP body from a curl -i response, robust to \r\n
http_body() {
  tr -d '\r' | awk 'BEGIN{p=0} /^$/{p=1; next} p{print}'
}
body_of() { echo "$1" | http_body; }

get() {
  local url="$1" cookies="$2"
  if [ -n "$cookies" ]; then
    curl -sS -i -b "$cookies" "$url"
  else
    curl -sS -i "$url"
  fi
}

# post_json can:
# - just post JSON
# - send cookies (-b)
# - save cookies (-c)
# - do both (send & save) when both params are provided
post_json() {
  local url="$1" json="$2" cookies="$3" savecookies="$4"
  if [ -n "$cookies" ] && [ -n "$savecookies" ]; then
    curl -sS -i -H "Content-Type: application/json" -d "$json" -b "$cookies" -c "$savecookies" "$url"
  elif [ -n "$savecookies" ]; then
    curl -sS -i -H "Content-Type: application/json" -d "$json" -c "$savecookies" "$url"
  elif [ -n "$cookies" ]; then
    curl -sS -i -H "Content-Type: application/json" -d "$json" -b "$cookies" "$url"
  else
    curl -sS -i -H "Content-Type: application/json" -d "$json" "$url"
  fi
}

expect_status() {
  local expected="$1" actual_headers="$2" label="$3"
  echo "$actual_headers" | head -n 1 | grep -q " $expected "
  if [ $? -eq 0 ]; then ok "$label"; else bad "$label (expected $expected)"; fi
}

json_field() {
  local key="$1"
  sed -nE "s/.*\"$key\"[[:space:]]*:[[:space:]]*\"?([^\",}]+)\"?.*/\1/p" | head -n 1
}

pick_event_id_from_trending() {
  echo "$1" | sed -nE 's/.*"id"[[:space:]]*:[[:space:]]*([0-9]+).*/\1/p' | head -n 1
}

# ---- 0) health checks ----
info "Base URL: $BASE_URL"
RESP="$(get "$BASE_URL/" "")"
expect_status "200" "$RESP" "Root reachable"

RESP="$(get "$BASE_URL/api/trending?perList=3&categoriesToShow=1" "")"
expect_status "200" "$RESP" "GET /api/trending (public)"

if [ -z "$EVENT_ID" ]; then
  BODY="$(body_of "$RESP")"
  EVENT_ID="$(echo "$BODY" | pick_event_id_from_trending)"
  if [ -z "$EVENT_ID" ]; then
    info "Could not pick event id from trending; defaulting to 1"
    EVENT_ID=1
  fi
fi
info "Using EVENT_ID=$EVENT_ID"

# ---- 1) auth flow ----
RESP="$(post_json "$BASE_URL/api/auth/register" "{\"email\":\"$U1_EMAIL\",\"password\":\"$PASSWORD\"}" "" "$COOK1")"
expect_status "200" "$RESP" "POST /api/auth/register (user1)"

RESP="$(get "$BASE_URL/api/auth/me" "$COOK1")"
expect_status "200" "$RESP" "GET /api/auth/me (user1)"
BODY="$(body_of "$RESP")"
USER1_ID="$(echo "$BODY" | json_field "id")"
info "user1: $U1_EMAIL (id=$USER1_ID)"

RESP="$(post_json "$BASE_URL/api/auth/register" "{\"email\":\"$U2_EMAIL\",\"password\":\"$PASSWORD\"}" "" "$COOK2")"
expect_status "200" "$RESP" "POST /api/auth/register (user2)"

RESP="$(get "$BASE_URL/api/auth/me" "$COOK2")"
expect_status "200" "$RESP" "GET /api/auth/me (user2)"
BODY="$(body_of "$RESP")"
USER2_ID="$(echo "$BODY" | json_field "id")"
info "user2: $U2_EMAIL (id=$USER2_ID)"

# send AND save cookies so server knows who to log out and jar gets updated
RESP="$(post_json "$BASE_URL/api/auth/logout" "{}" "$COOK1" "$COOK1")"
expect_status "200" "$RESP" "POST /api/auth/logout (user1)"

RESP="$(get "$BASE_URL/api/auth/me" "$COOK1")"
expect_status "401" "$RESP" "GET /api/auth/me after logout (user1)"

RESP="$(post_json "$BASE_URL/api/auth/login" "{\"email\":\"$U1_EMAIL\",\"password\":\"$PASSWORD\"}" "" "$COOK1")"
expect_status "200" "$RESP" "POST /api/auth/login (user1)"

# ---- 2) events & trending ----
RESP="$(get "$BASE_URL/api/events/$EVENT_ID" "")"
expect_status "200" "$RESP" "GET /api/events/{id}"

# ---- 3) status + rating (auth) ----
RESP="$(post_json "$BASE_URL/api/events/$EVENT_ID/status" '{"status":"Interested"}' "$COOK1" "")"
expect_status "200" "$RESP" "POST /api/events/{id}/status Interested"

RESP="$(post_json "$BASE_URL/api/events/$EVENT_ID/status" '{"status":"Going"}' "$COOK1" "")"
expect_status "200" "$RESP" "POST /api/events/{id}/status Going"

RESP="$(post_json "$BASE_URL/api/events/$EVENT_ID/rating" '{"rating":5}' "$COOK1" "")"
expect_status "200" "$RESP" "POST /api/events/{id}/rating 5"

RESP="$(get "$BASE_URL/api/events/$EVENT_ID/me" "$COOK1")"
expect_status "200" "$RESP" "GET /api/events/{id}/me"

# ---- 4) telemetry (anonymous ok) ----
RESP="$(post_json "$BASE_URL/api/telemetry/clicks" "{\"eventId\":$EVENT_ID}" "" "")"
expect_status "200" "$RESP" "POST /api/telemetry/clicks"

RESP="$(post_json "$BASE_URL/api/telemetry/dwell" "{\"eventId\":$EVENT_ID,\"dwellMs\":4200}" "" "")"
expect_status "200" "$RESP" "POST /api/telemetry/dwell"

# ---- 5) seed + train + recs ----
RESP="$(get "$BASE_URL/Admin/Seed" "$COOK1")"
expect_status "302" "$RESP" "GET /Admin/Seed (redirect is OK)"

RESP="$(get "$BASE_URL/Admin/Train" "$COOK1")"
echo "$RESP" | head -n 1 | grep -Eq " 302 | 200 " && ok "GET /Admin/Train" || bad "GET /Admin/Train"

RESP="$(get "$BASE_URL/api/recs?topN=6" "$COOK1")"
expect_status "200" "$RESP" "GET /api/recs?topN=6"

# ---- 6) social ----
RESP="$(post_json "$BASE_URL/api/social/follow" "{\"followeeId\":\"$USER2_ID\"}" "$COOK1" "")"
expect_status "200" "$RESP" "POST /api/social/follow (u1 -> u2)"

RESP="$(get "$BASE_URL/api/social/following" "$COOK1")"
expect_status "200" "$RESP" "GET /api/social/following (user1)"

RESP="$(get "$BASE_URL/api/social/followers" "$COOK2")"
expect_status "200" "$RESP" "GET /api/social/followers (user2)"

RESP="$(post_json "$BASE_URL/api/events/$EVENT_ID/status" '{"status":"Going"}' "$COOK2" "")"
expect_status "200" "$RESP" "user2 Going on event"

RESP="$(get "$BASE_URL/api/social/friends-going?eventId=$EVENT_ID" "$COOK1")"
expect_status "200" "$RESP" "GET /api/social/friends-going (user1)"

# ---- 7) users search ----
RESP="$(get "$BASE_URL/api/users/search?q=smoke2&limit=10" "$COOK1")"
expect_status "200" "$RESP" "GET /api/users/search (user1)"

# ---- summary ----
echo
echo "=================="
printf "%s Passed: %s   Failed: %s\n" "Summary →" "$(green $PASS)" "$(red $FAIL)"
echo "=================="

# cleanup temp (keep cookies for debugging if you like)
rm -rf "$TMPDIR"
# comment out the next two lines if you want to inspect cookies after run
rm -f "$COOK1" "$COOK2"

# exit code reflects failures (>0 if any failed)
[ "$FAIL" -eq 0 ]
