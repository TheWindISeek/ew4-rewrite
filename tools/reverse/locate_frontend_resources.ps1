param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,
    [string]$Output = 'reverse/reports/frontend-resource-location-20261021'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$source = Join-Path $Root 'apk_work/original/assets'
$client = Join-Path $Root 'rewrite/data/client'
$outputPath = Join-Path $Root $Output
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$manifest = [IO.File]::ReadAllText((Join-Path $client 'manifest.json')) | ConvertFrom-Json
$layout = [xml][IO.File]::ReadAllText((Join-Path $source 'layout-568h.xml'))
$specifications = @(
    'image_flag_hd|fra1.png', 'image_flag_hd|usa1.png',
    'image_ui_hd|save_grayboard.png', 'image_ui_hd|button_ok_noshadow.png',
    'image_ui_hd|star_board.png', 'image_ui_hd|star_middle.png',
    'image_ui_hd|diffcult_1.png', 'image_ui_hd|medals.png',
    'image_ui_hd|board_button.png', 'image_ui_hd|marker_money.png',
    'image_ui_hd|marker_industry.png', 'image_ui_hd|marker_food.png',
    'image_recruit_hd3|button_ok.png', 'image_recruit_hd3|button_training.png',
    'image_recruit_hd|button_buildupgrade.png', 'image_recruit_hd|button_buyship.png',
    'buildings_hd|morale_down1.png', 'buildings_hd|morale_down2.png',
    'buildings_hd|morale_down3.png', 'buildings_hd|morale_up.png',
    'image_ui_hd|box_skilldetail.png', 'image_ui_hd|box_skilldetail_arrow.png',
    'image_ui_hd|board_resources.png', 'image_ui_hd|common_line_hor.png'
)
$atlases = @{}
$textures = @{}
$records = [Collections.Generic.List[object]]::new()
$bitmap = [Drawing.Bitmap]::new(1120, 6 * 156)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$font = [Drawing.Font]::new('Arial', 10)
$background = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(228, 232, 235))
try {
    $graphics.Clear([Drawing.Color]::White)
    for ($index = 0; $index -lt $specifications.Count; $index++) {
        $atlas, $name = $specifications[$index].Split('|')
        if (-not $atlases.ContainsKey($atlas)) {
            # Legacy atlas files contain Texture and Images as sibling roots.
            $atlases[$atlas] = [xml]('<Atlas>' + [IO.File]::ReadAllText((Join-Path $source ($atlas + '.xml'))) + '</Atlas>')
            $textures[$atlas] = [Drawing.Image]::FromFile((Join-Path $source ($atlas + '.png')))
        }
        $node = @($atlases[$atlas].SelectNodes('//Image') | Where-Object name -eq $name)
        if ($node.Count -ne 1) { throw "Expected one sprite: $atlas/$name" }
        $node = $node[0]
        $rect = [Drawing.Rectangle]::new([int]$node.x, [int]$node.y, [int]$node.w, [int]$node.h)
        $texture = $textures[$atlas]
        if ($rect.Left -lt 0 -or $rect.Top -lt 0 -or $rect.Right -gt $texture.Width -or $rect.Bottom -gt $texture.Height) {
            throw "Sprite outside texture: $atlas/$name"
        }
        $runtime = @($manifest.atlases.PSObject.Properties | Where-Object { $null -ne $_.Value.PSObject.Properties[$name] } | ForEach-Object {
            [ordered]@{atlas = $_.Name; definition = $_.Value.PSObject.Properties[$name].Value}
        })
        $derived = 'ui/' + $atlas + '/' + $name
        $records.Add([ordered]@{
            atlas = $atlas; sprite = $name; source_texture = 'apk_work/original/assets/' + $atlas + '.png'
            source_definition = 'apk_work/original/assets/' + $atlas + '.xml'
            rect = [ordered]@{x=$rect.X; y=$rect.Y; width=$rect.Width; height=$rect.Height; refx=[int]$node.refx; refy=[int]$node.refy}
            derived = $derived; derived_exists = (Test-Path -LiteralPath (Join-Path $client $derived))
            runtime_matches = $runtime
        })
        $x = ($index % 4) * 280
        $y = [int][Math]::Floor($index / 4) * 156
        $graphics.FillRectangle($background, $x + 3, $y + 3, 274, 150)
        $graphics.DrawString($atlas, $font, [Drawing.Brushes]::Black, $x + 10, $y + 8)
        $graphics.DrawString($name, $font, [Drawing.Brushes]::Black, $x + 10, $y + 25)
        $scale = [Math]::Min(2.0, [Math]::Min(250.0 / $rect.Width, 100.0 / $rect.Height))
        $destination = [Drawing.RectangleF]::new($x + 140 - $rect.Width * $scale / 2, $y + 49, $rect.Width * $scale, $rect.Height * $scale)
        $graphics.DrawImage($texture, $destination, [Drawing.RectangleF]$rect, [Drawing.GraphicsUnit]::Pixel)
    }
    $bitmap.Save((Join-Path $outputPath 'resource-contact-sheet.png'), [Drawing.Imaging.ImageFormat]::Png)
    $layouts = @(foreach ($id in @('form_save','form_victory','form_victorytext','form_game','form_roundturn','form_floattext','form_messagebox')) {
        $node = $layout.SelectSingleNode("//Layout[@id='$id']")
        [ordered]@{id=$id; source_exists=($null -ne $node); imported=($null -ne $manifest.layouts.PSObject.Properties[$id]); xml=$node.OuterXml}
    })
    $sources = @($atlases.Keys | Sort-Object | ForEach-Object {
        $atlas = $_
        foreach ($extension in @('.xml','.png')) {
            $file = Join-Path $source ($atlas + $extension)
            [ordered]@{file='apk_work/original/assets/' + $atlas + $extension; sha256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash}
        }
    })
    $report = [ordered]@{date='2026-10-21'; scope='Source resource location only; no runtime changes or device validation'; sprites=$records.ToArray(); layouts=$layouts; sources=$sources}
    [IO.File]::WriteAllText((Join-Path $outputPath 'resource-index.json'), ($report | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
    [pscustomobject]@{Sprites=$records.Count;Layouts=$layouts.Count;RuntimeMissing=@($records | Where-Object {$_.runtime_matches.Count -eq 0}).Count;LayoutErrors=@($layouts | Where-Object {-not $_.source_exists -or -not $_.imported}).Count} | ConvertTo-Json -Compress
}
finally {
    foreach ($texture in $textures.Values) { $texture.Dispose() }
    $font.Dispose()
    $background.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}
