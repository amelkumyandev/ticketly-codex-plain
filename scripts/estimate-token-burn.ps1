param(
    [string]$Path = "TOKEN_BURN.md"
)

if (-not (Test-Path -LiteralPath $Path)) {
    Write-Error "Token burn file not found: $Path"
    exit 1
}

$content = Get-Content -LiteralPath $Path -Raw
$totalCharacters = $content.Length
$estimatedTotalTokens = [math]::Ceiling($totalCharacters / 4)

$inputCharacters = 0
$outputCharacters = 0

$inputMatches = [regex]::Matches($content, "(?ms)^## Task \d+ Input\s*(.*?)(?=^## Task \d+ Output|\z)")
foreach ($match in $inputMatches) {
    $inputCharacters += $match.Groups[1].Value.Length
}

$outputMatches = [regex]::Matches($content, "(?ms)^## Task \d+ Output\s*(.*?)(?=^## Task \d+ Input|\z)")
foreach ($match in $outputMatches) {
    $outputCharacters += $match.Groups[1].Value.Length
}

$estimatedInputTokens = [math]::Ceiling($inputCharacters / 4)
$estimatedOutputTokens = [math]::Ceiling($outputCharacters / 4)

Write-Output "estimated total tokens: $estimatedTotalTokens"
Write-Output "estimated input tokens: $estimatedInputTokens"
Write-Output "estimated output tokens: $estimatedOutputTokens"
Write-Output "estimation method: characters / 4 from $Path"
