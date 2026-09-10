# Builds per-region BC7 AssetBundles into out\bundles\ (one .bundle per map texture).
# Requires Unity 6000.0.60f1 (TLD) via Unity Hub.

param(
    [ValidateSet("charcoal", "color", "both", "base", "recipe", "packzip")]
    [string]$Pack = "charcoal",

    [string]$UnityEditor = "",
    [string]$ProjectPath = "$PSScriptRoot\Unity",
    [string]$LogFile = "$PSScriptRoot\Unity\build-assetbundle.log",
    [string]$OutDir = "$PSScriptRoot\out\bundles"
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
        Write-Warning "Using $UnityEditor - TLD runs 6000.0.60f1. Install that version if LoadFromFile returns null."
    }
}

$ErrorActionPreference = "Stop"

if (-not (Test-Path $UnityEditor)) {
    Write-Error "Unity Editor not found at: $UnityEditor"
}

if (-not (Test-Path $ProjectPath)) {
    Write-Error "Unity project not found at: $ProjectPath"
}

$executeMethod = switch ($Pack) {
    "charcoal" { "BuildDetailedMapsBundle.BuildCharcoal" }
    "color"    { "BuildDetailedMapsBundle.BuildColor" }
    "both"     { "BuildDetailedMapsBundle.Build" }
    "base"     { "BuildDetailedMapsBundle.BuildLayeredBases" }
    "recipe"   { "BuildDetailedMapsBundle.BuildLayeredRecipe" }
    "packzip"  { "BuildDetailedMapsBundle.PackReleaseZip" }
}

Write-Host "Building pack=$Pack (per-region) with: $UnityEditor"
Write-Host "Method: $executeMethod"
Write-Host "Project: $ProjectPath"
Write-Host "Log: $LogFile"
Write-Host "Out: $OutDir"

$unityArgs = @(
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath", $ProjectPath,
    "-executeMethod", $executeMethod,
    "-logFile", $LogFile
)
$proc = Start-Process -FilePath $UnityEditor -ArgumentList $unityArgs -Wait -PassThru
$exitCode = $proc.ExitCode
if ($null -eq $exitCode -or $exitCode -ne 0) {
    Write-Host "Unity exited with code $exitCode. Last 60 log lines:"
    if (Test-Path $LogFile) {
        Get-Content $LogFile -Tail 60
    }
    if ($null -eq $exitCode) { exit 1 }
    exit $exitCode
}

function Format-MB([long]$bytes) {
    return [math]::Round($bytes / 1MB, 1)
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$bundles = @(Get-ChildItem $OutDir -Filter "*.bundle" -ErrorAction SilentlyContinue)
if ($bundles.Count -eq 0) {
    Write-Error "Build finished but no .bundle files in $OutDir"
}

$total = ($bundles | Measure-Object -Property Length -Sum).Sum
Write-Host ("OK: {0} per-region bundles, {1} MB total in {2}" -f $bundles.Count, (Format-MB $total), $OutDir)

if ($Pack -eq "charcoal" -or $Pack -eq "both") {
    $charcoal = @($bundles | Where-Object { $_.Name -like "*_new.bundle" -or $_.Name -eq "map_details_blank.bundle" })
    $charcoalBytes = ($charcoal | Measure-Object Length -Sum).Sum
    $zip = Join-Path $PSScriptRoot "DetailedMaps.Charcoal.zip"
    if (Test-Path $zip) {
        $zipBytes = (Get-Item $zip).Length
        $reduction = [math]::Round((1 - ($charcoalBytes / $zipBytes)) * 100, 1)
        Write-Host ("Charcoal: {0} files, {1} MB bundles vs {2} MB PNG zip ({3} percent reduction)" -f `
            $charcoal.Count, (Format-MB $charcoalBytes), (Format-MB $zipBytes), $reduction)
    } else {
        Write-Host ("Charcoal: {0} files, {1} MB" -f $charcoal.Count, (Format-MB $charcoalBytes))
    }
}

if ($Pack -eq "color" -or $Pack -eq "both") {
    $color = @($bundles | Where-Object { $_.Name -like "*_color.bundle" })
    $colorBytes = ($color | Measure-Object Length -Sum).Sum
    $zip = Join-Path $PSScriptRoot "DetailedMaps.Color.zip"
    if ((Test-Path $zip) -and $color.Count -gt 0) {
        $zipBytes = (Get-Item $zip).Length
        $reduction = [math]::Round((1 - ($colorBytes / $zipBytes)) * 100, 1)
        Write-Host ("Color: {0} files, {1} MB bundles vs {2} MB PNG zip ({3} percent reduction)" -f `
            $color.Count, (Format-MB $colorBytes), (Format-MB $zipBytes), $reduction)
    } elseif ($color.Count -gt 0) {
        Write-Host ("Color: {0} files, {1} MB" -f $color.Count, (Format-MB $colorBytes))
    }
}

Write-Host ""
if ($Pack -eq "recipe" -or $Pack -eq "packzip") {
    Write-Host "Skipping LoadFromFile verify for pack=$Pack"
    Write-Host "Log: $LogFile"
    exit 0
}

Write-Host "Verifying sample bundle LoadFromFile in Unity Editor..."
$verifyLog = Join-Path $ProjectPath "verify-bundle.log"
$verifyProc = Start-Process -FilePath $UnityEditor -ArgumentList @(
    "-batchmode", "-nographics", "-quit",
    "-projectPath", $ProjectPath,
    "-executeMethod", "VerifyDetailedMapsBundle.Verify",
    "-logFile", $verifyLog
) -Wait -PassThru
if ($null -eq $verifyProc.ExitCode -or $verifyProc.ExitCode -ne 0) {
    Write-Host "Verify failed (exit=$($verifyProc.ExitCode)). Last 30 log lines:"
    if (Test-Path $verifyLog) { Get-Content $verifyLog -Tail 30 }
    exit 1
}
Write-Host "Verify OK."
Write-Host "Log: $LogFile"
