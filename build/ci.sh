#!/usr/bin/env bash
# All build gates in the order the README lists them (design §13). Any failure fails the job.
# Usage: CI=true ./build/ci.sh            (CI=true switches restore to --locked-mode)
set -euo pipefail
cd "$(dirname "$0")/.."

# The solution is found, not named, so renaming it does not silently drop every gate below (ADR-020).
SOLUTION=$(ls -1 ./*.slnx | head -n 1)
[ -n "$SOLUTION" ] || { echo "no .slnx solution found in $PWD" >&2; exit 1; }
mkdir -p artifacts

echo "== restore"
dotnet restore "$SOLUTION"

echo "== format"
dotnet format "$SOLUTION" --verify-no-changes --no-restore

echo "== build (warnings as errors, analyzers, banned APIs, licence denylist)"
dotnet build "$SOLUTION" -c Release --no-restore -warnaserror

echo "== fast tests: domain, model + drift gate, architecture"
dotnet test "$SOLUTION" -c Release --no-build --filter "FullyQualifiedName!~App.IntegrationTests" \
  --logger "trx;LogFileName=fast.trx" --results-directory artifacts/test-results

echo "== integration tests (Testcontainers)"
dotnet test tests/App.IntegrationTests/App.IntegrationTests.csproj -c Release --no-build \
  --logger "trx;LogFileName=integration.trx" --results-directory artifacts/test-results

echo "== vulnerable packages"
dotnet list "$SOLUTION" package --vulnerable --include-transitive | tee artifacts/vulnerable.txt
if grep -qE "has the following vulnerable packages" artifacts/vulnerable.txt; then
  echo "vulnerable packages found"; exit 1
fi

echo "== licence gate"
if ! command -v nuget-license >/dev/null 2>&1; then
  dotnet tool install --global nuget-license >/dev/null
  export PATH="$PATH:$HOME/.dotnet/tools"
fi
nuget-license --input "$SOLUTION" --include-transitive \
  --allowed-license-types build/allowed-licenses.json \
  --licenseurl-to-license-mappings build/license-url-mappings.json \
  --output Markdown --file-output artifacts/licences.md

echo "== template regeneration check"
./build/regen-check.sh

echo "all gates passed"
