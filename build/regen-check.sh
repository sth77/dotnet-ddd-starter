#!/usr/bin/env bash
# The "-Phygen-it" gate (design §12): proves the dotnet new templates still produce code that builds,
# formats and passes the fast test suites against the current starter.
#
# Copies the repository to a temp directory, installs the template pack from templates/, generates the
# Todo aggregate and the Country reference data into the copy, applies the manual wiring step that dotnet new
# cannot inject (the repository registration), checks the generated SQL migrations, then builds with warnings
# as errors, verifies formatting and runs the fast test suites. The temp copy and the template installation
# are removed again; any failure exits non-zero.
#
# Both templates default to migration version V0003, so the reference data is generated with V0004: the
# version prefixes have to stay unique (SqlMigratorTests).
#
# Usage: ./build/regen-check.sh [--keep-temp]
set -euo pipefail

KEEP_TEMP=${1:-}
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TEMPLATES="$REPO_ROOT/templates"
TEMP="$(mktemp -d "${TMPDIR:-/tmp}/ddd-regen-XXXXXXXX")"
INSTALLED=0

cleanup() {
  local status=$?
  if [ "$INSTALLED" = "1" ]; then
    echo "== uninstalling the template pack"
    dotnet new uninstall "$TEMPLATES" >/dev/null 2>&1 || true
  fi
  if [ "$KEEP_TEMP" = "--keep-temp" ]; then
    echo "temp copy kept at $TEMP"
  else
    rm -rf "$TEMP"
  fi
  if [ "$status" -ne 0 ]; then
    echo "regeneration check FAILED"
  fi
  exit "$status"
}
trap cleanup EXIT

# Asserts that an edit actually changed something, so a renamed anchor fails loudly instead of silently.
assert_contains() {
  grep -qF "$2" "$1" || { echo "post-action edit did not apply: '$2' missing from $1"; exit 1; }
}

# Static check on a generated migration: the file the template promised exists and creates the table.
assert_migration() {
  [ -f "$1" ] || { echo "generated migration $1 is missing"; exit 1; }
  grep -qF "$2" "$1" || { echo "generated migration $1 does not contain '$2'"; exit 1; }
  echo "   $(basename "$1"): $2"
}

echo "== copying the repository to $TEMP"
tar -cf - --exclude=./.git --exclude=bin --exclude=obj --exclude=artifacts \
  --exclude=TestResults --exclude=.vs --exclude=.idea -C "$REPO_ROOT" . | tar -xf - -C "$TEMP"

echo "== installing the template pack"
dotnet new install "$TEMPLATES" --force
INSTALLED=1

cd "$TEMP"

echo "== dotnet new ddd-aggregate -n Todo"
dotnet new ddd-aggregate -n Todo

echo "== dotnet new ddd-refdata -n Country --migration-version V0004"
dotnet new ddd-refdata -n Country --migration-version V0004

echo "== applying the manual post-action (repository registration)"
REGISTRATIONS=src/App.Infrastructure/InfrastructureServiceCollectionExtensions.cs

# Anchored on the scaffolding marker and on the last domain using, so this keeps working after a
# project has deleted every module the starter shipped with.
LAST_DOMAIN_USING=$(grep -n '^using App\.Domain\.[A-Za-z]*;$' "$REGISTRATIONS" | tail -n 1 | cut -d: -f1)
sed -i "${LAST_DOMAIN_USING}a using App.Domain.Todo;" "$REGISTRATIONS"
sed -i 's|^\( *\)// <ddd-scaffold:repositories>.*$|\1services.AddScoped<ITodos, Todos>();\n\1services.AddScoped<ICountries, Countries>();\n&|' "$REGISTRATIONS"
assert_contains "$REGISTRATIONS" 'using App.Domain.Todo;'
assert_contains "$REGISTRATIONS" 'services.AddScoped<ITodos, Todos>();'
assert_contains "$REGISTRATIONS" 'services.AddScoped<ICountries, Countries>();'

echo "== checking the generated SQL migrations"
assert_migration src/App.Infrastructure/Migrations/V0003__add_todos.sql 'CREATE TABLE todos'
assert_migration src/App.Infrastructure/Migrations/V0004__add_countries.sql 'CREATE TABLE countries'

echo "== restore"
# The solution is found, not named: a project that renames it keeps this gate.
SOLUTION=$(ls -1 ./*.slnx | head -n 1)
[ -n "$SOLUTION" ] || { echo "no .slnx solution found in $PWD" >&2; exit 1; }
dotnet restore "$SOLUTION"

echo "== build -warnaserror"
dotnet build "$SOLUTION" --no-restore -warnaserror

echo "== format --verify-no-changes"
dotnet format "$SOLUTION" --verify-no-changes --no-restore

for project in tests/App.Domain.Tests tests/App.ArchitectureTests tests/App.Infrastructure.Tests; do
  echo "== test $project"
  dotnet test "$project" --no-build
done

echo "regeneration check passed"
