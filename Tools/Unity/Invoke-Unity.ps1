param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Compile', 'Prepare', 'PreparePresentation', 'PrepareNetworking', 'Bake', 'Test', 'BuildWindows', 'BuildSmoke')]
    [string]$Action,
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe',
    [ValidateSet('EditMode', 'PlayMode')]
    [string]$TestPlatform = 'EditMode',
    [ValidatePattern('^[A-Za-z0-9_.;]+$')]
    [string]$TestFilter,
    [switch]$Graphics,
    [ValidateRange(1, 120)]
    [int]$TimeoutMinutes = 20
)

$ErrorActionPreference = 'Stop'
$expectedRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path.TrimEnd('\')
$actualRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd('\')
if (-not [string]::Equals($actualRoot, $expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "ProjectRoot must be this project: $expectedRoot"
}
if (-not [System.IO.Path]::IsPathRooted($UnityExe) -or -not (Test-Path -LiteralPath $UnityExe -PathType Leaf)) {
    throw "UnityExe must be an existing absolute executable path"
}

$artifacts = Join-Path $expectedRoot 'Artifacts/Unity'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$log = Join-Path $artifacts "$Action-$stamp.log"
$buildExe = Join-Path $artifacts 'Windows/WildstoneArena.exe'
$arguments = @('-batchmode', '-projectPath', "`"$expectedRoot`"", '-logFile', "`"$log`"")
if ($Action -ne 'Bake' -and -not $Graphics) { $arguments += '-nographics' }
if ($Graphics) { $arguments += '-force-d3d11' }
switch ($Action) {
    'Compile' { $arguments += @('-quit', '-executeMethod', 'Wildstone.Editor.ProjectSetup.Configure') }
    'Prepare' { $arguments += @('-quit', '-executeMethod', 'Wildstone.Editor.ContentBuild.Prepare') }
    'PreparePresentation' { $arguments += @('-quit', '-executeMethod', 'Wildstone.Editor.PresentationBuilder.Build') }
    'PrepareNetworking' { $arguments += @('-quit', '-executeMethod', 'Wildstone.Networking.Editor.FusionContentBuild.Prepare') }
    'Bake' { $arguments += @('-quit', '-executeMethod', 'Wildstone.Editor.ArenaLighting.BakeAll') }
    'Test' {
        $results = Join-Path $artifacts "$TestPlatform-$stamp.xml"
        $arguments += @('-runTests', '-testPlatform', $TestPlatform, '-testResults', "`"$results`"")
        if ($TestFilter) { $arguments += @('-testFilter', "`"$TestFilter`"") }
    }
    'BuildWindows' { $arguments += @('-quit', '-executeMethod', 'Wildstone.Editor.ProjectSetup.BuildWindows') }
    'BuildSmoke' { $arguments += @('-quit', '-executeMethod', 'Wildstone.Networking.Editor.FusionSmokeBuild.Build') }
}

$startedUtc = (Get-Date).ToUniversalTime()
$process = Start-Process -FilePath $UnityExe -ArgumentList $arguments -WorkingDirectory $expectedRoot -PassThru -WindowStyle Hidden
if (-not $process.WaitForExit($TimeoutMinutes * 60000)) {
    # Stop only the Editor this invocation owns; leave the user's Hub and other projects alone.
    $process.Kill()
    $process.WaitForExit()
    throw "Unity $Action timed out after $TimeoutMinutes minutes. See $log"
}
if ($process.ExitCode -ne 0) { throw "Unity $Action exited $($process.ExitCode). See $log" }
if (-not (Test-Path -LiteralPath $log)) { throw "Unity wrote no log: $log" }
$content = Get-Content -LiteralPath $log -Raw
if ($content -match 'error CS\d+|Compilation failed|Scripts have compiler errors') { throw "Unity $Action reported compilation errors. See $log" }
if ($Action -eq 'Compile' -and $content -notmatch 'WILDSTONE CONFIGURED') { throw "Unity setup did not complete. See $log" }
if ($Action -eq 'Prepare' -and $content -notmatch 'WILDSTONE CONTENT READY') { throw "Unity content preparation did not complete. See $log" }
if ($Action -eq 'PrepareNetworking' -and $content -notmatch 'WILDSTONE NETWORK CONTENT READY') { throw "Unity network preparation did not complete. See $log" }
if ($Action -eq 'BuildSmoke' -and $content -notmatch 'WILDSTONE SMOKE BUILD OK:') { throw "Unity smoke build did not complete. See $log" }
if ($Action -eq 'Bake' -and $content -notmatch 'WILDSTONE LIGHTING BAKED: 3') { throw "Unity lighting bake did not complete. See $log" }
if ($Action -eq 'BuildWindows') {
    if ($content -notmatch [regex]::Escape("WILDSTONE BUILD OK: $buildExe")) { throw "Unity build did not complete. See $log" }
    $builtFile = Get-Item -LiteralPath $buildExe -ErrorAction SilentlyContinue
    if ($null -eq $builtFile -or $builtFile.Length -eq 0 -or $builtFile.LastWriteTimeUtc -lt $startedUtc) {
        throw "Unity did not produce a fresh Windows executable: $buildExe"
    }
}
if ($Action -eq 'Test') {
    if (-not (Test-Path -LiteralPath $results)) { throw "Unity wrote no test results: $results" }
    [xml]$testXml = Get-Content -LiteralPath $results -Raw
    $run = $testXml.SelectSingleNode('/test-run')
    if ($null -eq $run -or [int]$run.GetAttribute('total') -lt 1 -or
        [int]$run.GetAttribute('failed') -gt 0 -or $run.GetAttribute('result') -ne 'Passed') {
        throw "Unity tests did not pass. See $results and $log"
    }
}
Write-Output "Unity $Action OK. Log: $log"
