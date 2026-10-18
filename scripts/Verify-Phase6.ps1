
param(
    [string]$ApiUrl = "http://localhost:5000",
    [int]$Days = 7,
    [int]$WindowMinutes = 30
)

$ErrorActionPreference = "Stop"

$uri = "$ApiUrl/api/insights/recommendations?days=$Days&windowMinutes=$WindowMinutes"

Write-Host "Checking Smart-X recommendation API..." -ForegroundColor Cyan

$report = Invoke-RestMethod -Uri $uri -Method Get

if ($null -eq $report.recommendations) {
    throw "Recommendation response is missing the recommendations collection."
}

$items = @($report.recommendations)

$reviewCount = @(
    $items | Where-Object { $_.readiness -eq "Review" }
).Count

$monitorCount = @(
    $items | Where-Object { $_.readiness -eq "Monitor" }
).Count

$blockedCount = @(
    $items | Where-Object { $_.readiness -eq "Blocked" }
).Count

if ($reviewCount -ne [int]$report.reviewCount) {
    throw "Review count does not match recommendation records."
}

if ($monitorCount -ne [int]$report.monitorCount) {
    throw "Monitor count does not match recommendation records."
}

if ($blockedCount -ne [int]$report.blockedCount) {
    throw "Blocked count does not match recommendation records."
}

foreach ($item in $items) {
    if ($item.score -lt 0 -or $item.score -gt 100) {
        throw "Recommendation $($item.id) has an invalid score."
    }

    if ($item.readiness -notin @("Review", "Monitor", "Blocked")) {
        throw "Recommendation $($item.id) has an invalid readiness state."
    }

    if ($null -eq $item.scoreBreakdown) {
        throw "Recommendation $($item.id) has no score explanation."
    }

    $breakdown = $item.scoreBreakdown

    $total = [double]$breakdown.repetition +
        [double]$breakdown.followUpRate +
        [double]$breakdown.recency +
        [double]$breakdown.searchContext +
        [double]$breakdown.currentAnomaly +
        [double]$breakdown.actuatorReadiness

    if ([Math]::Abs($total - [double]$item.score) -gt 0.2) {
        throw "Recommendation $($item.id) has an inconsistent score."
    }

    if ($report.dataLimitReached -and $item.readiness -ne "Blocked") {
        throw "Incomplete historical data produced an unblocked recommendation."
    }
}

Write-Host ""
Write-Host "Phase 6 API verification passed." -ForegroundColor Green
Write-Host "Recommendations: $($items.Count)"
Write-Host "Review: $reviewCount"
Write-Host "Monitor: $monitorCount"
Write-Host "Blocked: $blockedCount"
Write-Host "Historical patterns: $($report.historicalPatterns)"
Write-Host "Data truncated: $($report.dataLimitReached)"
Write-Host "No command submission request was issued by this script."
