param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")
$coverageDirectory = Join-Path $repoRoot "coverage"
$coverageFile = Join-Path $coverageDirectory "coverage.opencover.xml"

New-Item -ItemType Directory -Force -Path $coverageDirectory | Out-Null

$testArgs = @(
    "test",
    (Join-Path $repoRoot "tests/Ticketly.Tests/Ticketly.Tests.csproj"),
    "/p:CollectCoverage=true",
    "/p:CoverletOutputFormat=opencover",
    "/p:CoverletOutput=$coverageFile"
)

if ($NoBuild) {
    $testArgs += "--no-build"
}

dotnet @testArgs

Write-Output "Coverage report: $coverageFile"

