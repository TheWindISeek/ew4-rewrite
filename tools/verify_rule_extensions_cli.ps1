param([string]$Root = (Split-Path $PSScriptRoot -Parent), [string]$Tool, [string]$Base)
$ErrorActionPreference = 'Stop'
if (-not $Tool) { $Tool = Join-Path $Root 'build/ew4-mod-kit/Ew4.ModTool.exe' }
if (-not $Base) { $Base = Join-Path $Root 'build/ew4-mod-kit/data' }
$run = Join-Path $Root ('rewrite/reports/extensions-20261020/cli-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
$overlay = Join-Path $run 'overlay'
New-Item -ItemType Directory -Path $overlay -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $Root 'rewrite/mod-kit/examples/rule-extension/rewrite') -Destination $overlay -Recurse
$checks = 0
function Command([string[]]$Arguments) {
    $reply = & $tool @Arguments "--base=$base" "--mods=$run/mods" "--saves=$run/saves"
    if ($LASTEXITCODE -ne 0) { throw "CLI failed: $($Arguments[0])" }
    return ($reply | ConvertFrom-Json)
}
function Check([bool]$Value, [string]$Label) {
    if (-not $Value) { throw "Extension CLI: $Label" }
    $script:checks++
}
$manifest = Command @('pack', $overlay, '--id=rule-example', '--name=rule-example', '--version=1.0')
Check ($manifest.files.PSObject.Properties.Name -contains 'rewrite/extension.json') 'example is a valid packaged overlay'
$verified = Command @('verify', $overlay)
Check ($verified.id -eq 'rule-example') 'example validates against real builtin scenarios'
$installed = Command @('import', $overlay)
Check ($installed.files -gt 5) 'example installs'
$package = Join-Path $run 'example.ew4mod'
$null = Command @('export', $installed.key, $package)
Check (Test-Path -LiteralPath $package) 'example exports offline package'
$reimported = Command @('import', $package)
Check ($reimported.key -eq $installed.key) 'exported example imports without changes'
$source = Join-Path $base 'original/conquest1.btl'
$rules = Join-Path $run 'victory.json'
$null = Command @('victory-export', $source, $rules)
$definition = Get-Content -LiteralPath $rules -Raw | ConvertFrom-Json
Check ($definition.win.type -eq 'enemy_factions_defeated') 'victory export produces editable default template'
$outputDirectory = Join-Path $run 'changed'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$changed = Join-Path $outputDirectory 'conquest1.btl'
$null = Command @('victory-apply', $source, $rules, $changed)
Check ((Get-Item -LiteralPath $changed).Length -gt (Get-Item -LiteralPath $source).Length) 'victory extension appended'
$roundtrip = Join-Path $run 'roundtrip.json'
$null = Command @('victory-export', $changed, $roundtrip)
Check ((Get-Content -LiteralPath $rules -Raw) -eq (Get-Content -LiteralPath $roundtrip -Raw)) 'victory rules export roundtrip'
$restored = Join-Path $run 'restored.btl'
$null = Command @('victory-strip', $changed, $restored)
Check ((Get-FileHash -LiteralPath $restored).Hash -eq (Get-FileHash -LiteralPath $source).Hash) 'strip restores exact original bytes'
$summary = @{checks=$checks;errors=0;run=$run;package=$package}
$summary | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Root 'rewrite/reports/extensions-20261020/cli.json') -Encoding utf8NoBOM
$summary | ConvertTo-Json -Compress
