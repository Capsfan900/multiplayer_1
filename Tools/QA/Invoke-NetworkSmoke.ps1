param(
    [ValidateSet('basic','reconnect','migration','migration-crash','combat','laser-combat','laser-moving-cover','projectile-combat','grenade-combat','special-projectiles','deflection','gunplay','capacity','late-join','team-switch','rematch')][string]$Scenario = 'basic',
    [ValidateSet('baseline','regional80','adverse150')][string]$NetworkProfile = 'baseline',
    [string]$Executable,
    [ValidateRange(30,2400)][int]$TimeoutSeconds = 150,
    [ValidateRange(10,1800)][int]$SoakSeconds = 10
)

$ErrorActionPreference = 'Stop'
if($NetworkProfile -ne 'baseline' -and $Scenario -notin @('laser-moving-cover','projectile-combat','grenade-combat','special-projectiles','deflection','gunplay')) {
    throw 'Injected network profiles are supported only for the moving-laser, projectile combat and gunplay scenarios.'
}
# Players allow 180 s under injected latency; keep the wrapper's hang guard above that.
$effectiveTimeoutSeconds = if($Scenario -eq 'capacity'){[Math]::Max($TimeoutSeconds,$SoakSeconds+150)}elseif($NetworkProfile -ne 'baseline'){[Math]::Max($TimeoutSeconds,210)}else{$TimeoutSeconds}
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
$run = Join-Path $smokeRoot ((Get-Date -Format 'yyyyMMdd-HHmmss-fff')+'-'+$Scenario+'-'+$NetworkProfile)
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
        '--wildstone-smoke-network-profile',$NetworkProfile,
        '--wildstone-smoke-output',('"'+$statusPath+'"'),'--wildstone-smoke-peer-output',('"'+$peerPath+'"'))
    if($Scenario -eq 'capacity'){$arguments += @('--wildstone-smoke-soak-seconds',$SoakSeconds.ToString([Globalization.CultureInfo]::InvariantCulture))}
    if($Room){$arguments += @('--wildstone-smoke-room',$Room)}
    $peer = Start-Process -FilePath $Executable -ArgumentList $arguments -WorkingDirectory (Split-Path $Executable) -PassThru -WindowStyle Hidden
    $owned.Add($peer)
    $roles.Add($Role)
    return $peer
}
$result = [ordered]@{scenario=$Scenario; networkProfile=$NetworkProfile; networkEvidence='Configured in-process injection plus sampled SDK remote RTT; not external-network or performance acceptance.'; capacitySoakSeconds=$(if($Scenario -eq 'capacity'){$SoakSeconds}else{$null}); timeoutSeconds=$effectiveTimeoutSeconds; executable=$Executable; startedUtc=[DateTime]::UtcNow.ToString('o'); passed=$false; error=$null; run=$run}
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
    foreach($state in $requiredStates) {
        $profile=$state.networkProfile
        if($null -eq $profile -or $profile.profile -ne $NetworkProfile -or $profile.appliedCount -lt 1) {
            throw 'A peer did not apply and report the requested process-local network profile.'
        }
        $expectedRtt=0; $expectedJitter=0; $expectedLoss=0.0
        if($NetworkProfile -eq 'regional80'){$expectedRtt=80; $expectedJitter=10; $expectedLoss=.01}
        if($NetworkProfile -eq 'adverse150'){$expectedRtt=150; $expectedJitter=20; $expectedLoss=.02}
        if($profile.configuredAddedRttMs -ne $expectedRtt -or $profile.configuredOneWayJitterMs -ne $expectedJitter -or
            $profile.configuredLossProbabilityPerDirection -ne $expectedLoss -or $profile.injected -ne ($NetworkProfile -ne 'baseline') -or
            [Math]::Abs($profile.delayMinSeconds-($expectedRtt-2*$expectedJitter)/1000.0) -gt .000001 -or
            [Math]::Abs($profile.delayMaxSeconds-($expectedRtt+2*$expectedJitter)/1000.0) -gt .000001) {
            throw 'A peer reported different injection values from the requested profile.'
        }
        if($NetworkProfile -ne 'baseline' -and ($profile.sdkBuildType -ne 'Debug' -or $state.sdkRemoteRttSamples -lt 1)) {
            throw 'Injected network smoke requires Fusion Debug DLLs and positive SDK remote RTT samples on each peer.'
        }
    }
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
    if($Scenario -eq 'rematch') {
        # Two rounds in one room: Results (host Rematch, client wait), lobby return on the same seats, a fresh second match.
        $hostState=$requiredStates | Where-Object role -eq 'host'; $clientState=$requiredStates | Where-Object role -eq 'client'
        foreach($state in $requiredStates) {
            $r=$state.rematch
            if($null -eq $r -or $r.playedRounds -ne 2 -or $r.finishedRounds -ne 2 -or $r.resultsVerified -ne 2 -or -not $r.lobbyReturned -or -not $r.readyCleared -or
                -not $r.freshVerified -or -not $r.newHud -or $r.freshPlayers -ne 2 -or $r.freshProjectiles -ne 0 -or $r.freshScoreTicks -ne 0 -or
                $r.freshMinHealth -ne 100 -or $r.freshPickupCount -lt 1 -or $r.freshPickups -ne $r.freshPickupCount -or
                $r.firstMatchId -le 0 -or $r.secondMatchId -le 0 -or $r.firstMatchId -eq $r.secondMatchId -or
                @($r.winners).Count -ne 2 -or @($r.winners | Where-Object {$_ -ne $hostState.rematch.team}).Count) {
                throw 'Rematch smoke did not verify both rounds, the lobby return and a fresh second match.'
            }
        }
        $h=$hostState.rematch; $c=$clientState.rematch
        if(-not $h.rematchRequested -or $c.rematchRequested -or -not $c.clientRematchRefused -or $h.secondRoundPublishedTick -le $h.publishedTickAtRematch -or
            $h.room -ne $c.room -or $h.matchId -ne $c.matchId -or $h.firstMatchId -ne $c.firstMatchId -or $h.secondMatchId -ne $c.secondMatchId -or
            $h.team -eq $c.team -or $h.participant -eq $c.participant) {
            throw 'Rematch peers disagree on the room, matches, retained seats or host-only trigger.'
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
    if($Scenario -eq 'gunplay') {
        # Revision 4: sniper no-scope vs scoped spread, pistol body vs head damage, SMG held fire; both peers agree.
        $weapons=@('LaserSniper','LaserPistol','LaserSMG')
        foreach($state in $requiredStates) {
            $other=if($state.role -eq 'host'){$requiredStates | Where-Object role -eq 'client'}else{$requiredStates | Where-Object role -eq 'host'}
            if(@($state.gunplay).Count -ne 3){throw 'Gunplay smoke requires all three weapon verdicts.'}
            for($i=0;$i -lt 3;$i++) {
                $v=$state.gunplay[$i]; $r=$other.gunplay[$i]
                if($v.weapon -ne $weapons[$i] -or -not $v.picked -or -not $v.equipped -or -not $v.verified){throw "Gunplay smoke is missing a complete $($weapons[$i]) verdict."}
                if($i -lt 2) {
                    for($s=0;$s -lt 2;$s++) {
                        if(-not $v.stepObserved[$s] -or -not $v.shotIds[$s] -or $v.shotIds[$s] -ne $r.shotIds[$s] -or $v.hitIds[$s] -ne $r.hitIds[$s] -or
                            $v.headshots[$s] -ne $r.headshots[$s] -or $v.damages[$s] -ne $r.damages[$s]){throw "Peers disagree on $($weapons[$i]) step $s."}
                    }
                }
            }
            $sniper=$state.gunplay[0]; $pistol=$state.gunplay[1]; $smg=$state.gunplay[2]; $otherSmg=$other.gunplay[2]
            if(-not ($sniper.spreads[0] -gt $sniper.spreads[1])){throw 'Sniper no-scope spread must exceed the scoped spread.'}
            if($pistol.headshots[0] -or -not $pistol.headshots[1] -or -not ($pistol.damages[1] -gt $pistol.damages[0])){throw 'Pistol headshot must be flagged and deal more than the body shot.'}
            # The client drives the held trigger, so only its status records completion and the shot count.
            if(($state.role -eq 'client' -and (-not $smg.autoDone -or $smg.autoShots -lt 3)) -or -not $smg.autoObserved -or
                $smg.autoShotIds -ne $otherSmg.autoShotIds -or $smg.autoHitIds -ne $otherSmg.autoHitIds){throw 'SMG held fire is missing or peers disagree.'}
        }
    }
    if($Scenario -eq 'projectile-combat') {
        foreach($state in $requiredStates) {
            $p=$state.projectile
            $other=if($state.role -eq 'host'){$requiredStates | Where-Object role -eq 'client'}else{$requiredStates | Where-Object role -eq 'host'}
            if(-not $p.picked -or -not $p.equipped -or @($p.shots).Count -ne 2){throw 'Projectile smoke is missing pickup/equip or both cases.'}
            for($i=0;$i -lt 2;$i++) {
                $v=$p.shots[$i]; $r=$other.projectile.shots[$i]
                if(-not $v.verified -or -not $v.observed -or -not $v.despawned -or $v.samples -lt 2 -or
                    $v.lastTick -le $v.firstTick -or $v.lastTravel-$v.firstTravel -le .2 -or
                    $v.shotCount -ne 1 -or $v.explosions -ne 1 -or -not $v.identity -or -not $v.shotId -or -not $v.explosionId -or
                    $v.ammo -ne (9-$i) -or $v.identity -ne $r.identity -or $v.shotId -ne $r.shotId -or
                    $v.explosionId -ne $r.explosionId -or $v.hitId -ne $r.hitId -or [Math]::Abs($v.damage-$r.damage) -gt .01) {
                    throw 'Projectile smoke requires matching sampled travel, terminal event, despawn and single-spend verdicts.'
                }
                if($i -eq 0 -and ($v.hits -ne 1 -or -not $v.hitId -or $v.expectedDamage -le 20 -or [Math]::Abs($v.damage-$v.expectedDamage) -gt .01 -or [Math]::Abs($v.eventDamage-$v.expectedDamage) -gt .01)) {
                    throw 'Projectile positive control is missing matching falloff damage.'
                }
                if($i -eq 1 -and (-not $v.coverBlocks -or $v.blastDistance -ge 3.5 -or $v.expectedDamage -le 20 -or $v.damage -ne 0 -or $v.hits -ne 0)) {
                    throw 'Projectile cover case did not demonstrate a blocked target strictly inside blast range.'
                }
            }
        }
    }
    if($Scenario -eq 'grenade-combat') {
        foreach($state in $requiredStates) {
            $other=$requiredStates | Where-Object role -ne $state.role
            $g=$state.grenade
            if(-not $g.picked -or @($g.throws).Count -ne 2){throw 'Grenade smoke requires pickup and both throws.'}
            for($i=0;$i -lt 2;$i++) {
                $v=$g.throws[$i]; $r=$other.grenade.throws[$i]
                if(-not $v.verified -or -not $v.observed -or -not $v.despawned -or -not $v.gravityObserved -or -not $v.bounceObserved -or
                    $v.samples -lt 2 -or $v.lastTick -le $v.firstTick -or $v.lastTravel-$v.firstTravel -le 1 -or
                    $v.expireTick -le $v.actionTick -or $v.explosionTick -ne $v.expireTick -or $v.expireTick -ne $r.expireTick -or
                    $v.ammo -ne (1-$i) -or $v.shotCount -ne 1 -or $v.explosions -ne 1 -or
                    -not $v.identity -or -not $v.shotId -or -not $v.explosionId -or $v.identity -ne $r.identity -or
                    $v.shotId -ne $r.shotId -or $v.explosionId -ne $r.explosionId -or $v.hitId -ne $r.hitId -or
                    [Math]::Abs($v.damage-$r.damage) -gt .01 -or $v.blastDistance -ge 5 -or $v.expectedDamage -le 15) {
                    throw 'Grenade smoke is missing matching flight, bounce, fuse, ammo or in-range blast evidence.'
                }
                if($i -eq 0 -and ($v.hits -ne 1 -or -not $v.hitId -or [Math]::Abs($v.damage-$v.expectedDamage) -gt .01 -or [Math]::Abs($v.eventDamage-$v.expectedDamage) -gt .01)) {
                    throw 'Grenade positive control is missing falloff damage.'
                }
                if($i -eq 1 -and (-not $v.coverBlocks -or $v.hits -ne 0 -or $v.damage -ne 0)){throw 'Grenade cover did not protect its in-range target.'}
            }
        }
    }
    if($Scenario -eq 'special-projectiles') {
        foreach($state in $requiredStates) {
            $other=$requiredStates | Where-Object role -ne $state.role
            if(@($state.specialProjectiles).Count -ne 2){throw 'Both Disc and Stun verdicts are required.'}
            for($i=0;$i -lt 2;$i++) {
                $v=$state.specialProjectiles[$i]; $r=$other.specialProjectiles[$i]
                $expectedWeapon=if($i -eq 0){'ReboundDisc'}else{'StunGun'}
                $expectedShots=if($i -eq 0){1}else{3}; $ammo=if($i -eq 0){10}else{8}; $damage=if($i -eq 0){35}else{15}
                if($v.weapon -ne $expectedWeapon -or -not $v.picked -or -not $v.equipped -or -not $v.verified -or @($v.shots).Count -ne $expectedShots){throw 'Special projectile pickup/equip/verdict incomplete.'}
                for($j=0;$j -lt $expectedShots;$j++) {
                    $a=$v.shots[$j]; $b=$r.shots[$j]
                    if(-not $a.verified -or -not $a.observed -or -not $a.absent -or $a.samples -lt 2 -or
                        $a.lastTick -le $a.firstTick -or $a.lastTravel-$a.firstTravel -le .2 -or $a.shots -ne 1 -or $a.hits -ne 1 -or
                        $a.ammo -ne ($ammo-$j-1) -or [Math]::Abs($a.eventDamage-$damage) -gt .01 -or [Math]::Abs($a.damage-$damage*($j+1)) -gt .01 -or
                        -not $a.identity -or -not $a.events -or $a.identity -ne $b.identity -or $a.events -ne $b.events -or
                        $a.stunUntil -ne $b.stunUntil -or $a.immunityUntil -ne $b.immunityUntil){throw 'Special projectile peers lack matching flight, single spend/hit and event verdicts.'}
                    if($i -eq 0 -and ($a.ricochets -ne 2 -or $a.ricochetMask -ne 6 -or $a.maxBounces -ne 2 -or
                        $a.firstDespawnTick -le $a.lastTick -or $a.firstDespawnTick -ge $a.expireTick)){throw 'Disc must ricochet twice and disappear before lifetime expiry.'}
                    if($i -eq 1) {
                        if(-not $a.stunExpired){throw 'Stun suppression did not expire.'}
                        if($j -eq 1) {
                            if($a.stuns -ne 0 -or $a.stunUntil -ne $v.shots[0].stunUntil -or $a.immunityUntil -ne $v.shots[0].immunityUntil){throw 'Immune hit refreshed stun timers.'}
                        } else {
                            $hostShot=if($state.role -eq 'host'){$a}else{$b}
                            if($a.stuns -ne 1 -or -not $hostShot.suppressionObserved){throw 'Authoritative active stun was not observed.'}
                        }
                    }
                }
            }
        }
    }
    if($Scenario -eq 'deflection') {
        # D037: round 0 the host pushes, round 1 the client; both peers must report the same single reflection.
        $hostState=$requiredStates | Where-Object role -eq 'host'; $clientState=$requiredStates | Where-Object role -eq 'client'
        foreach($state in $requiredStates) {
            $other=$requiredStates | Where-Object role -ne $state.role
            if(@($state.deflection).Count -ne 2){throw 'Deflection smoke requires both pusher rounds.'}
            for($i=0;$i -lt 2;$i++) {
                $v=$state.deflection[$i]; $r=$other.deflection[$i]
                $pusher=if($i -eq 0){$hostState.localParticipant}else{$clientState.localParticipant}
                if(-not $v.verified -or -not $v.observed -or -not $v.picked -or $v.pusher -ne $pusher -or $v.shooter -eq $pusher -or
                    $v.samples -lt 1 -or $v.deflectedSamples -lt 1 -or -not $v.deflectedFlag -or $v.ownerTeam -ne $v.pusherTeam -or $v.ownerTeam -eq $v.shooterTeam -or
                    $v.velocityDot -ge 0 -or $v.deflectedLaunchTick -ne $v.launchTick -or $v.remaining -ne 0 -or
                    $v.fireShots -ne 1 -or $v.pushShots -ne 1 -or $v.deflections -ne 1 -or $v.explosions -ne 1 -or $v.shooterExplosions -ne 0 -or
                    $v.hits -ne 1 -or $v.pusherHits -ne 0 -or $v.pusherHealth -ne 100 -or $v.shooterAmmo -ne 9 -or $v.pusherAmmo -ne 5 -or
                    $v.expectedDamage -le 20 -or [Math]::Abs($v.damage-$v.expectedDamage) -gt .01 -or [Math]::Abs($v.eventDamage-$v.expectedDamage) -gt .01 -or
                    -not $v.deflectedId -or -not $v.explosionId -or -not $v.hitId -or $v.deflectedId -ne $r.deflectedId -or $v.explosionId -ne $r.explosionId -or
                    $v.hitId -ne $r.hitId -or $v.fireShotId -ne $r.fireShotId -or $v.pushShotId -ne $r.pushShotId -or [Math]::Abs($v.damage-$r.damage) -gt .01) {
                    throw "Deflection round $i lacks a matching single reflection credited to the pusher."
                }
            }
        }
        if(@(Get-ChildItem -LiteralPath $run -Filter '*.log' | Select-String -SimpleMatch 'Invalid replicated match state').Count){throw 'A player log reports invalid replicated match state.'}
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
    if($Scenario -in @('laser-combat','laser-moving-cover','gunplay') -and $result.host) {
        # Host lag-compensation diagnosis, not a gate: queried vs SDK-requested view age per hitscan query (cap 150 ms).
        $weapons=@($(if($Scenario -eq 'gunplay'){$result.host.gunplay}else{$result.host.lasers}) | Where-Object {$_.rewind})
        $queries=[int]($weapons | ForEach-Object {$_.rewind.queries} | Measure-Object -Sum).Sum
        $result.rewind=[ordered]@{queries=$queries;clamped=[int]($weapons | ForEach-Object {$_.rewind.clamped} | Measure-Object -Sum).Sum
            rewindAgeMeanMs=$(if($queries){($weapons | ForEach-Object {$_.rewind.meanMs*$_.rewind.queries} | Measure-Object -Sum).Sum/$queries})
            rewindAgeMaxMs=($weapons | ForEach-Object {$_.rewind.maxMs} | Measure-Object -Maximum).Maximum
            requestedMeanMs=$(if($queries){($weapons | ForEach-Object {$_.rewind.requestedMeanMs*$_.rewind.queries} | Measure-Object -Sum).Sum/$queries})
            requestedMaxMs=($weapons | ForEach-Object {$_.rewind.requestedMaxMs} | Measure-Object -Maximum).Maximum
            weapons=@($weapons | ForEach-Object {[pscustomobject]@{weapon=$_.weapon;rewind=$_.rewind}})}
    }
    if($Scenario -eq 'deflection' -and $result.host -and $result.client) {
        # Timing margin per round from the pusher's own record; full per-peer verdicts stay under host/client.
        $result.deflection=@(0,1 | ForEach-Object { $p=if($_ -eq 0){$result.host}else{$result.client}; $v=@($p.deflection)[$_]
            if($v){[pscustomobject]@{round=$_;pusherRole=$p.role;pushLeadTicks=$v.pushLeadTicks;pushObservedDistance=$v.pushObservedDistance
                pushPredictedDistance=$v.pushPredictedDistance;resolveDistance=$v.resolveDistance;velocityDot=$v.velocityDot
                damage=$v.damage;expectedDamage=$v.expectedDamage;deflectedId=$v.deflectedId;verified=$v.verified}} })
    }
    if($Scenario -in @('migration','migration-crash','late-join')){$result.client2 = Read-Status (Join-Path $run 'client2.json')}
    $result.hostCrashInjected=$crashInjected
    $result.processes=@(for($index=0;$index -lt $owned.Count;$index++) {
        [pscustomobject]@{role=$roles[$index];id=$owned[$index].Id;exitCode=$owned[$index].ExitCode;expectedCrash=($crashInjected -and $roles[$index] -eq 'host')}
    })
    if($Scenario -eq 'capacity'){
        $result.capacityMemory=@($roles | ForEach-Object {if($capacityMemory.ContainsKey($_)){
            $memory=$capacityMemory[$_]
            # Endpoint growth only; it does not by itself distinguish warm-up from an unbounded leak.
            $memory.workingSetGrowthBytes=$memory.end.workingSetBytes-$memory.start.workingSetBytes
            $memory.privateGrowthBytes=$memory.end.privateBytes-$memory.start.privateBytes
            [pscustomobject]$memory}})
        foreach($slot in 2..7){$result['client'+$slot]=Read-Status (Join-Path $run ('client'+$slot+'.json'))}
        $result.capacityTraffic=@($roles | ForEach-Object {$state=$result[$_]; if($state){[pscustomobject]@{role=$_;statsAvailable=$state.capacityStatsAvailable
            statsForwardTicks=$state.capacityStatsForwardTicks;sdkTicks=$state.capacityRunningSdkTicks+$state.capacityFinishedSdkTicks
            runningSeconds=$state.capacityRunningSeconds;runningInBytesPerSecond=$state.capacityRunningInBytesPerSecond;runningOutBytesPerSecond=$state.capacityRunningOutBytesPerSecond
            finishedSeconds=$state.capacityFinishedSeconds;finishedInBytesPerSecond=$state.capacityFinishedInBytesPerSecond;finishedOutBytesPerSecond=$state.capacityFinishedOutBytesPerSecond}}})
    }
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'result.json')
}
[pscustomobject]@{Passed=$result.passed; Scenario=$Scenario; Evidence=$run; Error=$result.error}
if(-not $result.passed){throw 'Network smoke failed; inspect the recorded result and player logs.'}
