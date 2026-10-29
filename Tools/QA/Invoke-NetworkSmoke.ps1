param(
    [ValidateSet('basic','reconnect','migration','migration-crash','combat','laser-combat','laser-moving-cover','capacity','late-join','team-switch')][string]$Scenario = 'basic',
    [string]$Executable,
    [ValidateRange(30,900)][int]$TimeoutSeconds = 150,
    [ValidateRange(10,600)][int]$SoakSeconds = 10
)

$ErrorActionPreference = 'Stop'
$effectiveTimeoutSeconds = if($Scenario -eq 'capacity'){[Math]::Max($TimeoutSeconds,$SoakSeconds+150)}else{$TimeoutSeconds}
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$smokeRoot = Join-Path $project 'Artifacts/Smoke'
if (-not $Executable) {
    $build = Get-Content -LiteralPath (Join-Path $smokeRoot 'latest-build.json') -Raw | ConvertFrom-Json
    $Executable = $build.executable
}
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$allowed = [IO.Path]::GetFullPath((Join-Path $project 'Artifacts/Unity')) + [IO.Path]::DirectorySeparatorChar
if (-not $Executable.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path $Executable -Leaf) -ne 'WildstoneArena.exe') {
    throw 'Use a Wildstone test player built under this workspace Artifacts/Unity directory.'
}
$run = Join-Path $smokeRoot ((Get-Date -Format 'yyyyMMdd-HHmmss-fff')+'-'+$Scenario)
New-Item -ItemType Directory -Path $run -Force | Out-Null
$hostStatus = Join-Path $run 'host.json'
$clientStatus = Join-Path $run 'client.json'
$owned = [Collections.Generic.List[Diagnostics.Process]]::new()
$roles = [Collections.Generic.List[string]]::new()
$crashInjected = $false
$capacityMemory = @{}
function Read-Status([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        try {
            $stream = [IO.FileStream]::new($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
            $reader = [IO.StreamReader]::new($stream)
            try { return $reader.ReadToEnd() | ConvertFrom-Json }
            finally { $reader.Dispose() }
        }
        catch { return $null }
    }
    return $null
}
function Get-CapacityMemorySample([Diagnostics.Process]$Peer,[double]$HoldSeconds) {
    $Peer.Refresh()
    if($Peer.HasExited){return $null}
    try {
        $working=[long]$Peer.WorkingSet64
        $private=[long]$Peer.PrivateMemorySize64
    }
    catch { $Peer.Refresh(); if($Peer.HasExited){return $null}; throw }
    if($working -le 0 -or $private -le 0){$Peer.Refresh(); if($Peer.HasExited){return $null}; throw 'Windows process memory counters were unavailable.'}
    return [pscustomobject]@{utc=[DateTime]::UtcNow.ToString('o');holdSeconds=$HoldSeconds;workingSetBytes=$working;privateBytes=$private}
}
function Start-Peer([string]$Role,[string]$Room) {
    $statusPath = Join-Path $run ($Role+'.json')
    $peerPath = if($Role -eq 'host'){$clientStatus}else{$hostStatus}
    $arguments = @('-batchmode','-nographics','-logFile',('"'+(Join-Path $run ($Role+'.log'))+'"'),
        '--wildstone-smoke-role',$Role,'--wildstone-smoke-scenario',$Scenario,
        '--wildstone-smoke-output',('"'+$statusPath+'"'),'--wildstone-smoke-peer-output',('"'+$peerPath+'"'))
    if($Scenario -eq 'capacity'){$arguments += @('--wildstone-smoke-soak-seconds',$SoakSeconds.ToString([Globalization.CultureInfo]::InvariantCulture))}
    if($Room){$arguments += @('--wildstone-smoke-room',$Room)}
    $peer = Start-Process -FilePath $Executable -ArgumentList $arguments -WorkingDirectory (Split-Path $Executable) -PassThru -WindowStyle Hidden
    $owned.Add($peer)
    $roles.Add($Role)
    return $peer
}
$result = [ordered]@{scenario=$Scenario; capacitySoakSeconds=$(if($Scenario -eq 'capacity'){$SoakSeconds}else{$null}); timeoutSeconds=$effectiveTimeoutSeconds; executable=$Executable; startedUtc=[DateTime]::UtcNow.ToString('o'); passed=$false; error=$null; run=$run}
try {
    $deadline = [DateTime]::UtcNow.AddSeconds($effectiveTimeoutSeconds)
    $hostPeer = Start-Peer 'host' ''
    do {
        $hostState = Read-Status $hostStatus
        if($hostState.finished -and -not $hostState.success){throw ('Host failed: '+$hostState.error)}
        if($hostPeer.HasExited){throw 'Host exited before publishing its room.'}
        if([DateTime]::UtcNow -ge $deadline){throw 'Host room creation timed out.'}
        if(-not $hostState.room){Start-Sleep -Milliseconds 500}
    } while(-not $hostState.room)
    if($hostState.room -notmatch '^[A-Z0-9]{4,12}$'){throw 'Host produced an invalid room code.'}
    $clientPeer = Start-Peer 'client' $hostState.room
    if($Scenario -in @('migration','migration-crash')){ $thirdPeer = Start-Peer 'client2' $hostState.room }
    if($Scenario -eq 'capacity') {
        for($slot=2;$slot -le 7;$slot++) {
            $previousRole = if($slot -eq 2){'client'}else{'client'+($slot-1)}
            do {
                $previousState=Read-Status (Join-Path $run ($previousRole+'.json'))
                if($previousState.finished -and -not $previousState.success){throw ($previousRole+' failed before the lobby was ready.')}
                if([DateTime]::UtcNow -ge $deadline){throw 'Sequential capacity admission timed out.'}
                $ready=$previousState.lobbyReady -or $previousState.phase -in @('Loading','Warmup','Running')
                if(-not $ready){Start-Sleep -Milliseconds 250}
            } while(-not $ready)
            $extraPeer=Start-Peer ('client'+$slot) $hostState.room
        }
    }
    do {
        if($Scenario -eq 'late-join' -and $roles.Count -eq 2) {
            $firstHost=Read-Status $hostStatus
            $firstClient=Read-Status $clientStatus
            if($firstHost.initialVerified -and $firstClient.initialVerified) {
                $thirdPeer=Start-Peer 'client2' $hostState.room
            }
        }
        $states = @($roles | ForEach-Object { Read-Status (Join-Path $run ($_+'.json')) })
        if($Scenario -eq 'migration-crash' -and -not $crashInjected) {
            $crashState=Read-Status $hostStatus
            if($crashState.crashReady) {
                $hostPeer.Kill(); $hostPeer.WaitForExit(); $crashInjected=$true
            }
        }
        foreach($state in $states) {
            if($state.finished -and -not $state.success){throw ($state.role+' failed: '+$state.error)}
        }
        for($index=0;$index -lt $owned.Count;$index++) {
            if($crashInjected -and $roles[$index] -eq 'host'){continue}
            $state=Read-Status (Join-Path $run ($roles[$index]+'.json'))
            if($Scenario -eq 'capacity' -and $state.capacityHoldStartElapsed -gt 0 -and
                $state.stage -notin @('leaving','passed','failed') -and -not $owned[$index].HasExited){
                $sample=Get-CapacityMemorySample $owned[$index] $state.stableSeconds
                if($null -ne $sample){
                    $role=$roles[$index]
                    if(-not $capacityMemory.ContainsKey($role)){
                        $capacityMemory[$role]=[ordered]@{role=$role;source='Windows process';sampleCount=0;start=$sample;end=$null;observedHoldSeconds=0}
                    }
                    $record=$capacityMemory[$role]
                    $record.sampleCount++
                    $record.end=$sample
                    $record.observedHoldSeconds=$sample.holdSeconds-$record.start.holdSeconds
                }
            }
            if($owned[$index].HasExited -and -not $state.finished){throw ($roles[$index]+' exited without a final result.')}
        }
        if([DateTime]::UtcNow -ge $deadline){throw 'Network smoke scenario timed out.'}
        $requiredStates=@($states | Where-Object { -not($crashInjected -and $_.role -eq 'host') })
        $requiredCount=$roles.Count - [int]$crashInjected
        $complete = @($requiredStates | Where-Object finished).Count -eq $requiredCount
        if(-not $complete){Start-Sleep -Milliseconds 500}
    } while(-not $complete)
    foreach($peer in $owned){if(-not $peer.WaitForExit(15000)){throw 'Peer cleanup did not finish.'}}
    for($index=0;$index -lt $owned.Count;$index++) {
        if($crashInjected -and $roles[$index] -eq 'host'){continue}
        if($owned[$index].ExitCode -ne 0){throw 'A smoke player exited with failure.'}
    }
    $runtimeFailures = @(Get-ChildItem -LiteralPath $run -Filter '*.log' | Select-String -Pattern '^\s*(?:[\w.]*Exception:|Assertion failed|AssertionException:)')
    if($runtimeFailures.Count){throw ("Player logs contain $($runtimeFailures.Count) runtime exception/assertion records.")}
    if($Scenario -eq 'migration-crash' -and -not $crashInjected){throw 'The required host crash was not injected.'}
    if($Scenario -eq 'late-join' -and $roles.Count -ne 3){throw 'The required late client was not launched.'}
    if($Scenario -eq 'team-switch') {
        foreach($state in $requiredStates) {
            if(-not $state.teamSwitchComplete -or $state.teamSwitchStep -ne 4 -or
                $state.teamSwitchClearAck -ne 4 -or $state.teamSwitchReadyAck -ne 5 -or
                -not $state.teamSwitchGuestFingerprint -or $state.teamSwitchConnectionGeneration -le 0 -or $state.stableSeconds -lt 10) {
                throw 'Team switch smoke did not verify all four actual moves, readiness clearing, identity and gameplay.'
            }
        }
        $a=$requiredStates[0]; $b=$requiredStates[1]
        if($a.teamSwitchGuestFingerprint -ne $b.teamSwitchGuestFingerprint -or $a.teamSwitchGuestPlayer -ne $b.teamSwitchGuestPlayer -or
            $a.teamSwitchConnectionGeneration -ne $b.teamSwitchConnectionGeneration -or $a.teamSwitchInitialTeam -ne $b.teamSwitchInitialTeam) {
            throw 'Team switch peers disagree on the retained guest connection.'
        }
    }
    if($Scenario -in @('laser-combat','laser-moving-cover')) {
        $weapons=@('LaserPistol','LaserShotgun','LaserSniper')
        foreach($state in $requiredStates) {
            if(@($state.lasers).Count -ne 3){throw 'Laser smoke requires all three weapon verdicts.'}
            for($i=0;$i -lt 3;$i++) {
                $verdict=$state.lasers[$i]
                $other=if($state.role -eq 'host'){$requiredStates | Where-Object role -eq 'client'}else{$requiredStates | Where-Object role -eq 'host'}
                $expectedHits=if($i -eq 1){8}else{1}
                if($Scenario -eq 'laser-moving-cover' -and (-not $verdict.movingVerified -or -not $verdict.movingObserved -or $verdict.movingFireDistance -le .2 -or $verdict.movingFireSpeed -le .2 -or
                    -not $verdict.coverPlaced -or -not $verdict.coverReady -or -not $verdict.coverObserved -or
                    $verdict.coverShots -ne 1 -or $verdict.coverHits -ne 0 -or $verdict.coverHealth -ne $verdict.coverBaselineHealth -or
                    $verdict.coverAmmo -ne $verdict.ammo-1 -or -not $verdict.coverShotId -or
                    $verdict.coverShotId -ne $other.lasers[$i].coverShotId -or $verdict.coverSequence -ne $other.lasers[$i].coverSequence)) {
                    throw 'Moving laser smoke requires matching actual-motion and blocked-cover verdicts.'
                }

                if($verdict.weapon -ne $weapons[$i] -or -not $verdict.picked -or -not $verdict.equipped -or -not $verdict.verified -or
                    $verdict.shotCount -ne 1 -or $verdict.hitCount -ne $expectedHits -or -not $verdict.shotId -or -not $verdict.hitIds -or
                    $verdict.shotId -ne $other.lasers[$i].shotId -or $verdict.hitIds -ne $other.lasers[$i].hitIds){
                    throw 'Laser smoke is missing a complete matching pickup/equip/ammo/damage/event verdict.'
                }
            }
        }
    }
    if($Scenario -eq 'capacity'){
        if($requiredCount -ne 8){throw 'Capacity soak requires all eight smoke processes.'}
        foreach($state in $requiredStates){
            if($state.capacitySoakSeconds -ne $SoakSeconds -or $state.stableSeconds -lt $SoakSeconds -or
                $state.capacityVerifiedPlayers -ne 8 -or $state.capacityVerifiedAvatars -ne 8 -or
                $state.capacityVerifiedConnected -ne 8 -or $state.capacityVerifiedTeams -ne 4 -or
                @($state.capacityVerifiedTeamSizes).Count -ne 4 -or @($state.capacityVerifiedTeamSizes | Where-Object {$_ -ne 2}).Count){
                throw 'A capacity peer did not verify the requested eight-player soak duration and teams.'
            }
            $memory=$capacityMemory[$state.role]
            if($null -eq $memory -or $memory.sampleCount -lt 2 -or $memory.start.workingSetBytes -le 0 -or $memory.start.privateBytes -le 0 -or
                $memory.end.workingSetBytes -le 0 -or $memory.end.privateBytes -le 0 -or
                $memory.start.holdSeconds -gt 2 -or $memory.end.holdSeconds -lt $SoakSeconds-2 -or $memory.observedHoldSeconds -le 0){
                throw 'A capacity peer is missing positive Windows memory samples near the start and end of the hold.'
            }
        }
    }
    $result.passed = @($requiredStates | Where-Object success).Count -eq $requiredCount
}
catch { $result.error = $_.Exception.Message }
finally {
    foreach($peer in $owned){if(-not $peer.HasExited){$peer.Kill();$peer.WaitForExit()}}
    $result.completedUtc = [DateTime]::UtcNow.ToString('o')
    $result.host = Read-Status $hostStatus
    $result.client = Read-Status $clientStatus
    if($Scenario -in @('migration','migration-crash','late-join')){$result.client2 = Read-Status (Join-Path $run 'client2.json')}
    $result.hostCrashInjected=$crashInjected
    $result.processes=@(for($index=0;$index -lt $owned.Count;$index++) {
        [pscustomobject]@{role=$roles[$index];id=$owned[$index].Id;exitCode=$owned[$index].ExitCode;expectedCrash=($crashInjected -and $roles[$index] -eq 'host')}
    })
    if($Scenario -eq 'capacity'){
        $result.capacityMemory=@($roles | ForEach-Object {if($capacityMemory.ContainsKey($_)){[pscustomobject]$capacityMemory[$_]}})
        foreach($slot in 2..7){$result['client'+$slot]=Read-Status (Join-Path $run ('client'+$slot+'.json'))}
    }
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'result.json')
}
[pscustomobject]@{Passed=$result.passed; Scenario=$Scenario; Evidence=$run; Error=$result.error}
if(-not $result.passed){throw 'Network smoke failed; inspect the recorded result and player logs.'}
