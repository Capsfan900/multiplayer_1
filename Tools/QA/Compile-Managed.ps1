param(
    [ValidateSet('Runtime', 'Editor', 'Tests', 'All')][string]$Scope = 'All',
    [switch]$RunManagedTests,
    [switch]$ExportLayouts,
    [string]$UnityData = 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Data',
    [string]$PackageAssemblies
)

# Type checking only; this does not import assets, run the Editor, or prove physics/gameplay.
$ErrorActionPreference = 'Stop'
if ($RunManagedTests -and $Scope -notin @('Tests', 'All')) { throw 'RunManagedTests requires Tests or All scope.' }
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $PackageAssemblies) { $PackageAssemblies = Join-Path $project 'Library/ScriptAssemblies' }
# Installed engine DLLs expose disabled modules too; this assertion is not a Package Manager resolution check.
$manifest = Get-Content -LiteralPath (Join-Path $project 'Packages/manifest.json') -Raw | ConvertFrom-Json
if ($manifest.dependencies.'com.unity.modules.audio' -ne '1.0.0') {
    throw 'Wildstone requires com.unity.modules.audio 1.0.0 explicitly enabled in Packages/manifest.json.'
}
Write-Output 'Audio declaration checked. Installed DLL references do not verify Unity Package Manager resolution or fresh-import module availability.'
$dotnet = Join-Path $UnityData 'DotNetSdk/dotnet.exe'
$compiler = Join-Path $UnityData 'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'
$referenceRoot = Join-Path $UnityData 'UnityReferenceAssemblies/unity-4.8-api'
foreach ($required in @($dotnet, $compiler, $referenceRoot, $PackageAssemblies)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Missing compiler input: $required" }
}
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$output = Join-Path $project "Artifacts/ManagedCompile/$stamp"
New-Item -ItemType Directory -Path $output -Force | Out-Null
$references = @(Get-ChildItem -LiteralPath $referenceRoot -Filter '*.dll' -Recurse | ForEach-Object FullName)
$references += @(Get-ChildItem -LiteralPath (Join-Path $UnityData 'Managed/UnityEngine') -Filter 'UnityEngine*.dll' | ForEach-Object FullName)
$packages = @('Unity.InputSystem.dll', 'Unity.RenderPipelines.Core.Runtime.dll', 'Unity.RenderPipelines.Core.Runtime.Shared.dll',
    'Unity.RenderPipelines.Universal.Runtime.dll', 'Unity.RenderPipelines.Universal.Config.Runtime.dll')
foreach ($package in $packages) {
    $path = Join-Path $PackageAssemblies $package
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing cached package reference: $path" }
    $references += $path
}

function Compile-Assembly([string]$Name, [string[]]$Sources, [string[]]$ExtraReferences, [string]$Defines, [string]$Target = 'library') {
    if ($Sources.Count -eq 0) { throw "No source files for $Name" }
    $extension = if ($Target -eq 'exe') { 'exe' } else { 'dll' }
    $arguments = @('-nologo', "-target:$Target", '-nostdlib+', '-langversion:latest', '-warn:4',
        ('-out:"' + (Join-Path $output "$Name.$extension") + '"'))
    if ($Defines) { $arguments += "-define:$Defines" }
    $arguments += @(($references + $ExtraReferences | Select-Object -Unique) | ForEach-Object { '-r:"' + $_ + '"' })
    $arguments += @($Sources | ForEach-Object { '"' + $_ + '"' })
    $response = Join-Path $output "$Name.rsp"
    [IO.File]::WriteAllLines($response, $arguments)
    & $dotnet $compiler "@$response" 2>&1 | Tee-Object -FilePath (Join-Path $output "$Name.log")
    if ($LASTEXITCODE -ne 0) { throw "$Name type check failed: $LASTEXITCODE. See $output" }
    Write-Output "$Name type check passed ($($Sources.Count) files)."
}

$runtimeSources = @(Get-ChildItem -LiteralPath (Join-Path $project 'Assets/Wildstone/Runtime') -Filter '*.cs' -Recurse | ForEach-Object FullName)
Compile-Assembly 'Wildstone.Runtime' $runtimeSources @() ''
$runtimeDll = Join-Path $output 'Wildstone.Runtime.dll'
if ($ExportLayouts) {
    Compile-Assembly 'ExportLayouts' @((Join-Path $project 'Tools/Maps/ExportLayouts.cs')) @($runtimeDll) '' 'exe'
    $originalMonoPath = $env:MONO_PATH
    try {
        $env:MONO_PATH = $output + ';' + (Join-Path $UnityData 'Managed/UnityEngine')
        & (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') (Join-Path $output 'ExportLayouts.exe') (Join-Path $project 'ArtSource/Maps/layouts.json')
        if ($LASTEXITCODE -ne 0) { throw "Map data export failed: $LASTEXITCODE" }
    }
    finally { $env:MONO_PATH = $originalMonoPath }
}
$editorReferences = @(Get-ChildItem -LiteralPath (Join-Path $UnityData 'Managed/UnityEngine') -Filter 'UnityEditor*.dll' | ForEach-Object FullName)
if ($Scope -in @('Editor', 'Tests', 'All')) {
    $editorSources = @(Get-ChildItem -LiteralPath (Join-Path $project 'Assets/Wildstone/Editor') -Filter '*.cs' -Recurse | ForEach-Object FullName)
    Compile-Assembly 'Wildstone.Editor' $editorSources ($editorReferences + $runtimeDll) 'UNITY_EDITOR'
}
if ($Scope -in @('Tests', 'All')) {
    $testReferences = $editorReferences + $runtimeDll + @((Join-Path $output 'Wildstone.Editor.dll')) + @(
        (Join-Path $UnityData 'Resources/PackageManager/BuiltInPackages/com.unity.ext.nunit/net472/unity-custom/nunit.framework.dll'),
        (Join-Path $PackageAssemblies 'UnityEngine.TestRunner.dll'),
        (Join-Path $PackageAssemblies 'UnityEditor.TestRunner.dll'))
    $testSources = @(Get-ChildItem -LiteralPath (Join-Path $project 'Assets/Wildstone/Tests') -Filter '*.cs' -Recurse | ForEach-Object FullName)
    Compile-Assembly 'Wildstone.Tests' $testSources $testReferences 'UNITY_EDITOR,UNITY_INCLUDE_TESTS'
    if ($RunManagedTests) {
        Compile-Assembly 'ManagedNUnitRunner' @((Join-Path $PSScriptRoot 'ManagedNUnitRunner.cs')) $testReferences '' 'exe'
        $originalMonoPath = $env:MONO_PATH
        $originalErrorAction = $ErrorActionPreference
        try {
            $nunitDirectory = Join-Path $UnityData 'Resources/PackageManager/BuiltInPackages/com.unity.ext.nunit/net472/unity-custom'
            $env:MONO_PATH = $output + ';' + (Join-Path $UnityData 'Managed/UnityEngine') + ';' + $PackageAssemblies + ';' + $nunitDirectory
            $ErrorActionPreference = 'Continue'
            & (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') (Join-Path $output 'ManagedNUnitRunner.exe') `
                (Join-Path $output 'Wildstone.Tests.dll') (Join-Path $output 'Managed-results.xml') 2>&1 |
                Tee-Object -FilePath (Join-Path $output 'Managed-run.log')
            $ErrorActionPreference = $originalErrorAction
            if ($LASTEXITCODE -ne 0) { throw "Managed tests failed: $LASTEXITCODE. See $output" }
        }
        finally { $env:MONO_PATH = $originalMonoPath; $ErrorActionPreference = $originalErrorAction }
    }
}
Write-Output "Compiler evidence: $output (not Unity import or runtime verification)."
