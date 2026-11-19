#!/usr/bin/env bash
# Fixture and smoke tests for fail-on-vulnerable-packages.sh.
# Exit 0 only when every assertion passes.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
SCANNER="${SCRIPT_DIR}/fail-on-vulnerable-packages.sh"
FIXTURES="${SCRIPT_DIR}/fixtures"
passed=0
failed=0
skipped=0
declare -a CASE_LOG=()

record_pass() {
  local name="$1"
  echo "PASS: $name"
  passed=$((passed + 1))
  CASE_LOG+=("PASS: $name")
}

record_fail() {
  local name="$1"
  shift
  echo "FAIL: $name" >&2
  if [[ $# -gt 0 ]]; then
    printf '%s\n' "$@" >&2
  fi
  failed=$((failed + 1))
  CASE_LOG+=("FAIL: $name")
}

record_skip() {
  local name="$1"
  local reason="$2"
  echo "SKIP: $name ($reason)"
  skipped=$((skipped + 1))
  CASE_LOG+=("SKIP: $name ($reason)")
}

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
    CASE_LOG+=("PASS: $name (exit $actual)")
  else
    echo "FAIL: $name (expected exit $expected, got $actual)" >&2
    echo "$output" >&2
    failed=$((failed + 1))
    CASE_LOG+=("FAIL: $name (expected exit $expected, got $actual)")
  fi
  LAST_OUTPUT="$output"
}

assert_contains() {
  local name="$1"
  local needle="$2"
  if [[ "$LAST_OUTPUT" == *"$needle"* ]]; then
    echo "PASS: $name (output contains '$needle')"
    passed=$((passed + 1))
    CASE_LOG+=("PASS: $name (output contains '$needle')")
  else
    echo "FAIL: $name (output missing '$needle')" >&2
    echo "$LAST_OUTPUT" >&2
    failed=$((failed + 1))
    CASE_LOG+=("FAIL: $name (output missing '$needle')")
  fi
}

assert_not_contains() {
  local name="$1"
  local needle="$2"
  if [[ "$LAST_OUTPUT" != *"$needle"* ]]; then
    echo "PASS: $name (output omits '$needle')"
    passed=$((passed + 1))
    CASE_LOG+=("PASS: $name (output omits '$needle')")
  else
    echo "FAIL: $name (output unexpectedly contains '$needle')" >&2
    echo "$LAST_OUTPUT" >&2
    failed=$((failed + 1))
    CASE_LOG+=("FAIL: $name (output unexpectedly contains '$needle')")
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

echo "=== Fixture: empty / whitespace / unrecognized text must fail closed ==="
assert_exit "empty text exits non-zero" 1 \
  "$SCANNER" --parse-text "$FIXTURES/empty-packages.txt"
assert_exit "whitespace-only text exits non-zero" 1 \
  "$SCANNER" --parse-text "$FIXTURES/whitespace-packages.txt"
assert_exit "garbage text exits non-zero" 1 \
  "$SCANNER" --parse-text "$FIXTURES/garbage-packages.txt"

echo "=== Fixture: JSON missing required report structure must fail closed ==="
assert_exit "empty JSON object {} exits non-zero" 1 \
  "$SCANNER" --parse-json "$FIXTURES/empty-object.json"
assert_exit "JSON {\"version\":1} exits non-zero" 1 \
  "$SCANNER" --parse-json "$FIXTURES/missing-projects.json"
assert_exit "JSON projects not array exits non-zero" 1 \
  "$SCANNER" --parse-json "$FIXTURES/projects-not-array.json"
assert_exit "JSON {\"projects\":[]} exits non-zero" 1 \
  "$SCANNER" --parse-json "$FIXTURES/empty-projects.json"
assert_exit "JSON {\"projects\":[{}]} exits non-zero" 1 \
  "$SCANNER" --parse-json "$FIXTURES/project-missing-path.json"
assert_exit "JSON array root [] exits non-zero" 1 \
  "$SCANNER" --parse-json "$FIXTURES/json-array-root.json"
assert_exit "JSON null exits non-zero" 1 \
  "$SCANNER" --parse-json "$FIXTURES/json-null.json"

echo "=== Fixture: malformed JSON must fail ==="
assert_exit "malformed JSON exits non-zero" 1 \
  "$SCANNER" --parse-json "$FIXTURES/malformed-packages.json"

echo "=== Unreadable report: missing file and no-read-permission ==="
assert_exit "missing file exits non-zero" 1 \
  "$SCANNER" --parse-json "$FIXTURES/does-not-exist.json"

unreadable="$(mktemp)"
cleanup_unreadable() { rm -f "$unreadable"; }
trap cleanup_unreadable EXIT
cp "$FIXTURES/clean-packages.json" "$unreadable"
chmod a-r "$unreadable"
if [[ "$(id -u)" -eq 0 ]]; then
  record_skip "unreadable report (no read permission)" "running as root; permission deny is not enforceable"
else
  assert_exit "unreadable report (no read permission) exits non-zero" 1 \
    "$SCANNER" --parse-json "$unreadable"
fi
trap - EXIT
cleanup_unreadable

echo "=== Missing JSON parser tools must fail ==="
assert_exit "missing jq/python exits non-zero" 1 \
  env JQ_BIN=/nonexistent/jq PYTHON3_BIN=/nonexistent/python3 \
  "$SCANNER" --parse-json "$FIXTURES/clean-packages.json"

# Full-scan path negatives (DOTNET_BIN stubs). Uses a dummy project path; stubs
# never touch the real solution. Stub stderr must not look like "--format json
# unsupported" or the scanner would take the console fallback path.
stubs_dir="$(mktemp -d)"
cleanup_stubs() { rm -rf "$stubs_dir"; }
trap cleanup_stubs EXIT
dummy_project="${stubs_dir}/Dummy.csproj"
: >"$dummy_project"

failing_dotnet="${stubs_dir}/dotnet-fail"
cat >"$failing_dotnet" <<'EOF'
#!/usr/bin/env bash
# Simulate a restore/auth failure on vulnerable package listing.
echo "error NU1301: Unable to load the service index for source https://api.nuget.org/v3/index.json (401 Unauthorized)." >&2
exit 1
EOF
chmod +x "$failing_dotnet"

vulnerable_dotnet="${stubs_dir}/dotnet-vulnerable"
cat >"$vulnerable_dotnet" <<EOF
#!/usr/bin/env bash
# Emit vulnerable JSON fixture and succeed (exit 0), like a real list --format json.
cat "$FIXTURES/vulnerable-packages.json"
exit 0
EOF
chmod +x "$vulnerable_dotnet"

clean_dotnet="${stubs_dir}/dotnet-clean"
cat >"$clean_dotnet" <<EOF
#!/usr/bin/env bash
# Emit clean JSON fixture and succeed (exit 0).
cat "$FIXTURES/clean-packages.json"
exit 0
EOF
chmod +x "$clean_dotnet"

echo "=== Full scan: missing DOTNET_BIN (dotnet not found) must fail ==="
assert_exit "missing DOTNET_BIN exits non-zero" 1 \
  env DOTNET_BIN=/nonexistent/dotnet \
  "$SCANNER" --project "$dummy_project"
assert_contains "missing DOTNET_BIN names install error" "dotnet is not installed"

echo "=== Full scan: failing DOTNET_BIN (scan command non-zero) must fail closed ==="
assert_exit "failing DOTNET_BIN exits non-zero" 1 \
  env DOTNET_BIN="$failing_dotnet" \
  "$SCANNER" --project "$dummy_project"
assert_contains "failing DOTNET_BIN reports list failure" "failed to list packages"
assert_not_contains "failing DOTNET_BIN omits clean success" "No vulnerable NuGet packages reported"

echo "=== Full scan: stub vulnerable JSON must fail and name the package ==="
assert_exit "stub vulnerable JSON exits non-zero" 1 \
  env DOTNET_BIN="$vulnerable_dotnet" \
  "$SCANNER" --project "$dummy_project"
assert_contains "stub vulnerable JSON names System.Text.RegularExpressions" "System.Text.RegularExpressions"

echo "=== Full scan: stub clean JSON must pass ==="
assert_exit "stub clean JSON exits 0" 0 \
  env DOTNET_BIN="$clean_dotnet" \
  "$SCANNER" --project "$dummy_project"
assert_contains "stub clean JSON reports clean" "No vulnerable NuGet packages reported"

trap - EXIT
cleanup_stubs

# Optional end-to-end checks when dotnet is available.
if command -v dotnet >/dev/null 2>&1; then
  echo "=== End-to-end: real clean scan of this repo must pass ==="
  pushd "$REPO_ROOT" >/dev/null
  assert_exit "e2e real repo scan exits 0" 0 \
    "$SCANNER" --project HelloblueGK.csproj
  assert_contains "e2e real repo scan reports clean" "No vulnerable NuGet packages reported"
  popd >/dev/null

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
  record_skip "e2e real repo scan" "dotnet not on PATH"
  record_skip "e2e vulnerable scratch project" "dotnet not on PATH"
fi

echo
echo "Results: $passed passed, $failed failed, $skipped skipped"
echo "Cases:"
for case_line in "${CASE_LOG[@]}"; do
  echo "  $case_line"
done
if [[ "$failed" -ne 0 ]]; then
  exit 1
fi
echo "All vulnerable-package detector tests passed."
