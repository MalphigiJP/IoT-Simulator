
param(
    [string]$ApiUrl = "http://localhost:5000",
    [int]$Days = 1,
    [int]$WindowMinutes = 1
)

$ErrorActionPreference = "Stop"
$ApiUrl = $ApiUrl.TrimEnd("/")
$Checks = [System.Collections.Generic.List[object]]::new()

function Invoke-IntegrationCheck {
    param(
        [string]$Name,
        [string]$Path,
        [scriptblock]$Validate = $null
    )

    $watch = [System.Diagnostics.Stopwatch]::StartNew()

    try {
        $response = Invoke-RestMethod `
            -Uri "$ApiUrl$Path" `
            -Method Get `
            -TimeoutSec 15 `
            -ErrorAction Stop

        if ($null -ne $Validate) {
            & $Validate $response
        }

        $watch.Stop()

        $Checks.Add([pscustomobject]@{
            Name = $Name
            Status = "PASS"
            TimeMs = [math]::Round($watch.Elapsed.TotalMilliseconds, 1)
            Detail = "GET $Path"
        })

        return $response
    }
    catch {
        $watch.Stop()

        $Checks.Add([pscustomobject]@{
            Name = $Name
            Status = "FAIL"
            TimeMs = [math]::Round($watch.Elapsed.TotalMilliseconds, 1)
            Detail = $_.Exception.Message
        })

        return $null
    }
}

function Add-SkippedCheck {
    param(
        [string]$Name,
        [string]$Reason
    )

    $Checks.Add([pscustomobject]@{
        Name = $Name
        Status = "SKIP"
        TimeMs = 0
        Detail = $Reason
    })
}

Write-Host ""
Write-Host "SMART-X PHASE 7 INTEGRATION VERIFICATION" -ForegroundColor Cyan
Write-Host "API: $ApiUrl"
Write-Host "Historical days: $Days"
Write-Host "Follow-up window: $WindowMinutes minutes"
Write-Host ""

$null = Invoke-IntegrationCheck `
    -Name "API startup" `
    -Path "/"

$sensorResponse = Invoke-IntegrationCheck `
    -Name "Sensor registry" `
    -Path "/api/sensors"

$null = Invoke-IntegrationCheck `
    -Name "Sensor health" `
    -Path "/api/sensors/health"

$null = Invoke-IntegrationCheck `
    -Name "Historical telemetry" `
    -Path "/api/telemetry"

$null = Invoke-IntegrationCheck `
    -Name "Live device registry" `
    -Path "/api/devices/live"

$null = Invoke-IntegrationCheck `
    -Name "Command audit history" `
    -Path "/api/commands/audit?limit=10"

$null = Invoke-IntegrationCheck `
    -Name "Operator search history" `
    -Path "/api/insights/searches?limit=10"

$null = Invoke-IntegrationCheck `
    -Name "Unified interaction history" `
    -Path "/api/insights/interactions?limit=10"

$relationships = Invoke-IntegrationCheck `
    -Name "Historical relationship analysis" `
    -Path "/api/insights/relationships?days=$Days&windowMinutes=$WindowMinutes" `
    -Validate {
        param($report)

        if (
            $report.PSObject.Properties.Name -notcontains
            "patterns"
        ) {
            throw "Relationship report is missing patterns."
        }

        if (
            $report.PSObject.Properties.Name -notcontains
            "observedEpisodes"
        ) {
            throw "Relationship report is missing observedEpisodes."
        }
    }

$recommendations = Invoke-IntegrationCheck `
    -Name "Recommendation ranking" `
    -Path "/api/insights/recommendations?days=$Days&windowMinutes=$WindowMinutes" `
    -Validate {
        param($report)

        if (
            $report.PSObject.Properties.Name -notcontains
            "recommendations"
        ) {
            throw "Recommendation report is missing recommendations."
        }

        if (
            $report.PSObject.Properties.Name -notcontains
            "dataLimitReached"
        ) {
            throw "Recommendation report is missing dataLimitReached."
        }
    }

$sensors = @()

if ($null -ne $sensorResponse) {
    if ($sensorResponse -is [array]) {
        $sensors = @($sensorResponse)
    }
    elseif (
        $sensorResponse.PSObject.Properties.Name -contains
        "sensors"
    ) {
        $sensors = @($sensorResponse.sensors)
    }
    elseif (
        $sensorResponse.PSObject.Properties.Name -contains
        "items"
    ) {
        $sensors = @($sensorResponse.items)
    }
    elseif (
        $sensorResponse.PSObject.Properties.Name -contains
        "data"
    ) {
        $sensors = @($sensorResponse.data)
    }
    elseif (
        $sensorResponse.PSObject.Properties.Name -contains
        "id"
    ) {
        $sensors = @($sensorResponse)
    }
}

$actuator = $sensors |
    Where-Object {
        $_.category -match
        '^(smartplug|irrigationvalve|pumpcontroller)$'
    } |
    Select-Object -First 1

if ($null -ne $actuator) {
    $id = $actuator.id

    $null = Invoke-IntegrationCheck `
        -Name "Actuator control state" `
        -Path "/api/devices/$id/control"

    $null = Invoke-IntegrationCheck `
        -Name "Actuator command history" `
        -Path "/api/devices/$id/commands"

    $null = Invoke-IntegrationCheck `
        -Name "Undo availability" `
        -Path "/api/devices/$id/commands/undo/status"
}
else {
    Add-SkippedCheck `
        -Name "Actuator control state" `
        -Reason "No supported registered actuator resolved."

    Add-SkippedCheck `
        -Name "Actuator command history" `
        -Reason "No supported registered actuator resolved."

    Add-SkippedCheck `
        -Name "Undo availability" `
        -Reason "No supported registered actuator resolved."
}

$passed = @(
    $Checks | Where-Object { $_.Status -eq "PASS" }
).Count

$failed = @(
    $Checks | Where-Object { $_.Status -eq "FAIL" }
).Count

$skipped = @(
    $Checks | Where-Object { $_.Status -eq "SKIP" }
).Count

Write-Host ""
Write-Host "INTEGRATION RESULTS" -ForegroundColor Cyan

$Checks |
    Format-Table Name, Status, TimeMs, Detail -AutoSize -Wrap

Write-Host ""
Write-Host "Passed: $passed" -ForegroundColor Green
Write-Host "Failed: $failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
Write-Host "Skipped: $skipped" -ForegroundColor Yellow

if ($null -ne $relationships) {
    Write-Host ""
    Write-Host "HISTORICAL ANALYSIS" -ForegroundColor Cyan
    Write-Host "Observed episodes: $($relationships.observedEpisodes)"
    Write-Host "Detected patterns: $($relationships.totalDetectedPatterns)"
    Write-Host "Data limit reached: $($relationships.dataLimitReached)"
}

if ($null -ne $recommendations) {
    Write-Host ""
    Write-Host "RECOMMENDATIONS" -ForegroundColor Cyan
    Write-Host "Review: $($recommendations.reviewCount)"
    Write-Host "Monitor: $($recommendations.monitorCount)"
    Write-Host "Blocked: $($recommendations.blockedCount)"
}

$reportPath = Join-Path $PSScriptRoot "Phase7-Integration-Report.json"

$reportData = [pscustomobject]@{
    VerifiedAtUtc = [DateTime]::UtcNow.ToString("o")
    ApiUrl = $ApiUrl
    Days = $Days
    WindowMinutes = $WindowMinutes
    Passed = $passed
    Failed = $failed
    Skipped = $skipped
    Checks = @($Checks.ToArray())
}

$reportData |
    ConvertTo-Json -Depth 8 |
    Set-Content -Path $reportPath -Encoding UTF8

Write-Host ""
Write-Host "Report saved to: $reportPath"

if ($failed -gt 0) {
    exit 1
}

exit 0
