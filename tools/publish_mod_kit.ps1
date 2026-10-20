param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$dotnet = 'C:/Users/WIN11/.codex/tools/dotnet-android/dotnet.exe'
$destination = Join-Path $Root 'build/ew4-mod-kit'
& $dotnet publish (Join-Path $Root 'rewrite/Ew4.ModTool/Ew4.ModTool.csproj') -c Release -r win-x64 --self-contained true -o $destination --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Tool publish failed' }
foreach ($part in 'original','client') {
    $target = Join-Path $destination "data/$part"
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Copy-Item -Path (Join-Path $Root "rewrite/data/$part/*") -Destination $target -Recurse -Force
}
$legacy = Join-Path $destination 'data/legacy'
New-Item -ItemType Directory -Path $legacy -Force | Out-Null
foreach ($name in 'catalog.json','runtime_rule_tables.json','metagame_rule_tables.json') {
    Copy-Item -LiteralPath (Join-Path $Root "rewrite/data/legacy/$name") -Destination $legacy -Force
}
$frontend = Join-Path $destination 'data/frontend'
New-Item -ItemType Directory -Path $frontend -Force | Out-Null
foreach ($name in 'conquest-pages.json','city-names.json') {
    Copy-Item -LiteralPath (Join-Path $Root "rewrite/Ew4.Web/wwwroot/$name") -Destination $frontend -Force
}
# Runtime font credits and other data must not overwrite the detailed distribution guides.
Copy-Item -Path (Join-Path $Root 'rewrite/mod-kit/*') -Destination $destination -Recurse -Force
$all = Get-ChildItem -LiteralPath $destination -File -Recurse
[PSCustomObject]@{ Files = $all.Count; Bytes = ($all | Measure-Object Length -Sum).Sum; Path = $destination } | ConvertTo-Json -Compress
