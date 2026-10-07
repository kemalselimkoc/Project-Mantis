# Project Mantis public release installer — https://github.com/kemalselimkoc/Project-Mantis
param([switch]$NoStartup)
$ErrorActionPreference = 'Stop'
$package = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceExe = Join-Path $package 'ProjectMantis.exe'
$sourceAssets = Join-Path $package 'assets'
if (-not (Test-Path $sourceExe)) { throw 'ProjectMantis.exe is missing. Extract the complete ZIP before installing.' }

$installDir = Join-Path $env:LOCALAPPDATA 'Programs\Project Mantis'
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item $sourceExe (Join-Path $installDir 'ProjectMantis.exe') -Force
Copy-Item $sourceAssets (Join-Path $installDir 'assets') -Recurse -Force
Copy-Item (Join-Path $package 'LICENSE') (Join-Path $installDir 'LICENSE') -Force
Copy-Item (Join-Path $package 'EULA.txt') (Join-Path $installDir 'EULA.txt') -Force

$installedExe = Join-Path $installDir 'ProjectMantis.exe'
$desktop = [Environment]::GetFolderPath('Desktop')
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $desktop 'Project Mantis.lnk'))
$shortcut.TargetPath = $installedExe
$shortcut.WorkingDirectory = $installDir
$shortcut.IconLocation = "$installedExe,0"
$shortcut.Description = 'Project Mantis - System Care'
$shortcut.Save()

if (-not $NoStartup) {
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    New-ItemProperty -Path $runKey -Name 'ProjectMantis' -Value ('"' + $installedExe + '"') -PropertyType String -Force | Out-Null
}

Start-Process $installedExe
Write-Host 'Project Mantis installed successfully.'
