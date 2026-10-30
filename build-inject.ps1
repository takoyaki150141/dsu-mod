$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$inject = Join-Path $root "inject"
$tccDir = Join-Path $root "tools\tcc"
$tcc = Join-Path $tccDir "tcc.exe"
$fw = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$wpf = Join-Path $fw "WPF"
$csc = Join-Path $fw "csc.exe"

if (-not (Test-Path $tcc)) { throw "tcc.exe was not found: $tcc" }

Write-Host "Building DsuBootstrap.dll"
Push-Location $tccDir
try {
    & $tcc -shared -o (Join-Path $inject "DsuBootstrap.dll") (Join-Path $inject "DsuBootstrap.c") -luser32
    if ($LASTEXITCODE -ne 0) { throw "DsuBootstrap build failed." }
}
finally {
    Pop-Location
}

Write-Host "Building DsuInjector.exe"
& $csc /nologo /codepage:65001 /target:exe /platform:x64 /main:DsuMod.Program /out:"$inject\DsuInjector.exe" /r:"$wpf\PresentationFramework.dll" /r:"$wpf\PresentationCore.dll" /r:"$wpf\WindowsBase.dll" /r:"$fw\System.Xaml.dll" /r:"$fw\System.dll" /r:"$fw\System.Core.dll" "$inject\DsuInjector.cs" "$inject\DsuInjectorPayload.cs"
if ($LASTEXITCODE -ne 0) { throw "DsuInjector build failed." }

$plugin = Join-Path $root "dist\DsuExt.dll"
if (Test-Path $plugin) {
    Copy-Item $plugin (Join-Path $inject "DsuExt.dll") -Force
}

Get-ChildItem $inject | Select-Object Name, Length | Format-Table -AutoSize


