param([string]$Executable,[switch]$Motion,[switch]$Weapons)
$ErrorActionPreference='Stop'
if($Motion -and $Weapons){throw 'Choose one diagnostic capture mode.'}
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
$expectedCaptures=if($Weapons){20}elseif($Motion){13}else{16}
if($Motion){$arguments+='--wildstone-motion-check'}
if($Weapons){$arguments+='--wildstone-weapon-check'}
$record=[ordered]@{mode=$(if($Weapons){'weapon-effect-fixtures-not-gameplay-proof'}elseif($Motion){'visual-pose-fixtures-not-gameplay-proof'}else{'static-screen-captures'});expectedCaptures=$expectedCaptures;executable=$Executable;arguments=$arguments;startedUtc=[DateTime]::UtcNow.ToString('o');passed=$false;error=$null}
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
