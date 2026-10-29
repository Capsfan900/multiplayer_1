param([string]$Executable,[switch]$Motion,[switch]$Weapons,[switch]$Sequence)
$ErrorActionPreference='Stop'
if(([int][bool]$Motion+[int][bool]$Weapons+[int][bool]$Sequence) -gt 1){throw 'Choose one diagnostic capture mode.'}
$project=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if(-not $Executable){$Executable=(Get-Content (Join-Path $project 'Artifacts/Smoke/latest-build.json') -Raw|ConvertFrom-Json).executable}
$Executable=(Resolve-Path -LiteralPath $Executable).Path
$allowed=[IO.Path]::GetFullPath((Join-Path $project 'Artifacts/Unity'))+[IO.Path]::DirectorySeparatorChar
if(-not $Executable.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path $Executable -Leaf) -ne 'WildstoneArena.exe'){
    throw 'Use a diagnostic Wildstone player built under this workspace Artifacts/Unity directory.'
}
$run=Join-Path $project ('Artifacts/Visual/'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $run -Force|Out-Null
$log=Join-Path $run 'player.log'
$arguments=@('-batchmode','-force-d3d11','-screen-fullscreen','0','-screen-width','1280','-screen-height','720',
    '-logFile',('"'+$log+'"'),'--wildstone-visual-output',('"'+$run+'"'))
$expectedCaptures=if($Sequence){32}elseif($Weapons){26}elseif($Motion){13}else{16}
if($Sequence){$arguments+='--wildstone-sequence-check'}
if($Motion){$arguments+='--wildstone-motion-check'}
if($Weapons){$arguments+='--wildstone-weapon-check'}
$record=[ordered]@{mode=$(if($Sequence){'continuous-presentation-samples-not-gameplay-or-framerate-proof'}elseif($Weapons){'weapon-effect-fixtures-not-gameplay-proof'}elseif($Motion){'visual-pose-fixtures-not-gameplay-proof'}else{'static-screen-captures'});expectedCaptures=$expectedCaptures;executable=$Executable;arguments=$arguments;startedUtc=[DateTime]::UtcNow.ToString('o');passed=$false;error=$null}
$player=$null
try {
    $player=Start-Process -FilePath $Executable -ArgumentList $arguments -WorkingDirectory (Split-Path $Executable) -PassThru -WindowStyle Hidden
    if(-not $player.WaitForExit(210000)){throw 'Owned visual-check player timed out.'}
    if($player.ExitCode -ne 0){throw "Visual-check player exited $($player.ExitCode)."}
    $status=Get-Content -LiteralPath (Join-Path $run 'result.json') -Raw|ConvertFrom-Json
    if(-not $status.passed -or $status.captures.Count -ne $expectedCaptures){throw "The player did not complete all $expectedCaptures captures."}
    foreach($capture in $status.captures){
        $file=Get-Item -LiteralPath $capture.file
        if(-not $file.FullName.StartsWith($run+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or $file.Length -lt 100){throw 'A capture is missing or outside the run directory.'}
        $stream=[IO.File]::OpenRead($file.FullName)
        try { $header=New-Object byte[] 24; if($stream.Read($header,0,24) -ne 24){throw 'Incomplete capture header.'} }
        finally { $stream.Dispose() }
        if([BitConverter]::ToString($header,0,8) -ne '89-50-4E-47-0D-0A-1A-0A'){throw 'Capture is not PNG.'}
        $pngWidth=[int]$header[16]*16777216+[int]$header[17]*65536+[int]$header[18]*256+[int]$header[19]
        $pngHeight=[int]$header[20]*16777216+[int]$header[21]*65536+[int]$header[22]*256+[int]$header[23]
        if($pngWidth -ne $capture.width -or $pngHeight -ne $capture.height -or
            [Math]::Abs($capture.cameraAspect-$pngWidth/[double]$pngHeight) -gt .0001){throw 'Capture dimensions/projection differ from the offscreen target.'}
    }
    if($Sequence){
        $samples=@{}; $previousFrame=-1
        foreach($capture in $status.captures){
            if([int]$capture.simulatedFrame -le $previousFrame){throw 'Sequence samples are duplicated or out of order.'}
            $previousFrame=[int]$capture.simulatedFrame
            foreach($value in @($capture.cameraFov,$capture.cameraPosition.x,$capture.cameraPosition.y,$capture.cameraPosition.z,
                $capture.handsPosition.x,$capture.handsPosition.y,$capture.handsPosition.z,
                $capture.handsRotation.x,$capture.handsRotation.y,$capture.handsRotation.z,$capture.handsRotation.w)){
                if([double]::IsNaN([double]$value) -or [double]::IsInfinity([double]$value)){throw 'Non-finite presentation transform.'}
            }
            $samples[$previousFrame]=$capture
        }
        foreach($frame in @(0,12,24,30,31,32,42,54,60,61,62,72,84,90,91,92,96,102,108,114,120,121,122,132,144,156,168,180,192,198,204,216)){
            if(-not $samples.ContainsKey($frame)){throw "Missing sequence sample $frame."}
            if([Math]::Abs($samples[$frame].simulationDelta-(1.0/60)) -gt .000001){throw 'Unexpected simulated delta.'}
        }
        function HandDistance($a,$b){
            [Math]::Sqrt([Math]::Pow($a.handsPosition.x-$b.handsPosition.x,2)+[Math]::Pow($a.handsPosition.y-$b.handsPosition.y,2)+[Math]::Pow($a.handsPosition.z-$b.handsPosition.z,2))
        }
        $record.sequenceObservations=[ordered]@{
            simulationDelta=$samples[216].simulationDelta; simulatedSeconds=$samples[216].simulatedSeconds
            captureWallSeconds=$samples[216].wallSeconds-$samples[0].wallSeconds
            slideToReturnDistance=(HandDistance $samples[54] $samples[84])
            recoilDistance=(HandDistance $samples[91] $samples[84])
            recoilReturnDistance=(HandDistance $samples[114] $samples[84])
            reloadDistance=(HandDistance $samples[156] $samples[216])
            reloadReturnDistance=(HandDistance $samples[216] $samples[114])
        }
        $observed=$record.sequenceObservations
        if(-not $samples[54].sliding -or $samples[84].sliding -or
            -not $samples[156].reloading -or $samples[216].reloading -or
            $observed.slideToReturnDistance -lt .01 -or $observed.recoilDistance -lt .005 -or
            $observed.recoilReturnDistance -gt .01 -or $observed.reloadDistance -lt .01 -or
            $observed.reloadReturnDistance -gt .01){throw 'Continuous presentation transition/return gate failed; inspect samples and images.'}
    }
    $failures=@(Select-String -LiteralPath $log -Pattern '^\s*(?:[\w.]*Exception:|Assertion failed|AssertionException:)')
    if($failures.Count){throw "Visual player logged $($failures.Count) runtime exceptions/assertions."}
    $record.passed=$true
}
catch {$record.error=$_.Exception.Message}
finally {
    if($player -and -not $player.HasExited){$player.Kill();$player.WaitForExit()}
    $record.completedUtc=[DateTime]::UtcNow.ToString('o')
    $record|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $run 'run.json')
}
[pscustomobject]@{Passed=$record.passed;Evidence=$run;Error=$record.error}
if(-not $record.passed){throw 'Visual capture failed; inspect the run record and player log.'}
