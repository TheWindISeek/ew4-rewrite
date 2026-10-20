param([string]$Root = (Split-Path $PSScriptRoot -Parent), [int]$OldProcess = 35192,
    [string]$Runtime = 'rewrite/local/mod-delivery-runtime')
$ErrorActionPreference = 'Stop'
$endpoint = 'http://127.0.0.1:5184'
$profile = Join-Path $Root 'rewrite/reports/frontend_restoration_20261019/test-profile'
$runtime = if ([IO.Path]::IsPathRooted($Runtime)) { $Runtime } else { Join-Path $Root $Runtime }
$evidence = Join-Path $Root 'rewrite/reports/mod-import-20261019/preview-preservation.json'
function Request([string]$Route, [object]$Body, [switch]$AllowNoSession) {
    $query = [Uri]::EscapeDataString(($Body | ConvertTo-Json -Depth 10 -Compress))
    $reply = Invoke-RestMethod "$endpoint/api/$($Route)?q=$query"
    if (-not $reply.ok) {
        if ($AllowNoSession -and $reply.code -eq 'client.no_session') { return $null }
        throw "Preview $Route failed: $($reply.message)"
    }
    return $reply.data
}
$before = Request 'action' @{op='state'} -AllowNoSession
$activeBefore = (Invoke-RestMethod "$endpoint/api/mods").active
# Persist the currently running scene, never a historical snapshot.
if ($null -ne $before) {
    $mode = if ($before.scenario.StartsWith('tutorial')) { 4 } elseif ($before.scenario.StartsWith('conquest')) { 2 } else { 1 }
    $null = Request 'save' @{mode=$mode;slot=0}
}
$savedHashes = @{}
Get-ChildItem -LiteralPath $profile -File -Recurse | Where-Object {$_.Name -notlike '*.bak'} | ForEach-Object { $savedHashes[$_.FullName] = (Get-FileHash -LiteralPath $_.FullName).Hash }
$process = Get-CimInstance Win32_Process -Filter "ProcessId=$OldProcess"
if ($process.CommandLine -notlike '*--urls http://127.0.0.1:5184*' -or $process.CommandLine -notlike '*Ew4.Web.dll*') { throw 'Unexpected preview process; refusing to stop' }
Stop-Process -Id $OldProcess
$new = Start-Process -FilePath 'C:/Users/WIN11/.codex/tools/dotnet-android/dotnet.exe' -ArgumentList "$runtime/Ew4.Web.dll --urls $endpoint --root $Root --save-root $profile --contentRoot $Root/rewrite/Ew4.Web --Logging:LogLevel:Default Warning" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$runtime/live-out.log" -RedirectStandardError "$runtime/live-error.log"
$ready=$false
for ($attempt=0; $attempt -lt 30; $attempt++) {
    try { $null=Invoke-RestMethod "$endpoint/api/mods"; $ready=$true; break } catch { Start-Sleep -Milliseconds 300 }
}
if (-not $ready) { throw 'New preview did not start' }
$after = if ($null -ne $before) { Request 'load' @{mode=$mode;slot=0} } else { Request 'action' @{op='state'} -AllowNoSession }
$checks = @{}
$checks['activeContent'] = (Invoke-RestMethod "$endpoint/api/mods").active -eq $activeBefore
if (-not $checks['activeContent']) { throw 'Active content changed during restart' }
if ($null -eq $before) {
    $checks['emptySession'] = $null -eq $after
    if (-not $checks['emptySession']) { throw 'Restart unexpectedly opened a game' }
}
else { foreach($key in 'scenario','revision','round','phase','playerFaction','units','cells','factions','dialogue') {
    $left=ConvertTo-Json -InputObject $before.$key -Depth 100 -Compress
    $right=ConvertTo-Json -InputObject $after.$key -Depth 100 -Compress
    $checks[$key]=$left -eq $right
    if (-not $checks[$key]) { throw "Current scene changed during restart: $key" }
} }
foreach($path in $savedHashes.Keys) {
    if ($null -ne $before -and $path.EndsWith('account.json')) { continue }
    if ((Get-FileHash -LiteralPath $path).Hash -ne $savedHashes[$path]) { throw "Save changed during reload: $path" }
}
[ordered]@{process=$new.Id;scenario=$after.scenario;revision=$after.revision;units=$after.units.Count;checks=$checks;saveFilesChecked=$savedHashes.Count;source=$(if($null -eq $before){'main menu without session; no game loaded or save written'}else{'immediately current running scene, saved before restart'})} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $evidence -Encoding utf8NoBOM
Get-Content -LiteralPath $evidence
