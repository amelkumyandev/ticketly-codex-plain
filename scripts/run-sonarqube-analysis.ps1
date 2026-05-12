param(
    [string]$ProjectKey = "ticketly-plain",
    [string]$SonarHostUrl = "http://localhost:9000",
    [string]$SonarToken = $env:SONAR_TOKEN
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($SonarToken)) {
    Write-Error "Set SONAR_TOKEN or pass -SonarToken before running analysis."
    exit 1
}

$repoRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")
$coverageFile = Join-Path $repoRoot "coverage/coverage.opencover.xml"
$solutionPath = Join-Path $repoRoot "Ticketly.sln"

dotnet tool restore

dotnet tool run dotnet-sonarscanner begin `
    /k:$ProjectKey `
    "/d:sonar.host.url=$SonarHostUrl" `
    "/d:sonar.token=$SonarToken" `
    "/d:sonar.cs.opencover.reportsPaths=$coverageFile"

dotnet build $solutionPath --no-incremental

& (Join-Path $PSScriptRoot "run-tests-with-coverage.ps1") -NoBuild

dotnet tool run dotnet-sonarscanner end "/d:sonar.token=$SonarToken"
