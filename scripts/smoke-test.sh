#!/usr/bin/env bash
# Exercises the acceptance checks from the brief against a running API.
# Usage: scripts/smoke-test.sh [base-url]   (default http://localhost:5153)
# Needs curl. Uses the demo users from appsettings.Development.json.
set -u
BASE="${1:-http://localhost:5153}"
JAR="$(mktemp)"
PASS=0; FAIL=0

check() { # name expected actual
  if [ "$2" = "$3" ]; then PASS=$((PASS+1)); echo "PASS  $1"; else FAIL=$((FAIL+1)); echo "FAIL  $1 (expected $2, got $3)"; fi
}
status() { curl -s -o /dev/null -w '%{http_code}' "$@"; }
login() {
  rm -f "$JAR"
  status -c "$JAR" -H 'Content-Type: application/json' -d "{\"email\":\"$1\",\"password\":\"$2\"}" "$BASE/api/auth/login"
}

check "unauthenticated history -> 401" 401 "$(status "$BASE/api/runs")"
check "unauthenticated detail -> 401" 401 "$(status "$BASE/api/runs/R-102")"

check "viewer sign-in" 200 "$(login viewer@demo.local 'Viewer#Demo2026')"
check "viewer history -> 403" 403 "$(status -b "$JAR" "$BASE/api/runs")"
check "viewer detail -> 403" 403 "$(status -b "$JAR" "$BASE/api/runs/R-102")"

check "wrong password -> 401" 401 "$(login admin@demo.local 'wrong-password')"
check "admin sign-in" 200 "$(login admin@demo.local 'Admin#Demo2026')"

HISTORY="$(curl -s -b "$JAR" "$BASE/api/runs?page=1&pageSize=20")"
echo "      history: $HISTORY"
case "$HISTORY" in *'"runId":"R-102"'*'"runId":"R-101"'*) check "R-102 listed before R-101" ok ok;; *) check "R-102 listed before R-101" ok no;; esac

DETAIL="$(curl -s -b "$JAR" "$BASE/api/runs/R-102")"
echo "      R-102: $DETAIL"
case "$DETAIL" in *'"totalAllocatedUnits":5'*'"gapCount":3'*) check "R-102 has 5 units and 3 gaps" ok ok;; *) check "R-102 has 5 units and 3 gaps" ok no;; esac

EXC="$(curl -s -b "$JAR" "$BASE/api/runs/R-102?filter=exceptions")"
case "$EXC" in *'"gapId":"G-01"'*'"gapId":"G-03"'*) G2=$(echo "$EXC" | grep -c '"gapId":"G-02"'); check "exceptions filter = G-01 and G-03 only" 0 "$G2";; *) check "exceptions filter = G-01 and G-03 only" ok no;; esac

check "unknown run -> 404" 404 "$(status -b "$JAR" "$BASE/api/runs/R-999")"
check "page=0 -> 400" 400 "$(status -b "$JAR" "$BASE/api/runs?page=0")"
check "pageSize=101 -> 400" 400 "$(status -b "$JAR" "$BASE/api/runs?pageSize=101")"
check "pageSize=abc -> 400" 400 "$(status -b "$JAR" "$BASE/api/runs?pageSize=abc")"
check "filter=bogus -> 400" 400 "$(status -b "$JAR" "$BASE/api/runs/R-102?filter=bogus")"

CID="$(curl -s -D - -o /dev/null -b "$JAR" -H 'X-Correlation-ID: smoke-test-123' "$BASE/api/runs" | tr -d '\r' | awk -F': ' 'tolower($1)=="x-correlation-id"{print $2}')"
check "correlation ID echoed" smoke-test-123 "$CID"

rm -f "$JAR"
echo "----"; echo "$PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
