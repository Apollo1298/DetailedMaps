# Sync TerrainDumper maps_base (*_new.png) → Maps/*_base.png (+ keep blank).
# Then build BC7 bases + recipe and pack out\DetailedMaps.zip

param(
    [string]$MapsBaseDir = "F:\Github\TerrainDumper\out\maps_base",
    [string]$MapsDir = "$PSScriptRoot\Maps",
    [string]$UnityEditor = "",
    [string]$ProjectPath = "$PSScriptRoot\Unity",
    [string]$OutDir = "$PSScriptRoot\out\bundles",
    [string]$PackZip = "$PSScriptRoot\out\DetailedMaps.zip"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $MapsBaseDir)) {
    Write-Error "maps_base not found: $MapsBaseDir"
}

New-Item -ItemType Directory -Force -Path $MapsDir | Out-Null

# Remove previous *_base.png from Maps (keep *_new / *_color / blank for repo).
Get-ChildItem $MapsDir -Filter "map_bg_*_base.png" -ErrorAction SilentlyContinue | Remove-Item -Force

$copied = 0
Get-ChildItem $MapsBaseDir -Filter "map_bg_*_new.png" | ForEach-Object {
    $destName = $_.Name -replace '_new\.png$', '_base.png'
    Copy-Item $_.FullName (Join-Path $MapsDir $destName) -Force
    $copied++
}
if ($copied -eq 0) {
    Write-Error "No map_bg_*_new.png in $MapsBaseDir"
}
if (-not (Test-Path (Join-Path $MapsDir "map_details_blank.png"))) {
    Write-Error "Missing Maps\map_details_blank.png"
}

Write-Host "Synced $copied base PNGs → $MapsDir (*_base.png)"

& "$PSScriptRoot\build-assetbundle.ps1" -Pack base -UnityEditor $UnityEditor -ProjectPath $ProjectPath -OutDir $OutDir
& "$PSScriptRoot\build-assetbundle.ps1" -Pack recipe -UnityEditor $UnityEditor -ProjectPath $ProjectPath -OutDir $OutDir

# Pack zip (no Unity needed)
$files = @()
$files += Get-ChildItem $OutDir -Filter "map_bg_*_base.bundle"
$files += Get-ChildItem $OutDir -Filter "map_details_blank.bundle" -ErrorAction SilentlyContinue
$files += Get-ChildItem $OutDir -Filter "DetailedMaps.Recipe.bundle" -ErrorAction SilentlyContinue
if ($files.Count -lt 2) {
    Write-Error "Expected base bundles + recipe in $OutDir (found $($files.Count))"
}

New-Item -ItemType Directory -Force -Path (Split-Path $PackZip) | Out-Null
if (Test-Path $PackZip) { Remove-Item $PackZip -Force }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($PackZip, 'Create')
try {
    foreach ($f in $files) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip, $f.FullName, $f.Name, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $zip.Dispose()
}

$totalMb = [math]::Round((Get-Item $PackZip).Length / 1MB, 1)
Write-Host "OK: $($files.Count) bundles → $PackZip ($totalMb MB)"
Write-Host "Install: DetailedMaps.dll + copy $PackZip to Mods\ as DetailedMaps.zip"
