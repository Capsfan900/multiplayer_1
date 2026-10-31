param([string]$UnityData = 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Data')

# Type-checks the Wildstone.Fusion runtime sources (production and WILDSTONE_SMOKE) outside Unity, then runs the
# rewind-age/clamp, rewind aggregation and soak-bound rules against the compiled smoke assembly under Mono.
# No SDK weaving, Unity import, player launch or network service; native Unity builds/tests remain authoritative.
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$output = Join-Path $project ('Artifacts/ManagedCompile/network-evidence-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$dotnet = Join-Path $UnityData 'DotNetSdk/dotnet.exe'
$compiler = Join-Path $UnityData 'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'
$engine = Join-Path $UnityData 'Managed/UnityEngine'
$fusion = Join-Path $project 'Assets/Photon/Fusion/Assemblies'
$cache = Join-Path $project 'Library/ScriptAssemblies'
$references = @(Get-ChildItem -LiteralPath (Join-Path $UnityData 'UnityReferenceAssemblies/unity-4.8-api') -Filter '*.dll' -Recurse | ForEach-Object FullName)
$references += @(Get-ChildItem -LiteralPath $engine -Filter '*.dll' | Where-Object { $_.Name -like 'UnityEngine*' -or $_.Name -eq 'Unity.Scripting.dll' } | ForEach-Object FullName)
$references += @('Fusion.Runtime','Fusion.Common','Fusion.Sockets','Fusion.Log','Fusion.Realtime' | ForEach-Object { Join-Path $fusion "$_.dll" })
$references += @((Join-Path $project 'Assets/Photon/PhotonLibs/netstandard2.0/debug/PhotonClient.dll'))
$references += @('Fusion.Unity','Photon.Realtime','Unity.InputSystem','Unity.RenderPipelines.Core.Runtime','Unity.RenderPipelines.Core.Runtime.Shared',
    'Unity.RenderPipelines.Universal.Runtime','Unity.RenderPipelines.Universal.Config.Runtime' | ForEach-Object { Join-Path $cache "$_.dll" })
foreach ($required in @($dotnet, $compiler) + $references) { if (-not (Test-Path -LiteralPath $required)) { throw "Missing compiler input: $required" } }
function Compile([string]$Name, [string[]]$Sources, [string[]]$Extra, [string]$Defines, [string]$Target = 'library') {
    $file = Join-Path $output ($Name + $(if($Target -eq 'exe'){'.exe'}else{'.dll'}))
    $arguments = @('-nologo', "-target:$Target", '-nostdlib+', '-langversion:latest', '-unsafe+', '-warn:4', '-nowarn:CS0618', ('-out:"'+$file+'"'))
    if ($Defines) { $arguments += "-define:$Defines" }
    $arguments += @(($references + $Extra | Select-Object -Unique) | ForEach-Object { '-r:"'+$_+'"' })
    $arguments += @($Sources | ForEach-Object { '"'+$_+'"' })
    $response = Join-Path $output "$Name.rsp"; [IO.File]::WriteAllLines($response, $arguments)
    & $dotnet $compiler "@$response" 2>&1 | Tee-Object -FilePath (Join-Path $output "$Name.log") | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "$Name type check failed. See $output" }
    return $file
}
$runtime = Compile 'Wildstone.Runtime' @(Get-ChildItem -LiteralPath (Join-Path $project 'Assets/Wildstone/Runtime') -Filter '*.cs' -Recurse | ForEach-Object FullName) @() ''
$network = @(Get-ChildItem -LiteralPath (Join-Path $project 'Assets/Wildstone/Networking/Runtime') -Filter '*.cs' -Recurse | ForEach-Object FullName)
$null = Compile 'Wildstone.Fusion.Production' $network @($runtime) ''
$smoke = Compile 'Wildstone.Fusion' $network @($runtime) 'WILDSTONE_SMOKE'
Write-Output "Wildstone.Fusion production and WILDSTONE_SMOKE type checks passed ($($network.Count) files)."
$check = Join-Path $output 'NetworkEvidenceCheck.cs'
@'
using System;
using System.Reflection;
using Wildstone.Combat;
using Wildstone.Networking;
static class NetworkEvidenceCheck
{
    const BindingFlags Any=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static void Require(bool ok,string what) { if(!ok) throw new Exception(what); }
    static FusionHitscanRecord Shot(int sdkFrom,int sdkTo,float sdkAlpha)
    {
        // The same BoundView/ViewAge pair FusionCombatWorld.Hitscan uses, at 120 Hz with the host on tick 1000.
        var world=typeof(FusionCombatWorld); float dt=1f/120;
        var bound=new object[]{1000,dt,(int?)sdkFrom,(int?)sdkTo,(float?)sdkAlpha,0,0,0f};
        world.GetMethod("BoundView",Any).Invoke(null,bound);
        Func<int,int,float,float> age=(f,t,a)=>(float)world.GetMethod("ViewAge",Any).Invoke(null,new object[]{1000,dt,f,t,a});
        float alpha=(float)bound[7];
        return new FusionHitscanRecord(default(CombatAction),1000,(int)bound[5],(int)bound[6],alpha,age((int)bound[5],(int)bound[6],alpha),age(sdkFrom,sdkTo,alpha),false,0);
    }
    static int Main()
    {
        var over=Shot(970,971,.5f); var under=Shot(990,991,.5f);
        Require(over.Clamped && Math.Abs(over.RewindSeconds-.15f)<.0001f && Math.Abs(over.RequestedRewindSeconds-29.5f/120)<.0001f,"A 246 ms SDK view must be clamped to 150 ms.");
        Require(!under.Clamped && under.RewindSeconds==under.RequestedRewindSeconds && Math.Abs(under.RewindSeconds-9.5f/120)<.0001f,"A 79 ms SDK view must be queried unchanged.");
        var metrics=Activator.CreateInstance(typeof(FusionSmokeDriver).GetNestedType("RewindMetrics",Any));
        foreach(var shot in new[]{over,under}) metrics.GetType().GetMethod("Add").Invoke(metrics,new object[]{shot});
        Func<string,object> field=name=>metrics.GetType().GetField(name).GetValue(metrics);
        Require((int)field("queries")==2 && (int)field("clamped")==1 && Math.Abs((float)field("maxMs")-150)<.01f &&
            Math.Abs((float)field("meanMs")-(150+9.5f/.12f)/2)<.01f && Math.Abs((float)field("requestedMaxMs")-29.5f/.12f)<.01f,"Rewind aggregation mismatch.");
        var parse=typeof(FusionSmokeDriver).GetMethod("ParseCapacitySoakSeconds",Any);
        Require((int)parse.Invoke(null,new object[]{null})==10 && (int)parse.Invoke(null,new object[]{"1800"})==1800,"Valid soak durations changed.");
        foreach(var bad in new[]{"9","1801"," 300"})
            try { parse.Invoke(null,new object[]{bad}); throw new Exception("Invalid soak duration accepted: "+bad); }
            catch(TargetInvocationException failure) { Require(failure.InnerException is ArgumentException,"Unexpected soak parser failure."); }
        Console.WriteLine("Network evidence rules: rewind clamp/unclamped, aggregation and 10-1800 s soak bounds passed.");
        return 0;
    }
}
'@ | Set-Content -LiteralPath $check
$exe = Compile 'NetworkEvidenceCheck' @($check) @($runtime, $smoke) '' 'exe'
$originalMonoPath = $env:MONO_PATH
try {
    $env:MONO_PATH = (@($output, $engine, $cache, $fusion, (Join-Path $project 'Assets/Photon/PhotonLibs/netstandard2.0/debug')) -join ';')
    & (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $exe
    if ($LASTEXITCODE -ne 0) { throw "Network evidence rules failed: $LASTEXITCODE. See $output" }
}
finally { $env:MONO_PATH = $originalMonoPath }
Write-Output "Compiler evidence: $output (not SDK weaving, Unity import or runtime verification)."
