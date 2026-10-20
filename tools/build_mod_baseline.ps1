param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$source = Join-Path $Root 'apk_work/original/assets'
$records = [ordered]@{}
Get-ChildItem -LiteralPath $source -Recurse -File | Sort-Object FullName | ForEach-Object {
    $name = [IO.Path]::GetRelativePath($source, $_.FullName).Replace('\', '/')
    $records[$name] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
$records | ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $Root 'rewrite/data/client/import-baseline.json') -Encoding utf8NoBOM
Write-Output "Original resource fingerprints: $($records.Count)"
