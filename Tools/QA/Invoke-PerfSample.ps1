param([string]$Executable)
# Same-PC frame-time sample (FusionVisualCheckDriver --wildstone-perf-check) at 1920x1080 offscreen. Unlike the visual checks
# it opens a visible window for about 40 s and uses the normal render loop, so frame time includes real presentation.
$ErrorActionPreference='Stop'
$project=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if(-not $Executable){$Executable=(Get-Content (Join-Path $project 'Artifacts/Smoke/latest-build.json') -Raw|ConvertFrom-Json).executable}
$Executable=(Resolve-Path -LiteralPath $Executable).Path
$allowed=[IO.Path]::GetFullPath((Join-Path $project 'Artifacts/Unity'))+[IO.Path]::DirectorySeparatorChar
if(-not $Executable.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path $Executable -Leaf) -ne 'WildstoneArena.exe'){
    throw 'Use a diagnostic Wildstone player built under this workspace Artifacts/Unity directory.'
}
$run=Join-Path $project ('Artifacts/Visual/Perf-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $run -Force|Out-Null
$log=Join-Path $run 'player.log'
$arguments=@('-force-d3d11','-screen-fullscreen','0','-screen-width','1920','-screen-height','1080',
    '-logFile',('"'+$log+'"'),'--wildstone-visual-output',('"'+$run+'"'),'--wildstone-perf-check')
$player=Start-Process -FilePath $Executable -ArgumentList $arguments -WorkingDirectory (Split-Path $Executable) -PassThru
try {
    if(-not $player.WaitForExit(180000)){throw 'Perf sample player timed out.'}
    if($player.ExitCode -ne 0){throw "Perf sample player exited $($player.ExitCode); see $log."}
    $status=Get-Content -LiteralPath (Join-Path $run 'result.json') -Raw|ConvertFrom-Json
    if(-not $status.passed){throw "Perf sample failed: $($status.error)"}
    $failures=@(Select-String -LiteralPath $log -Pattern '^\s*(?:[\w.]*Exception:|Assertion failed|AssertionException:)')
    if($failures.Count){throw "Perf player logged $($failures.Count) runtime exceptions/assertions."}
}
finally { if(-not $player.HasExited){$player.Kill();$player.WaitForExit()} }
Get-Content -LiteralPath (Join-Path $run 'perf.json') -Raw
