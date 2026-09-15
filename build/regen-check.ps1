#Requires -Version 7
<#
.SYNOPSIS
The "-Phygen-it" gate (design §12): proves the dotnet new templates still produce code that builds,
formats and passes the fast test suites against the current starter.

.DESCRIPTION
Copies the repository to a temp directory, installs the template pack from templates/, generates the
Todo aggregate and the Country reference data into the copy, applies the manual wiring step that
dotnet new cannot inject (the repository registration), checks the generated SQL migrations, then builds
with warnings as errors, verifies formatting and runs the fast test suites. The temp copy and the template
installation are removed again; any failure exits non-zero.

Both templates default to migration version V0003, so the reference data is generated with V0004: the
version prefixes have to stay unique (SqlMigratorTests).

.PARAMETER KeepTemp
Leave the temp copy in place for inspection (the path is printed).
#>
param([switch]$KeepTemp)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$templates = Join-Path $repoRoot 'templates'
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("ddd-regen-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
$installed = $false
$failed = $null

function Step($name, [scriptblock]$body) {
    Write-Host "== $name" -ForegroundColor Cyan
    & $body
    if ($LASTEXITCODE -ne 0) { throw "$name failed ($LASTEXITCODE)" }
}

# Inserts $lines directly after the last line that matches $anchor. Writes LF endings (see .editorconfig).
function Add-AfterLine([string]$path, [string]$anchor, [string[]]$lines) {
    $content = [System.IO.File]::ReadAllText($path) -replace "`r`n", "`n"
    $all = $content.Split("`n")
    $index = -1
    for ($i = 0; $i -lt $all.Length; $i++) { if ($all[$i] -match $anchor) { $index = $i } }
    if ($index -lt 0) { throw "anchor '$anchor' not found in $path" }
    $updated = @($all[0..$index]) + $lines + @($all[($index + 1)..($all.Length - 1)])
    [System.IO.File]::WriteAllText($path, ($updated -join "`n"))
}

# Static check on a generated migration: the file the template promised exists and creates the table.
function Assert-Migration([string]$path, [string]$statement) {
    if (-not (Test-Path $path)) { throw "generated migration $path is missing" }
    if (-not (Select-String -Path $path -SimpleMatch -Pattern $statement -Quiet)) {
        throw "generated migration $path does not contain '$statement'"
    }
    Write-Host "   $([System.IO.Path]::GetFileName($path)): $statement"
}

try {
    Write-Host "== copying the repository to $temp" -ForegroundColor Cyan
    New-Item -ItemType Directory -Force $temp | Out-Null
    robocopy $repoRoot $temp /E /XD bin obj .git artifacts TestResults .vs .idea /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "copy failed ($LASTEXITCODE)" }
    $global:LASTEXITCODE = 0

    Step 'installing the template pack' { dotnet new install $templates --force }
    $installed = $true

    Push-Location $temp
    try {
        Step 'dotnet new ddd-aggregate -n Todo' { dotnet new ddd-aggregate -n Todo }
        Step 'dotnet new ddd-refdata -n Country --migration-version V0004' { dotnet new ddd-refdata -n Country --migration-version V0004 }

        Write-Host '== applying the manual post-action (repository registration)' -ForegroundColor Cyan
        # Anchored on the scaffolding marker and on the last domain using, so the check keeps working after a
        # project has deleted every module the starter shipped with (ADR-020).
        $registrations = Join-Path $temp 'src/App.Infrastructure/InfrastructureServiceCollectionExtensions.cs'
        Add-AfterLine $registrations '^using App\.Domain\.[A-Za-z]+;$' @('using App.Domain.Todo;')
        Add-AfterLine $registrations '<ddd-scaffold:repositories>' @(
            '        services.AddScoped<ITodos, Todos>();',
            '        services.AddScoped<ICountries, Countries>();')

        Write-Host '== checking the generated SQL migrations' -ForegroundColor Cyan
        Assert-Migration (Join-Path $temp 'src/App.Infrastructure/Migrations/V0003__add_todos.sql') 'CREATE TABLE todos'
        Assert-Migration (Join-Path $temp 'src/App.Infrastructure/Migrations/V0004__add_countries.sql') 'CREATE TABLE countries'

        # The solution is found, not named: a project that renames it keeps this gate (ADR-020).
        $solution = (Get-ChildItem -Path $temp -Filter '*.slnx' -File | Select-Object -First 1).Name
        if (-not $solution) { throw "no .slnx solution found in $temp" }

        Step 'restore' { dotnet restore $solution }
        Step 'build -warnaserror' { dotnet build $solution --no-restore -warnaserror }
        Step 'format --verify-no-changes' { dotnet format $solution --verify-no-changes --no-restore }

        foreach ($project in 'tests/App.Domain.Tests', 'tests/App.ArchitectureTests', 'tests/App.Infrastructure.Tests') {
            Step "test $project" { dotnet test $project --no-build }
        }
    }
    finally {
        Pop-Location
    }
}
catch {
    $failed = $_
}
finally {
    if ($installed) {
        Write-Host '== uninstalling the template pack' -ForegroundColor Cyan
        dotnet new uninstall $templates | Out-Null
        $global:LASTEXITCODE = 0
    }
    if ($KeepTemp) {
        Write-Host "temp copy kept at $temp"
    }
    elseif (Test-Path $temp) {
        Remove-Item -Recurse -Force $temp -ErrorAction SilentlyContinue
    }
}

if ($failed) {
    Write-Host "regeneration check FAILED: $failed" -ForegroundColor Red
    exit 1
}

Write-Host 'regeneration check passed' -ForegroundColor Green
