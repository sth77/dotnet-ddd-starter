#Requires -Version 7
<#
.SYNOPSIS
All build gates in the order the README lists them (design §13). Any failure fails the script.
.PARAMETER SkipIntegration
Skip the Testcontainers suite (no Docker daemon reachable from this shell).
#>
param([switch]$SkipIntegration)

$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
# The solution is found, not named, so renaming it does not silently drop every gate below (ADR-020).
$solution = (Get-ChildItem -Path . -Filter '*.slnx' -File | Select-Object -First 1).Name
if (-not $solution) { throw "no .slnx solution found in $(Get-Location)" }
New-Item -ItemType Directory -Force artifacts | Out-Null

function Step($name, [scriptblock]$body) {
    Write-Host "== $name" -ForegroundColor Cyan
    & $body
    if ($LASTEXITCODE -ne 0) { throw "$name failed ($LASTEXITCODE)" }
}

Step 'restore' { dotnet restore $solution }
Step 'format'  { dotnet format $solution --verify-no-changes --no-restore }
Step 'build'   { dotnet build $solution -c Release --no-restore -warnaserror }
Step 'fast tests' {
    dotnet test $solution -c Release --no-build --filter 'FullyQualifiedName!~App.IntegrationTests' `
        --logger 'trx;LogFileName=fast.trx' --results-directory artifacts/test-results
}
if (-not $SkipIntegration) {
    Step 'integration tests' {
        dotnet test tests/App.IntegrationTests/App.IntegrationTests.csproj -c Release --no-build `
            --logger 'trx;LogFileName=integration.trx' --results-directory artifacts/test-results
    }
}
Step 'vulnerable packages' {
    $out = dotnet list $solution package --vulnerable --include-transitive
    $out | Set-Content artifacts/vulnerable.txt
    if ($out -match 'has the following vulnerable packages') { Write-Error 'vulnerable packages found' }
}
Step 'licence gate' {
    if (-not (Get-Command nuget-license -ErrorAction SilentlyContinue)) { dotnet tool install --global nuget-license | Out-Null }
    nuget-license --input $solution --include-transitive --allowed-license-types build/allowed-licenses.json `
        --licenseurl-to-license-mappings build/license-url-mappings.json `
        --output Markdown --file-output artifacts/licences.md
}
Step 'template regeneration check' { pwsh -NoProfile -File build/regen-check.ps1 }

Write-Host 'all gates passed' -ForegroundColor Green
