#!/usr/bin/env bash
# Fail CI when NuGet reports vulnerable packages (direct or transitive).
#
# Severity policy: fail on ANY reported vulnerability (Low/Moderate/High/Critical).
# This matches the previous CVE/GHSA presence gate and must not be loosened.
#
# Usage:
#   fail-on-vulnerable-packages.sh
#   fail-on-vulnerable-packages.sh --project path/to.csproj [--project other.csproj ...]
#   fail-on-vulnerable-packages.sh --parse-json report.json
#   fail-on-vulnerable-packages.sh --parse-text report.txt
#
# JSON parsing prefers jq (preinstalled on ubuntu-latest), then python3.
# If `dotnet list ... --format json` is unavailable, falls back to console text + grep -E.
# Never depends on ripgrep (`rg`).
#
# Parsers pass only on recognizably valid reports:
#   JSON — top-level object with a projects array; each project is an object that either
#          omits frameworks (NuGet clean shape) or has frameworks as an array.
#   Text — recognizable `dotnet list package --vulnerable` clean or vulnerable output.
# Empty, garbage, or unexpected schema exits 1 (fail closed).
set -euo pipefail

JQ_BIN="${JQ_BIN:-jq}"
PYTHON3_BIN="${PYTHON3_BIN:-python3}"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"

# ---------------------------------------------------------------------------
# Parsers (fixture-testable)
# Exit 0 = clean, 2 = vulnerabilities found, 1 = tool/parse/schema error.
# ---------------------------------------------------------------------------

parse_json_report() {
  local file="$1"
  local ids=""

  if [[ ! -f "$file" ]]; then
    echo "ERROR: JSON report not found: $file" >&2
    return 1
  fi

  if [[ ! -s "$file" ]]; then
    echo "ERROR: JSON report is empty" >&2
    return 1
  fi

  if command -v "$JQ_BIN" >/dev/null 2>&1; then
    # Validate schema first (fail closed), then extract vulnerable package ids.
    if ! "$JQ_BIN" -e '
      if type != "object" then
        error("top-level JSON must be an object")
      elif (has("projects") | not) then
        error("JSON report missing projects array")
      elif (.projects | type) != "array" then
        error("JSON report projects must be an array")
      elif ([.projects[] | select(
          (type != "object")
          or ((has("frameworks")) and ((.frameworks | type) != "array"))
        )] | length) > 0 then
        error("JSON report project entries must be objects with frameworks omitted (clean) or an array")
      else
        true
      end
    ' "$file" >/dev/null; then
      echo "ERROR: JSON report has unexpected or invalid vulnerable-package schema" >&2
      return 1
    fi

    if ! ids="$("$JQ_BIN" -r '
      [.projects[]
        | (.frameworks // [])[]
        | ((.topLevelPackages // []) + (.transitivePackages // []))[]
        | select((.vulnerabilities // []) | length > 0)
        | .id
      ] | unique | .[]
    ' "$file")"; then
      echo "ERROR: failed to parse vulnerable-package JSON report with jq" >&2
      return 1
    fi
  elif command -v "$PYTHON3_BIN" >/dev/null 2>&1; then
    if ! ids="$("$PYTHON3_BIN" - "$file" <<'PY'
import json, sys

path = sys.argv[1]
try:
    data = json.load(open(path, encoding="utf-8"))
except Exception as exc:
    print(f"ERROR: failed to parse JSON: {exc}", file=sys.stderr)
    sys.exit(1)

if not isinstance(data, dict):
    print("ERROR: top-level JSON must be an object", file=sys.stderr)
    sys.exit(1)
if "projects" not in data:
    print("ERROR: JSON report missing projects array", file=sys.stderr)
    sys.exit(1)
if not isinstance(data["projects"], list):
    print("ERROR: JSON report projects must be an array", file=sys.stderr)
    sys.exit(1)

ids = []
for project in data["projects"]:
    if not isinstance(project, dict):
        print("ERROR: JSON report project entries must be objects", file=sys.stderr)
        sys.exit(1)
    if "frameworks" in project and not isinstance(project["frameworks"], list):
        print(
            "ERROR: JSON report project frameworks must be an array when present",
            file=sys.stderr,
        )
        sys.exit(1)
    for fw in project.get("frameworks") or []:
        if not isinstance(fw, dict):
            print("ERROR: JSON report framework entries must be objects", file=sys.stderr)
            sys.exit(1)
        for key in ("topLevelPackages", "transitivePackages"):
            packages = fw.get(key) or []
            if not isinstance(packages, list):
                print(f"ERROR: JSON report {key} must be an array", file=sys.stderr)
                sys.exit(1)
            for pkg in packages:
                if not isinstance(pkg, dict):
                    continue
                vulns = pkg.get("vulnerabilities") or []
                if vulns:
                    pkg_id = pkg.get("id")
                    if pkg_id:
                        ids.append(pkg_id)

for pkg_id in sorted(set(ids)):
    print(pkg_id)
PY
    )"; then
      echo "ERROR: failed to parse vulnerable-package JSON report with python3" >&2
      return 1
    fi
  else
    echo "ERROR: neither jq nor python3 is available to parse JSON reports" >&2
    return 1
  fi

  if [[ -n "${ids}" ]]; then
    echo "ERROR: vulnerable packages found:" >&2
    # shellcheck disable=SC2001
    echo "$ids" | sed 's/^/  - /' >&2
    return 2
  fi
  return 0
}

parse_text_report() {
  local file="$1"

  if [[ ! -f "$file" ]]; then
    echo "ERROR: text report not found: $file" >&2
    return 1
  fi

  if [[ ! -s "$file" ]]; then
    echo "ERROR: text report is empty" >&2
    return 1
  fi

  # Recognizable `dotnet list package --vulnerable` console shapes:
  #   clean:      "... has no vulnerable packages ..."
  #   vulnerable: "... has the following vulnerable packages" plus advisory/table cues
  local has_clean=0 has_vulnerable=0
  if grep -qE 'has no vulnerable packages' "$file"; then
    has_clean=1
  fi
  if grep -qE 'has the following vulnerable packages' "$file" \
    || grep -qE 'Top-level Package[[:space:]]+Requested[[:space:]]+Resolved[[:space:]]+Severity[[:space:]]+Advisory' "$file"; then
    has_vulnerable=1
  fi

  if [[ "$has_clean" -eq 0 && "$has_vulnerable" -eq 0 ]]; then
    echo "ERROR: unrecognized or invalid vulnerable-package text report" >&2
    return 1
  fi

  # Match NuGet's vulnerable package table / advisory URLs (CVE or GHSA).
  # Use grep -E only — never ripgrep — so GitHub-hosted runners work.
  if ! grep -qE 'CVE-[0-9]{4}-[0-9]+|GHSA-[A-Za-z0-9-]+' "$file"; then
    if [[ "$has_vulnerable" -ne 0 ]]; then
      echo "ERROR: vulnerable-package text report claims vulnerabilities but names none" >&2
      return 1
    fi
    return 0
  fi

  echo "ERROR: vulnerable packages found:" >&2
  # Console rows look like: "   > Package.Id      1.0.0       1.0.0      High       https://..."
  local named=0 pkg
  while IFS= read -r line; do
    if [[ "$line" =~ CVE-[0-9]{4}-[0-9]+|GHSA-[A-Za-z0-9-]+ ]] && \
       [[ "$line" =~ ^[[:space:]]*\>[[:space:]]+([^[:space:]]+) ]]; then
      pkg="${BASH_REMATCH[1]}"
      echo "  - ${pkg}" >&2
      named=1
    fi
  done < <(grep -E '^\s*>\s+\S+' "$file" || true)

  if [[ "$named" -eq 0 ]]; then
    grep -E 'CVE-[0-9]{4}-[0-9]+|GHSA-[A-Za-z0-9-]+' "$file" | sed 's/^/  /' >&2 || true
  fi
  return 2
}

# ---------------------------------------------------------------------------
# Live scan
# ---------------------------------------------------------------------------

# Run a parser without tripping set -e on expected status 2 (vulns found).
# Sets global _parse_rc.
_run_parser() {
  local parser="$1"
  local file="$2"
  _parse_rc=0
  "$parser" "$file" && _parse_rc=0 || _parse_rc=$?
  return 0
}

scan_project() {
  local project="$1"
  local tmpdir="$2"
  local base out_json out_text err_file json_rc text_rc

  base="$(basename "$project")"
  out_json="$tmpdir/${base}.json"
  out_text="$tmpdir/${base}.txt"
  err_file="$tmpdir/${base}.err"

  echo "== Vulnerable packages: $project =="

  if ! command -v "$DOTNET_BIN" >/dev/null 2>&1; then
    echo "ERROR: dotnet is not installed or not on PATH" >&2
    return 1
  fi

  # Prefer machine-readable JSON. Capture stderr separately so a format-unsupported
  # SDK can fall back to console text without losing real failures.
  json_rc=0
  "$DOTNET_BIN" list "$project" package --vulnerable --include-transitive --format json \
    >"$out_json" 2>"$err_file" && json_rc=0 || json_rc=$?

  if [[ "$json_rc" -eq 0 ]]; then
    cat "$out_json"
    _run_parser parse_json_report "$out_json"
    if [[ "$_parse_rc" -eq 0 ]]; then
      return 0
    elif [[ "$_parse_rc" -eq 2 ]]; then
      return 2
    else
      return 1
    fi
  fi

  # Fall back only when --format json is unavailable on older SDKs.
  if grep -qiE 'unrecognized|unknown (option|argument)|format' "$err_file"; then
    echo "NOTE: --format json unavailable; falling back to console text + grep" >&2
    text_rc=0
    "$DOTNET_BIN" list "$project" package --vulnerable --include-transitive \
      >"$out_text" 2>&1 && text_rc=0 || text_rc=$?
    cat "$out_text"
    if [[ "$text_rc" -ne 0 ]]; then
      echo "ERROR: failed to list packages for $project" >&2
      return 1
    fi
    _run_parser parse_text_report "$out_text"
    if [[ "$_parse_rc" -eq 0 ]]; then
      return 0
    elif [[ "$_parse_rc" -eq 2 ]]; then
      return 2
    else
      return 1
    fi
  fi

  cat "$err_file" >&2 || true
  if [[ -s "$out_json" ]]; then
    cat "$out_json" >&2 || true
  fi
  echo "ERROR: failed to list packages for $project" >&2
  return 1
}

run_default_scan() {
  local -a projects=("$@")
  local tmpdir found=0 rc

  if [[ "${#projects[@]}" -eq 0 ]]; then
    projects=(
      "HelloblueGK.csproj"
      "WebAPI/HelloblueGK.WebAPI.csproj"
      "Tests/HelloblueGK.Tests.csproj"
    )
  fi

  tmpdir="$(mktemp -d)"

  for project in "${projects[@]}"; do
    rc=0
    scan_project "$project" "$tmpdir" && rc=0 || rc=$?
    if [[ "$rc" -eq 2 ]]; then
      found=1
    elif [[ "$rc" -ne 0 ]]; then
      rm -rf "$tmpdir"
      return 1
    fi
  done

  rm -rf "$tmpdir"

  if [[ "$found" -ne 0 ]]; then
    echo "Security scan failed: resolve or upgrade vulnerable NuGet packages." >&2
    return 1
  fi

  echo "No vulnerable NuGet packages reported."
  return 0
}

# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

main() {
  local -a projects=()
  local rc=0

  if [[ $# -eq 0 ]]; then
    run_default_scan && rc=0 || rc=$?
    return "$rc"
  fi

  case "$1" in
    --parse-json)
      if [[ $# -ne 2 ]]; then
        echo "Usage: $0 --parse-json <file>" >&2
        return 1
      fi
      _run_parser parse_json_report "$2"
      if [[ "$_parse_rc" -eq 0 ]]; then
        echo "No vulnerable NuGet packages reported."
        return 0
      fi
      return 1
      ;;
    --parse-text)
      if [[ $# -ne 2 ]]; then
        echo "Usage: $0 --parse-text <file>" >&2
        return 1
      fi
      _run_parser parse_text_report "$2"
      if [[ "$_parse_rc" -eq 0 ]]; then
        echo "No vulnerable NuGet packages reported."
        return 0
      fi
      return 1
      ;;
    --project)
      while [[ $# -gt 0 ]]; do
        if [[ "$1" != "--project" ]]; then
          echo "Usage: $0 --project <csproj> [--project <csproj> ...]" >&2
          return 1
        fi
        shift
        if [[ $# -eq 0 ]]; then
          echo "ERROR: --project requires a path" >&2
          return 1
        fi
        projects+=("$1")
        shift
      done
      run_default_scan "${projects[@]}" && rc=0 || rc=$?
      return "$rc"
      ;;
    -h|--help)
      sed -n '2,20p' "$0" | sed 's/^# \?//'
      return 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      echo "Usage: $0 [--project <csproj> ...] | --parse-json <file> | --parse-text <file>" >&2
      return 1
      ;;
  esac
}

main "$@"
