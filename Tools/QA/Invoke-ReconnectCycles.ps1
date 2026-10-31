param(
    [ValidateRange(1,1000)][int]$Cycles = 100,
    [string]$Executable
)

# One cycle is one complete `Invoke-NetworkSmoke.ps1 -Scenario reconnect` run: fresh host and client processes,
# a new Photon room and join, ten seconds of play, a host-side disconnect of the client, its credential-bound retry
# into the held seat and ten more seconds of play. Fresh processes per cycle do not measure accumulation on one host.
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$smokeRoot = Join-Path $project 'Artifacts/Smoke'
# Pin one build for every cycle even if latest-build.json changes during the run.
if (-not $Executable) { $Executable = (Get-Content -LiteralPath (Join-Path $smokeRoot 'latest-build.json') -Raw | ConvertFrom-Json).executable }
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$summaryPath = Join-Path $smokeRoot ((Get-Date -Format 'yyyyMMdd-HHmmss-fff')+'-reconnect-cycles.json')
$summary = [ordered]@{scenario='reconnect'; cycle='Fresh host+client processes: one Photon join, one host-forced disconnect and one credential-bound reconnect into the held seat.'
    executable=$Executable; cyclesRequested=$Cycles; cyclesRun=0; passed=0; failed=0; startedUtc=[DateTime]::UtcNow.ToString('o'); completedUtc=$null
    durationSeconds=$null; reconnectSeconds=$null; failures=@(); runs=@()}
function Get-Range($Values) { $m = @($Values) | Measure-Object -Minimum -Maximum -Average; if($m.Count){[ordered]@{min=$m.Minimum; mean=$m.Average; max=$m.Maximum}} }
for ($cycle = 1; $cycle -le $Cycles; $cycle++) {
    $began = [DateTime]::UtcNow; $outcome = $null; $failure = $null; $detail = $null
    # The smoke wrapper emits its summary object before throwing on failure; keep it to locate the run.
    try { & (Join-Path $PSScriptRoot 'Invoke-NetworkSmoke.ps1') -Scenario reconnect -Executable $Executable | ForEach-Object { $outcome = $_ } }
    catch { $failure = $_.Exception.Message }
    if ($outcome.Evidence) { try { $detail = Get-Content -LiteralPath (Join-Path $outcome.Evidence 'result.json') -Raw | ConvertFrom-Json } catch { } }
    $passed = $null -eq $failure -and $outcome.Passed -eq $true -and $detail.passed -eq $true
    $run = [pscustomobject]@{cycle=$cycle; passed=$passed; run=$outcome.Evidence; error=$(if($detail.error){$detail.error}else{$failure})
        seconds=([DateTime]::UtcNow-$began).TotalSeconds; reconnectSeconds=$detail.host.reconnectSeconds; retainedAvatar=$detail.host.retainedAvatar}
    $summary.runs += $run
    if ($passed) { $summary.passed++ } else { $summary.failed++; $summary.failures += $run }
    $summary.cyclesRun = $cycle
    $summary.durationSeconds = Get-Range $summary.runs.seconds
    $summary.reconnectSeconds = Get-Range @($summary.runs | Where-Object passed | ForEach-Object reconnectSeconds)
    $summary.completedUtc = [DateTime]::UtcNow.ToString('o')
    # Rewritten every cycle so an interrupted run keeps its evidence.
    $summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $summaryPath
    Write-Host ("cycle {0}/{1}: {2} {3}" -f $cycle, $Cycles, $(if($passed){'passed'}else{'FAILED'}), $outcome.Evidence)
}
[pscustomobject]@{Passed=($summary.failed -eq 0); Cycles=$Cycles; PassedCycles=$summary.passed; Summary=$summaryPath}
if ($summary.failed) { throw "$($summary.failed) of $Cycles reconnect cycles failed; inspect $summaryPath." }
