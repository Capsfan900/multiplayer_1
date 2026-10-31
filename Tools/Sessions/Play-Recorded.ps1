param(
    # Defaults to the BuildWindows output. Any WildstoneArena.exe built from a commit that has Assets/Wildstone/DevTools works.
    [string]$Executable,
    # Trust check, e.g. 'shots=10,jumps=5,slides=3,marks=2' (names from the report's Counts line).
    [string]$Expect,
    # Also record the screen (off by default; game data is always recorded). Needs ffmpeg on PATH.
    [switch]$Video
)
# Development add-on: launch the player with the session recorder on, wait for you to quit, then write report.md.
# Recordings stay local under Artifacts/Sessions (ignored by git). See docs/SESSION-RECORDER.md.
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $Executable) { $Executable = Join-Path $project 'Artifacts/Unity/Windows/WildstoneArena.exe' }
if (-not (Test-Path -LiteralPath $Executable)) { throw "No player at $Executable. Build one: ./Tools/Unity/Invoke-Unity.ps1 -Action BuildWindows" }
$run = Join-Path $project ('Artifacts/Sessions/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
$log = Join-Path $run 'player.log'
Write-Output "Recording to $run"
Write-Output 'Play normally. Press F8 whenever something feels wrong (saves a mark + screenshot). Quit the game to finish.'

# Screen video: GPU desktop duplication + NVENC at 30 fps, so it costs almost no CPU or game frame time. MKV survives
# being cut off. video.json holds the wall-clock start the analyzer uses to line frames up with the data (~0.5 s).
$video = $null
$ffmpeg = (Get-Command ffmpeg -ErrorAction SilentlyContinue).Source
if ($Video -and -not $ffmpeg) { throw 'ffmpeg not found on PATH; install it (winget install Gyan.FFmpeg) or run without -Video.' }
if ($Video) {
    $info = New-Object System.Diagnostics.ProcessStartInfo $ffmpeg
    $info.Arguments = '-hide_banner -loglevel error -y -f lavfi -i ddagrab=output_idx=0:framerate=30 -c:v h264_nvenc -preset p4 -cq 30 "' + (Join-Path $run 'session.mkv') + '"'
    $info.UseShellExecute = $false; $info.RedirectStandardInput = $true; $info.CreateNoWindow = $true
    $video = [System.Diagnostics.Process]::Start($info)
    @{ startedUtc = (Get-Date).ToUniversalTime().ToString('o') } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'video.json')
    Write-Output 'Screen recording on (main monitor).'
}

try {
    $player = Start-Process -FilePath $Executable -WorkingDirectory (Split-Path $Executable) -PassThru -ArgumentList @(
        '-logFile', ('"' + $log + '"'), '--wildstone-record', '--wildstone-record-dir', ('"' + $run + '"'))
    $player.WaitForExit()
}
finally {
    if ($video -and -not $video.HasExited) {
        $video.StandardInput.Write('q'); $video.StandardInput.Close()   # ffmpeg's clean stop
        if (-not $video.WaitForExit(15000)) { $video.Kill() }
    }
}
if (-not (Test-Path (Join-Path $run 'session.jsonl'))) { throw "No session.jsonl was written; this build may not include the recorder. See $log" }

$python = (Get-Command python -ErrorAction SilentlyContinue).Source
if (-not $python) { throw "Python not found; recording kept at $run" }
$arguments = @('-I', (Join-Path $PSScriptRoot 'analyze_session.py'), $run)
if ($Expect) { $arguments += @('--expect', $Expect) }
& $python @arguments
$checkFailed = $LASTEXITCODE -ne 0

# Exceptions in the player log are usually the real bugs; append them to the report.
$errors = @(Select-String -LiteralPath $log -Pattern '^\s*(?:[\w.]*Exception\b|Assertion failed|NullReference)' -ErrorAction SilentlyContinue)
$summary = "`n## Player log`n`n$($errors.Count) exception/assertion lines in player.log.`n"
foreach ($e in ($errors | Select-Object -First 10)) { $summary += "- line $($e.LineNumber): $($e.Line.Trim())`n" }
Add-Content -LiteralPath (Join-Path $run 'report.md') -Value $summary
Write-Output $summary
Write-Output "Report: $(Join-Path $run 'report.md')"
if ($checkFailed) { exit 1 }
