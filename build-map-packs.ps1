# Builds sidecar map zip packs for DetailedMaps and optionally copies them to TLD Mods.
param(
    [string]$MapsDir = (Join-Path $PSScriptRoot "Maps"),
    [string]$OutDir = $PSScriptRoot,
    [string]$ModsPath = "I:\SteamLibrary\steamapps\common\TheLongDark\Mods",
    [switch]$Deploy
)

$ErrorActionPreference = "Stop"

function New-FlatZip([string]$ZipPath, [System.IO.FileInfo[]]$Files) {
    if (Test-Path $ZipPath) {
        Remove-Item $ZipPath -Force
    }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $zip = [System.IO.Compression.ZipFile]::Open($ZipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in $Files) {
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip,
                $file.FullName,
                $file.Name,
                [System.IO.Compression.CompressionLevel]::NoCompression)
        }
    }
    finally {
        $zip.Dispose()
    }

    $mb = [math]::Round((Get-Item $ZipPath).Length / 1MB, 1)
    Write-Host ("Wrote {0} ({1} files, {2} MB)" -f $ZipPath, $Files.Count, $mb)
}

if (-not (Test-Path $MapsDir)) {
    throw "Maps directory not found: $MapsDir"
}

$charcoal = @(Get-ChildItem (Join-Path $MapsDir "map_bg_*_new.png"))
$blank = Get-ChildItem (Join-Path $MapsDir "map_details_blank.png") -ErrorAction SilentlyContinue
if ($blank) {
    $charcoal += $blank
}

if ($charcoal.Count -eq 0) {
    throw "No charcoal map_bg_*_new.png files found in $MapsDir"
}

$color = @(Get-ChildItem (Join-Path $MapsDir "map_bg_*_color.png"))
if ($color.Count -eq 0) {
    Write-Warning "No color map_bg_*_color.png files found in $MapsDir - Color pack skipped."
}

$charcoalZip = Join-Path $OutDir "DetailedMaps.Charcoal.zip"
$colorZip = Join-Path $OutDir "DetailedMaps.Color.zip"

New-FlatZip -ZipPath $charcoalZip -Files $charcoal

if ($color.Count -gt 0) {
    New-FlatZip -ZipPath $colorZip -Files $color
}

if ($Deploy) {
    if (-not (Test-Path $ModsPath)) {
        throw "Mods path not found: $ModsPath"
    }

    Copy-Item $charcoalZip $ModsPath -Force
    Write-Host ("Deployed DetailedMaps.Charcoal.zip -> {0}" -f $ModsPath)

    if (Test-Path $colorZip) {
        Copy-Item $colorZip $ModsPath -Force
        Write-Host ("Deployed DetailedMaps.Color.zip -> {0}" -f $ModsPath)
    }
}
