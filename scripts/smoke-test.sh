#!/usr/bin/env bash
# ============================================================
# AykutOnPC — Production smoke test
# Usage:   bash scripts/smoke-test.sh https://aykutonpc.com
# Exits non-zero on the first failed check.
# ============================================================
set -euo pipefail

BASE_URL="${1:-${BASE_URL:-http://localhost:8080}}"
BASE_URL="${BASE_URL%/}"          # strip trailing slash

PASS=0
FAIL=0
TOTAL=0

# ── helpers ──────────────────────────────────────────────────
green() { printf '\033[32m%s\033[0m\n' "$*"; }
red()   { printf '\033[31m%s\033[0m\n' "$*"; }
gray()  { printf '\033[90m%s\033[0m\n' "$*"; }

check() {
    local name="$1" cmd="$2"
    TOTAL=$((TOTAL + 1))
    printf '  [%d] %-45s ' "$TOTAL" "$name"
    if eval "$cmd" >/dev/null 2>&1; then
        green "PASS"
        PASS=$((PASS + 1))
    else
        red "FAIL"
        gray "      → $cmd"
        FAIL=$((FAIL + 1))
    fi
}

echo
echo "============================================================"
echo " AykutOnPC smoke test"
echo " Target: $BASE_URL"
echo "============================================================"

# ── 1. Reachability ─────────────────────────────────────────
echo
echo "▸ Connectivity"
check "Homepage returns 200" \
    "curl -fsS --max-time 10 '$BASE_URL/' -o /dev/null"

check "Homepage HTML contains site marker" \
    "curl -fsS --max-time 10 '$BASE_URL/' | grep -qi 'AykutOnPC'"

# ── 2. Health endpoint ──────────────────────────────────────
echo
echo "▸ Health"
HEALTH_BODY=$(curl -fsS --max-time 10 "$BASE_URL/health" || echo "")
check "/health returns 200" \
    "[ -n \"\$(curl -fsS --max-time 10 '$BASE_URL/health' || true)\" ]"

check "/health reports overall status Healthy" \
    "echo '$HEALTH_BODY' | grep -q '\"status\":\"Healthy\"'"

check "/health includes database check" \
    "echo '$HEALTH_BODY' | grep -q '\"name\":\"database\"'"

check "/health includes redis check" \
    "echo '$HEALTH_BODY' | grep -q '\"name\":\"redis\"'"

# ── 3. SEO surface ──────────────────────────────────────────
echo
echo "▸ SEO"
HOME_HTML=$(curl -fsS --max-time 10 "$BASE_URL/" || echo "")

check "robots.txt is served" \
    "curl -fsS --max-time 10 '$BASE_URL/robots.txt' | grep -q 'User-agent'"

check "robots.txt disallows /admin" \
    "curl -fsS --max-time 10 '$BASE_URL/robots.txt' | grep -qi 'Disallow:.*[Aa]dmin'"

check "Open Graph title meta present" \
    "echo '$HOME_HTML' | grep -q 'property=\"og:title\"'"

check "Twitter card meta present" \
    "echo '$HOME_HTML' | grep -q 'name=\"twitter:card\"'"

check "Canonical URL present" \
    "echo '$HOME_HTML' | grep -q 'rel=\"canonical\"'"

# ── 4. AI chat endpoint ─────────────────────────────────────
echo
echo "▸ AI"
CHAT_REPLY=$(curl -fsS --max-time 30 -X POST "$BASE_URL/api/chat/ask" \
    -H 'Content-Type: application/json' \
    -d '{"message":"Hello"}' || echo "")

check "/api/chat/ask returns JSON with response field" \
    "echo '$CHAT_REPLY' | grep -q '\"response\"'"

# ── 5. Rate limiting ────────────────────────────────────────
echo
echo "▸ Rate limiting (chat: 10/min)"
RATE_LIMITED=0
for i in $(seq 1 15); do
    code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 -X POST "$BASE_URL/api/chat/ask" \
        -H 'Content-Type: application/json' \
        -d '{"message":"rate test"}' || echo "000")
    if [ "$code" = "429" ]; then
        RATE_LIMITED=1
        break
    fi
done
check "Burst of 15 chat requests triggers 429" \
    "[ $RATE_LIMITED -eq 1 ]"

# ── 6. Security headers (TLS only) ──────────────────────────
if [[ "$BASE_URL" == https://* ]]; then
    echo
    echo "▸ Security headers (HTTPS only)"
    HEADERS=$(curl -fsSI --max-time 10 "$BASE_URL/" || echo "")

    check "Strict-Transport-Security present" \
        "echo '$HEADERS' | grep -qi 'strict-transport-security'"

    check "X-Frame-Options or CSP frame-ancestors present" \
        "echo '$HEADERS' | grep -Eqi 'x-frame-options|frame-ancestors'"

    check "X-Content-Type-Options: nosniff" \
        "echo '$HEADERS' | grep -qi 'x-content-type-options:.*nosniff'"
fi

# ── 7. Admin surface (must be reachable but not return 200 when anonymous) ──
echo
echo "▸ Admin surface"
ADMIN_CODE=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 "$BASE_URL/Admin" || echo "000")
check "/Admin redirects or 401s (no anon access)" \
    "[ '$ADMIN_CODE' = '302' ] || [ '$ADMIN_CODE' = '401' ] || [ '$ADMIN_CODE' = '403' ]"

# ── Summary ─────────────────────────────────────────────────
echo
echo "============================================================"
if [ "$FAIL" -eq 0 ]; then
    green " ✓ $PASS / $TOTAL checks passed"
    echo "============================================================"
    exit 0
else
    red   " ✗ $FAIL / $TOTAL checks FAILED ($PASS passed)"
    echo "============================================================"
    exit 1
fi
