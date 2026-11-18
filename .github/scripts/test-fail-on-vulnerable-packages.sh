#!/usr/bin/env bash
# Fixture and smoke tests for fail-on-vulnerable-packages.sh.
# Exit 0 only when every assertion passes.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCANNER="${SCRIPT_DIR}/fail-on-vulnerable-packages.sh"
FIXTURES="${SCRIPT_DIR}/fixtures"
passed=0
failed=0

assert_exit() {
  local name="$1"
  local expected="$2"
  shift 2
  set +e
  local output
  output="$("$@" 2>&1)"
  local actual=$?
  set -e
  if [[ "$actual" -eq "$expected" ]]; then
    echo "PASS: $name (exit $actual)"
    passed=$((passed + 1))
  else
    echo "FAIL: $name (expected exit $expected, got $actual)" >&2
    echo "$output" >&2
    failed=$((failed + 1))
  fi
  # Stash last output for content checks when needed
  LAST_OUTPUT="$output"
}

assert_contains() {
  local name="$1"
  local needle="$2"
  if [[ "$LAST_OUTPUT" == *"$needle"* ]]; then
    echo "PASS: $name (output contains '$needle')"
    passed=$((passed + 1))
  else
    echo "FAIL: $name (output missing '$needle')" >&2
    echo "$LAST_OUTPUT" >&2
    failed=$((failed + 1))
  fi
}

chmod +x "$SCANNER"

echo "=== Fixture: vulnerable JSON must fail and name the package ==="
assert_exit "vulnerable JSON exits non-zero" 1 \
  "$SCANNER" --parse-json "$FIXTURES/vulnerable-packages.json"
assert_contains "vulnerable JSON names System.Text.RegularExpressions" "System.Text.RegularExpressions"
assert_contains "vulnerable JSON names Newtonsoft.Json" "Newtonsoft.Json"

echo "=== Fixture: clean JSON must pass ==="
assert_exit "clean JSON exits 0" 0 \
  "$SCANNER" --parse-json "$FIXTURES/clean-packages.json"

echo "=== Fixture: vulnerable text (grep fallback) must fail and name the package ==="
assert_exit "vulnerable text exits non-zero" 1 \
  "$SCANNER" --parse-text "$FIXTURES/vulnerable-packages.txt"
assert_contains "vulnerable text names System.Text.RegularExpressions" "System.Text.RegularExpressions"

echo "=== Fixture: clean text must pass ==="
assert_exit "clean text exits 0" 0 \
  "$SCANNER" --parse-text "$FIXTURES/clean-packages.txt"

echo "=== Fixture: malformed JSON must fail ==="
assert_exit "malformed JSON exits non-zero" 1 \
  "$SCANNER" --parse-json "$FIXTURES/malformed-packages.json"

echo "=== Missing JSON parser tools must fail ==="
assert_exit "missing jq/python exits non-zero" 1 \
  env JQ_BIN=/nonexistent/jq PYTHON3_BIN=/nonexistent/python3 \
  "$SCANNER" --parse-json "$FIXTURES/clean-packages.json"

echo "=== Missing report file must fail ==="
assert_exit "missing file exits non-zero" 1 \
  "$SCANNER" --parse-json "$FIXTURES/does-not-exist.json"

# Optional end-to-end: scratch project with a known-vulnerable package version.
# Never commits anything; only runs when dotnet is available.
if command -v dotnet >/dev/null 2>&1; then
  echo "=== End-to-end: scratch project with known-vulnerable package ==="
  scratch="$(mktemp -d)"
  cleanup_scratch() { rm -rf "$scratch"; }
  trap cleanup_scratch EXIT

  dotnet new classlib -n VulnE2E -f net9.0 -o "$scratch" --force >/dev/null
  # NU1903 is expected for this intentionally vulnerable pin.
  dotnet add "$scratch/VulnE2E.csproj" package System.Text.RegularExpressions --version 4.3.0 >/dev/null
  dotnet restore "$scratch/VulnE2E.csproj" -v q >/dev/null

  assert_exit "e2e vulnerable project exits non-zero" 1 \
    "$SCANNER" --project "$scratch/VulnE2E.csproj"
  assert_contains "e2e names System.Text.RegularExpressions" "System.Text.RegularExpressions"

  trap - EXIT
  cleanup_scratch
else
  echo "SKIP: end-to-end scratch project (dotnet not on PATH)"
fi

echo
echo "Results: $passed passed, $failed failed"
if [[ "$failed" -ne 0 ]]; then
  exit 1
fi
echo "All vulnerable-package detector tests passed."
