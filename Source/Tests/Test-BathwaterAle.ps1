param([string]$GameRoot = 'E:\steam\steamapps\common\RimWorld')
$ErrorActionPreference = 'Stop'
$modRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$workspaceRoot = Split-Path -Parent $modRoot
# 仅夹具隔离游戏的对象池依赖，不写入生产程序集。
$poolContext = [Runtime.Loader.AssemblyLoadContext]::new('BathwaterFixtureObjectPool', $false)
$gamePool = $poolContext.LoadFromAssemblyPath((Join-Path $GameRoot 'Microsoft.Extensions.ObjectPool.dll'))
[Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving([Func[Runtime.Loader.AssemblyLoadContext, Reflection.AssemblyName, Reflection.Assembly]]{
    param($context, $name)
    if ($name.Name -eq 'Microsoft.Extensions.ObjectPool' -and $name.Version.Major -eq 11) { return $gamePool }
    return $null
})
foreach ($file in @('Brutal.Vulkan.dll','Waylith.UnityCompat.dll','Waylith.dll','UnityStubs.dll','Assembly-CSharp.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $GameRoot $file))
}
$harmony = [Reflection.Assembly]::LoadFrom('E:\steam\steamapps\workshop\content\294100\2009463077\1.7\Assemblies\0Harmony.dll')
$hostAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $workspaceRoot 'Nivarian Race/1.7/Core/Assemblies/Nivarian_Race.rimunity'))
$core = [Reflection.Assembly]::LoadFrom((Join-Path $modRoot '1.7/Assemblies/NivarianBathwaterAle.dll'))
$refs = @($core.Location, $harmony.Location, (Join-Path $GameRoot 'Assembly-CSharp.dll'), (Join-Path $GameRoot 'UnityStubs.dll'))
$refs += @(Get-ChildItem (Join-Path $PSHOME 'ref') -Filter '*.dll' | ForEach-Object FullName)
Add-Type -Path (Join-Path $PSScriptRoot 'BathwaterAleTests.cs') -ReferencedAssemblies $refs -CompilerOptions '/nowarn:1701'
[BathwaterAleTests]::Run($hostAssembly, (Join-Path $modRoot 'Source/obj/settings-test.xml'))
