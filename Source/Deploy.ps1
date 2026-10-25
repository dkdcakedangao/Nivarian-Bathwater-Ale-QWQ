param([string]$ModsRoot = 'E:\steam\steamapps\common\RimWorld\Mods')
$ErrorActionPreference = 'Stop'
$modRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$resolvedModsRoot = (Resolve-Path -LiteralPath $ModsRoot).Path
$destination = [IO.Path]::GetFullPath((Join-Path $resolvedModsRoot 'Nivarian-Bathwater-Ale-QWQ'))
if ((Split-Path -Parent $destination) -ne $resolvedModsRoot -or $destination -eq $modRoot) {
    throw 'Deployment must target this single mod directory under Mods.'
}
foreach ($version in @('1.6', '1.7')) {
    if (-not (Test-Path -LiteralPath (Join-Path $modRoot "$version/Assemblies/NivarianBathwaterAle.dll"))) {
        throw "Build RimWorld $version before deploying."
    }
}
# 不镜像删除目标文件；开发目录和本地日志永不复制。
& robocopy $modRoot $destination /E /XD Source '.git*' /XF '.git*' log.txt /R:1 /W:1 /NFL /NDL /NJH /NJS /NP
if ($LASTEXITCODE -ge 8) { throw "Deployment failed: robocopy exit $LASTEXITCODE" }
"Deployed: $destination"
exit 0
