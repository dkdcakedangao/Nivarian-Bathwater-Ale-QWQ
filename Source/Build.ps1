param(
    [ValidateSet('1.6', '1.7', 'All')]
    [string]$Version = 'All',
    [string]$RimWorldDir = 'E:\steam\steamapps\common\RimWorld',
    [string]$RimWorld16ManagedDir = (Join-Path $RimWorldDir 'RimWorldWin64_Data/Managed'),
    [string]$HarmonyRoot = 'E:\steam\steamapps\workshop\content\294100\2009463077',
    [string]$DotNet = 'dotnet'
)

$ErrorActionPreference = 'Stop'
if ($Version -in @('1.6', 'All')) {
    & $DotNet build (Join-Path $PSScriptRoot 'NivarianBathwaterAle.csproj') -c Release "-p:RimWorldManagedDir=$RimWorld16ManagedDir" "-p:HarmonyAssemblyPath=$HarmonyRoot/1.6/Assemblies/0Harmony.dll"
    if ($LASTEXITCODE -ne 0) { throw 'RimWorld 1.6 build failed.' }
}
if ($Version -in @('1.7', 'All')) {
    & $DotNet build (Join-Path $PSScriptRoot '1.7/NivarianBathwaterAle.csproj') -c Release "-p:RimWorldDir=$RimWorldDir" "-p:HarmonyAssemblyPath=$HarmonyRoot/1.7/Assemblies/0Harmony.dll"
    if ($LASTEXITCODE -ne 0) { throw 'RimWorld 1.7 build failed.' }
}
