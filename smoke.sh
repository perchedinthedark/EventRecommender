#!/usr/bin/env bash
# EventRecommender full smoke (~30 checks)
# Run: bash smoke.sh
# Optional: BASE_URL=http://localhost:5243 bash smoke.sh

set +e
export LC_ALL=C

BASE_URL="${BASE_URL:-http://localhost:5210}"
COOK1="cookie1.txt"
COOK2="cookie2.txt"
rm -f "$COOK1" "$COOK2"

STAMP="$(date +%s)$RANDOM"
U1_EMAIL="smoke1+$STAMP@example.com"
U2_EMAIL="smoke2+$STAMP@example.com"
PASSWORD="P@ssw0rd!"

PASS=0
FAIL=0

green(){ printf "\033[32m%s\033[0m" "$1"; }
red(){ printf "\033[31m%s\033[0m" "$1"; }
ok(){ PASS=$((PASS+1)); printf "  %s %s\n" "$(green '✅')" "$1"; }
bad(){ FAIL=$((FAIL+1)); printf "  %s %s\n" "$(red '❌')" "$1"; }
info(){ printf "• %s\n" "$1"; }

body_of(){ echo "$1" | tr -d '\r' | awk 'BEGIN{p=0} /^$/{p=1; next} p{print}'; }

get(){ [ -n "$2" ] && curl -sS -i -b "$2" "$1" || curl -sS -i "$1"; }

post_json(){
  local url="$1" json="$2" send="$3" save="$4"
  if [ -n "$send" ] && [ -n "$save" ]; then
    curl -sS -i -H "Content-Type: application/json" -d "$json" -b "$send" -c "$save" "$url"
  elif [ -n "$save" ]; then
    curl -sS -i -H "Content-Type: application/json" -d "$json" -c "$save" "$url"
  elif [ -n "$send" ]; then
    curl -sS -i -H "Content-Type: application/json" -d "$json" -b "$send" "$url"
  else
    curl -sS -i -H "Content-Type: application/json" -d "$json" "$url"
  fi
}

expect_status(){ echo "$2" | head -n1 | grep -q " $1 " && ok "$3" || bad "$3 (expected $1)"; }
expect_status_any(){
  local labels="$1" hdr="$2" msg="$3"
  for s in $labels; do echo "$hdr" | head -n1 | grep -q " $s " && { ok "$msg"; return; }; done
  bad "$msg (expected one of: $labels)"
}

first_json_int_field(){ sed -nE "s/.*\"$1\"[[:space:]]*:[[:space:]]*([0-9]+).*/\1/p" | head -n1; }
first_json_str_field(){ sed -nE "s/.*\"$1\"[[:space:]]*:[[:space:]]*\"([^\"]+)\".*/\1/p" | head -n1; }

info "BASE_URL = $BASE_URL"

# 0) Public routes
R="$(get "$BASE_URL/" "")";                     expect_status "200" "$R" "GET /"
R="$(get "$BASE_URL/api/categories" "")";       expect_status "200" "$R" "GET /api/categories"
CATS_BODY="$(body_of "$R")"
CAT_ID="$(echo "$CATS_BODY" | first_json_int_field id)"; [ -z "$CAT_ID" ] && CAT_ID=1
R="$(get "$BASE_URL/api/trending?perList=3&categoriesToShow=1" "")"
expect_status "200" "$R" "GET /api/trending (public)"
TREND_BODY="$(body_of "$R")"
EVENT_ID="$(echo "$TREND_BODY" | first_json_int_field id)"; [ -z "$EVENT_ID" ] && EVENT_ID=1

# 1) Unauthed guards (allow 401 or Identity 302)
R="$(post_json "$BASE_URL/api/events/$EVENT_ID/status" '{"status":"Interested"}' "" "")"
expect_status_any "401 302" "$R" "POST /events/{id}/status unauth"
R="$(post_json "$BASE_URL/api/events/$EVENT_ID/rating" '{"rating":5}' "" "")"
expect_status_any "401 302" "$R" "POST /events/{id}/rating unauth"

# 2) Search (public)
R="$(get "$BASE_URL/api/search?q=jazz" "")";    expect_status "200" "$R" "GET /api/search?q=jazz"

# 3) Register 2 users
R="$(post_json "$BASE_URL/api/auth/register" "{\"email\":\"$U1_EMAIL\",\"password\":\"$PASSWORD\",\"displayName\":\"Smoke One\"}" "" "$COOK1")"
expect_status "200" "$R" "POST /auth/register (u1)"
R="$(get "$BASE_URL/api/auth/me" "$COOK1")";     expect_status "200" "$R" "GET /auth/me (u1)"
U1_ID="$(body_of "$R" | first_json_str_field id)"

R="$(post_json "$BASE_URL/api/auth/register" "{\"email\":\"$U2_EMAIL\",\"password\":\"$PASSWORD\",\"displayName\":\"Smoke Two\"}" "" "$COOK2")"
expect_status "200" "$R" "POST /auth/register (u2)"
R="$(get "$BASE_URL/api/auth/me" "$COOK2")";     expect_status "200" "$R" "GET /auth/me (u2)"
U2_ID="$(body_of "$R" | first_json_str_field id)"

# 4) Event fetch + telemetry (public)
R="$(get "$BASE_URL/api/events/$EVENT_ID" "")";  expect_status "200" "$R" "GET /api/events/{id}"
R="$(post_json "$BASE_URL/api/telemetry/clicks" "{\"eventId\":$EVENT_ID}" "" "")"
expect_status_any "200 204" "$R" "POST /telemetry/clicks"
R="$(post_json "$BASE_URL/api/telemetry/dwell" "{\"eventId\":$EVENT_ID,\"dwellMs\":2500}" "" "")"
expect_status_any "200 204" "$R" "POST /telemetry/dwell"

# 5) Status + rating (authed u1)
R="$(post_json "$BASE_URL/api/events/$EVENT_ID/status" '{"status":"Interested"}' "$COOK1" "")"
expect_status "200" "$R" "u1 Interested"
R="$(post_json "$BASE_URL/api/events/$EVENT_ID/status" '{"status":"Going"}' "$COOK1" "")"
expect_status "200" "$R" "u1 Going"
R="$(post_json "$BASE_URL/api/events/$EVENT_ID/rating" '{"rating":5}' "$COOK1" "")"
expect_status "200" "$R" "u1 Rate 5"
R="$(get "$BASE_URL/api/events/$EVENT_ID/me" "$COOK1")"; expect_status "200" "$R" "u1 me on event"

# 6) Social (u1 follows u2; u2 going)
R="$(post_json "$BASE_URL/api/social/follow" "{\"followeeId\":\"$U2_ID\"}" "$COOK1" "")"
expect_status "200" "$R" "u1 follow u2"
R="$(get "$BASE_URL/api/social/following" "$COOK1")"; expect_status "200" "$R" "u1 following"
R="$(post_json "$BASE_URL/api/events/$EVENT_ID/status" '{"status":"Going"}' "$COOK2" "")"
expect_status "200" "$R" "u2 Going"
R="$(get "$BASE_URL/api/social/friends-going?eventId=$EVENT_ID" "$COOK1")"
expect_status "200" "$R" "friends-going for u1"

# 7) Saved lists (u1)
R="$(get "$BASE_URL/api/events/mine?status=Going" "$COOK1")";     expect_status "200" "$R" "u1 saved Going"
R="$(get "$BASE_URL/api/events/mine?status=Interested" "$COOK1")";expect_status "200" "$R" "u1 saved Interested"

# 8) Trending (authed) + by-category
R="$(get "$BASE_URL/api/trending?perList=6&categoriesToShow=2" "$COOK1")"
expect_status "200" "$R" "GET /api/trending (auth)"
R="$(get "$BASE_URL/api/trending/by-category?categoryId=$CAT_ID&topN=6" "$COOK1")"
expect_status "200" "$R" "GET /trending/by-category"

# 9) Recs (authed)
R="$(get "$BASE_URL/api/recs?topN=6" "$COOK1")"; expect_status "200" "$R" "GET /api/recs (u1)"

# 10) Users search (authed)
R="$(get "$BASE_URL/api/users/search?q=smoke2&limit=10" "$COOK1")"
expect_status "200" "$R" "GET /api/users/search"

# 11) Admin seeding/training (accept 302 redirect from Razor page)
R="$(get "$BASE_URL/Admin/Seed" "$COOK1")";      expect_status_any "302 200" "$R" "GET /Admin/Seed"
R="$(get "$BASE_URL/Admin/Train" "$COOK1")";     expect_status_any "302 200" "$R" "GET /Admin/Train"

# 12) Auth logout -> protected calls should now deny
R="$(post_json "$BASE_URL/api/auth/logout" "{}" "$COOK1" "$COOK1")"
expect_status "200" "$R" "POST /auth/logout (u1)"
R="$(get "$BASE_URL/api/recs?topN=3" "$COOK1")"; expect_status_any "401 302" "$R" "GET /api/recs after logout"

echo
echo "=================="
printf "Summary → Passed: %s   Failed: %s\n" "$(green $PASS)" "$(red $FAIL)"
echo "=================="
[ "$FAIL" -eq 0 ]
