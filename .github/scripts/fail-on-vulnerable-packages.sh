#!/usr/bin/env bash
# Fail CI when NuGet reports vulnerable packages (direct or transitive).
set -euo pipefail

projects=(
  "HelloblueGK.csproj"
  "WebAPI/HelloblueGK.WebAPI.csproj"
  "Tests/HelloblueGK.Tests.csproj"
)

tmpdir="$(mktemp -d)"
trap 'rm -rf "$tmpdir"' EXIT
found=0

for project in "${projects[@]}"; do
  out="$tmpdir/$(basename "$project").txt"
  echo "== Vulnerable packages: $project =="
  if ! dotnet list "$project" package --vulnerable --include-transitive >"$out" 2>&1; then
    cat "$out"
    echo "ERROR: failed to list packages for $project" >&2
    exit 1
  fi
  cat "$out"

  # Match NuGet's vulnerable package table rows (has a CVE/GHSA-looking advisory column).
  if rg -q 'CVE-[0-9]{4}-[0-9]+|GHSA-[A-Za-z0-9-]+' "$out"; then
    echo "ERROR: vulnerable packages found in $project" >&2
    found=1
  fi
done

if [[ "$found" -ne 0 ]]; then
  echo "Security scan failed: resolve or upgrade vulnerable NuGet packages." >&2
  exit 1
fi

echo "No vulnerable NuGet packages reported."
