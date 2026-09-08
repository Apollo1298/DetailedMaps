# Builds the Unity 6 detailedmaps asset bundle and copies it to the repo root.
# Requires Unity 6000.x installed via Unity Hub.

param(
    # Prefer TLD's exact Unity version (6000.0.60f1). Bundles from newer editors often fail to load.
    [string]$UnityEditor = "",
    [string]$ProjectPath = "$PSScriptRoot\Unity",
    [string]$LogFile = "$PSScriptRoot\Unity\build-assetbundle.log"
)

if (-not $UnityEditor) {
    $candidates = @(
        "C:\Program Files\Unity\Hub\Editor\6000.0.60f1\Editor\Unity.exe",
        "C:\Program Files\Unity\Hub\Editor\6000.0.58f2\Editor\Unity.exe",
        "C:\Program Files\Unity\Hub\Editor\6000.0.50f1\Editor\Unity.exe",
        "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe"
    )
    $UnityEditor = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $UnityEditor) {
        Write-Error "No Unity 6000 editor found under C:\Program Files\Unity\Hub\Editor. Install 6000.0.60f1 (matches TLD) via Unity Hub."
    }
    if ($UnityEditor -notmatch '6000\.0\.') {
        Write-Warning "Using $UnityEditor — TLD runs 6000.0.60f1. Install that version if LoadFromFile returns null."
    }
}

$ErrorActionPreference = "Stop"

if (-not (Test-Path $UnityEditor)) {
    Write-Error "Unity Editor not found at: $UnityEditor"
}

if (-not (Test-Path $ProjectPath)) {
    Write-Error "Unity project not found at: $ProjectPath"
}

Write-Host "Building detailedmaps with: $UnityEditor"
Write-Host "Project: $ProjectPath"
Write-Host "Log: $LogFile"

& $UnityEditor `
    -batchmode `
    -nographics `
    -quit `
    -projectPath $ProjectPath `
    -executeMethod BuildDetailedMapsBundle.Build `
    -logFile $LogFile

$exitCode = $LASTEXITCODE
if ($exitCode -ne 0) {
    Write-Host "Unity exited with code $exitCode. Last 40 log lines:"
    if (Test-Path $LogFile) {
        Get-Content $LogFile -Tail 40
    }
    exit $exitCode
}

$bundle = Join-Path $PSScriptRoot "detailedmaps"
if (-not (Test-Path $bundle)) {
    Write-Error "Build finished but detailedmaps was not written to repo root."
}

$bytes = [System.IO.File]::ReadAllBytes($bundle)[0..80]
$header = [System.Text.Encoding]::ASCII.GetString($bytes) -replace '[^\x20-\x7E]', '.'
Write-Host "OK: $bundle ($([math]::Round((Get-Item $bundle).Length / 1MB, 1)) MB)"
Write-Host "Header: $header"
Write-Host ""
Write-Host "Next: dotnet build DetailedMaps.sln -c Release"
