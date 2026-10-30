param(
    [switch]$Test
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root "src"
$dist = Join-Path $root "dist"
$fw = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$wpf = Join-Path $fw "WPF"
$csc = Join-Path $fw "csc.exe"

$package = Get-AppxPackage -Name "7906AAC0.dynabookSupportUtility" |
    Sort-Object Version -Descending |
    Select-Object -First 1

if (-not $package) {
    throw "dynabook Support Utility package was not found."
}

$appDir = Join-Path $package.InstallLocation "dynabook Support Utility"
if (-not (Test-Path (Join-Path $appDir "PluginLib.dll"))) {
    throw "PluginLib.dll was not found: $appDir"
}

New-Item -ItemType Directory -Force -Path $dist | Out-Null

Write-Host "Building DsuExt.dll"
$pluginArgs = @(
    "/nologo",
    "/codepage:65001",
    "/target:library",
    "/out:$dist\DsuExt.dll",
    "/r:$appDir\PluginLib.dll",
    "/r:$appDir\libTMachInfo.dll",
    "/r:$wpf\PresentationFramework.dll",
    "/r:$wpf\PresentationCore.dll",
    "/r:$wpf\WindowsBase.dll",
    "/r:$fw\System.Xaml.dll",
    "/r:$fw\System.dll",
    "/r:$fw\System.Core.dll",
    "/r:$fw\System.Management.dll",
    "$src\DsuExt\DsuExt.cs"
)
& $csc @pluginArgs
if ($LASTEXITCODE -ne 0) {
    throw "DsuExt build failed."
}

Write-Host "Building DsuModPatcher.exe"
$patcherArgs = @(
    "/nologo",
    "/codepage:65001",
    "/target:exe",
    "/out:$dist\DsuModPatcher.exe",
    "/r:$root\lib\Mono.Cecil.dll",
    "/r:$fw\System.dll",
    "/r:$fw\System.Core.dll",
    "$src\DsuModPatcher\DsuModPatcher.cs"
)
& $csc @patcherArgs
if ($LASTEXITCODE -ne 0) {
    throw "DsuModPatcher build failed."
}

Copy-Item "$root\lib\Mono.Cecil.dll" "$dist\Mono.Cecil.dll" -Force

if ($Test) {
    Write-Host "Building PluginSmokeTest.exe"
    $testBin = Join-Path $root "testbin"
    New-Item -ItemType Directory -Force -Path $testBin | Out-Null
    Get-ChildItem $appDir -Filter "*.dll" -File | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $testBin $_.Name) -Force
    }
    Copy-Item "$dist\DsuExt.dll" (Join-Path $testBin "DsuExt.dll") -Force

    $testArgs = @(
        "/nologo",
        "/codepage:65001",
        "/target:exe",
        "/out:$testBin\PluginSmokeTest.exe",
        "/r:$appDir\PluginLib.dll",
        "/r:$dist\DsuExt.dll",
        "/r:$wpf\PresentationFramework.dll",
        "/r:$wpf\PresentationCore.dll",
        "/r:$wpf\WindowsBase.dll",
        "/r:$fw\System.Xaml.dll",
        "/r:$fw\System.dll",
        "/r:$fw\System.Core.dll",
        "$root\tests\PluginSmokeTest.cs"
    )
    & $csc @testArgs
    if ($LASTEXITCODE -ne 0) {
        throw "PluginSmokeTest build failed."
    }
}

if (Test-Path (Join-Path $root "inject")) {
    Copy-Item "$dist\DsuExt.dll" (Join-Path $root "inject\DsuExt.dll") -Force
}
$portable = Join-Path (Split-Path $root -Parent) "DSU Mod Portable"
if (Test-Path $portable) {
    Copy-Item "$dist\DsuExt.dll" (Join-Path $portable "DsuExt.dll") -Force
}

Write-Host "Output: $dist"
Get-ChildItem $dist | Select-Object Name, Length | Format-Table -AutoSize


