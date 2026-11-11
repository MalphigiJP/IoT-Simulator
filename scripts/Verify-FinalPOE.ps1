
param(
    [string]$ApiUrl = "http://localhost:5000",
    [switch]$SkipApi
)

$ErrorActionPreference = "Stop"
$ApiUrl = $ApiUrl.TrimEnd('/')
$root = Split-Path -Parent $PSScriptRoot

$results = [System.Collections.Generic.List[object]]::new()
$preview = $null

function Assert-Check {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Run-Check {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-Host "`n=== $Name ===" -ForegroundColor Cyan

    try {
        & $Action

        $results.Add([pscustomobject]@{
            Check = $Name
            Result = "PASS"
            Details = "Completed successfully"
        })

        Write-Host "PASS: $Name" -ForegroundColor Green
    }
    catch {
        $message = $_.Exception.Message

        $results.Add([pscustomobject]@{
            Check = $Name
            Result = "FAIL"
            Details = $message
        })

        Write-Host "FAIL: $Name" -ForegroundColor Red
        Write-Host $message -ForegroundColor Red
    }
}

function Get-Api {
    param([string]$Path)

    return Invoke-RestMethod `
        -Uri "$ApiUrl$Path" `
        -Method Get `
        -TimeoutSec 60
}

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host " SMART-X FINAL POE VERIFICATION" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Repository: $root"
Write-Host "API: $ApiUrl"

Push-Location $root

try {
    Run-Check "ASP.NET Core API build" {
        & dotnet build ".\SmartX.Api\SmartX.Api.csproj" --nologo

        Assert-Check ($LASTEXITCODE -eq 0) "API build failed."
    }

    Run-Check "Complete backend test suite" {
        & dotnet test ".\SmartX.Api.Tests\SmartX.Api.Tests.csproj" `
            --nologo `
            --logger "console;verbosity=normal"

        Assert-Check ($LASTEXITCODE -eq 0) "Backend tests failed."
    }

    Run-Check "React production build" {
        Push-Location ".\Frontend\smartx-dashboard"

        try {
            & npm run build
            Assert-Check ($LASTEXITCODE -eq 0) "React build failed."
        }
        finally {
            Pop-Location
        }
    }

    if (-not $SkipApi) {
        Run-Check "API health check" {
            $health = Get-Api "/"
            Assert-Check ($null -ne $health) "API health endpoint returned no result."
        }

        Run-Check "Registered mesh snapshot" {
            $preview = Get-Api "/api/mesh/preview"

            Assert-Check ($preview.gatewayId -eq 0) "Unexpected gateway ID."

            $nodes = @($preview.nodes)
            $health = @($preview.deviceHealth)
            $links = @($preview.links)

            Assert-Check (
                $nodes.Count -eq ([int]$preview.registeredSensorCount + 1)
            ) "Node count does not match the registered sensors."

            Assert-Check (
                $health.Count -eq [int]$preview.registeredSensorCount
            ) "Device health count does not match the registry."

            Assert-Check (
                (@($nodes | Where-Object { $_.isGateway })).Count -eq 1
            ) "The mesh must have exactly one gateway."

            $totalHealth = [int]$preview.onlineCount +
                [int]$preview.delayedCount +
                [int]$preview.offlineCount +
                [int]$preview.unknownCount

            Assert-Check (
                $totalHealth -eq [int]$preview.registeredSensorCount
            ) "Health counters do not add up."

            Write-Host "Registered: $($preview.registeredSensorCount)"
            Write-Host "Online: $($preview.onlineCount)"
            Write-Host "Delayed: $($preview.delayedCount)"
            Write-Host "Offline: $($preview.offlineCount)"
            Write-Host "Unknown: $($preview.unknownCount)"
            Write-Host "Logical links: $($links.Count)"
            Write-Host "Reachable: $($preview.reachableSensorCount)"
        }

        Run-Check "Minimum Spanning Tree consistency" {
            $mst = Get-Api "/api/mesh/mst"
            $connected = @($mst.connectedNodeIds)
            $edges = @($mst.edges)

            Assert-Check ($connected -contains 0) "MST is missing the gateway."

            Assert-Check (
                $edges.Count -eq [Math]::Max(0, $connected.Count - 1)
            ) "MST edge count is inconsistent."

            $totalCost = 0.0

            foreach ($edge in $edges) {
                Assert-Check (
                    ($connected -contains $edge.fromId) -and
                    ($connected -contains $edge.toId)
                ) "An MST edge references an unconnected node."

                $totalCost += [double]$edge.cost
            }

            Assert-Check (
                [Math]::Abs($totalCost - [double]$mst.totalCost) -lt 0.0001
            ) "MST total cost does not match its edges."

            Write-Host "Connected sensors: $($mst.connectedSensorCount)"
            Write-Host "Selected MST edges: $($edges.Count)"
            Write-Host "Total simulated cost: $($mst.totalCost)"
        }

        if ($null -ne $preview -and @($preview.deviceHealth).Count -gt 0) {
            $sensor = @($preview.deviceHealth)[0]
            $id = [int]$sensor.sensorId

            Run-Check "Dijkstra shortest-route endpoint" {
                $route = Get-Api "/api/mesh/route/$id"

                Assert-Check ($route.sourceId -eq $id) "Incorrect route source."

                if ($route.reachable) {
                    $path = @($route.path)

                    Assert-Check ($path.Count -ge 2) "Reachable path is too short."
                    Assert-Check ($path[0] -eq $id) "Path has an incorrect start."
                    Assert-Check ($path[-1] -eq 0) "Path does not end at gateway."
                    Assert-Check (
                        @($route.hops).Count -eq $path.Count - 1
                    ) "Hop count is inconsistent."
                }
                else {
                    Assert-Check (
                        @($route.path).Count -eq 0
                    ) "Unreachable route unexpectedly contains a path."

                    Assert-Check (
                        -not [string]::IsNullOrWhiteSpace($route.reason)
                    ) "Unreachable route has no explanation."
                }

                Write-Host "Reachable: $($route.reachable)"
                Write-Host "Path: $($route.path -join ' -> ')"
                Write-Host "Cost: $($route.totalCost)"
            }

            Run-Check "MAC-address sensor lookup" {
                $mac = [uri]::EscapeDataString($sensor.macAddress)

                $result = Get-Api "/api/mesh/nodes/by-mac?macAddress=$mac"

                Assert-Check (
                    $result.node.id -eq $id
                ) "MAC lookup returned the wrong sensor."
            }

            Run-Check "Historical SQL telemetry endpoint" {
                $records = @(Get-Api "/api/sensors/$id/telemetry")

                Assert-Check (
                    $records.Count -le 100
                ) "Telemetry endpoint returned more than 100 readings."

                foreach ($record in $records) {
                    Assert-Check (
                        $record.sensorId -eq $id
                    ) "Telemetry history contains another sensor's records."
                }

                Write-Host "Historical records retrieved: $($records.Count)"
            }
        }
        else {
            Write-Host "No registered sensors found for route-specific API checks." -ForegroundColor Yellow
        }

        Run-Check "Live device registry and alert services" {
            $null = Get-Api "/api/devices/live"
            $null = Get-Api "/api/alerts/status"
            $null = Get-Api "/api/telemetry/queue/status"
            $null = Get-Api "/api/telemetry/timeline/status"
        }
    }
}
finally {
    Pop-Location
}

$reportDir = Join-Path $root "test-results"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null

$reportPath = Join-Path $reportDir (
    "FinalPOE-Verification-{0}.csv" -f (Get-Date -Format "yyyyMMdd-HHmmss")
)

$results | Export-Csv `
    -Path $reportPath `
    -NoTypeInformation `
    -Encoding UTF8

Write-Host "`n========================================="
Write-Host " FINAL POE RESULTS" -ForegroundColor Cyan
Write-Host "=========================================" 

$results | Format-Table Check, Result -AutoSize

$failed = @($results | Where-Object { $_.Result -eq "FAIL" }).Count
$passed = @($results | Where-Object { $_.Result -eq "PASS" }).Count

Write-Host "Passed: $passed"
Write-Host "Failed: $failed"
Write-Host "Report: $reportPath"

if ($failed -gt 0) {
    Write-Host "Some verification checks failed."
    exit 1
}

Write-Host "All executed verification checks passed."
exit 0
