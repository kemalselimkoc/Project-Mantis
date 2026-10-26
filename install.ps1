# Project Mantis developer install script — https://github.com/kemalselimkoc/Project-Mantis
# Copyright (c) 2026 kemalselimkoc. MIT licensed.
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $project 'build.ps1')
$version = [regex]::Match((Get-Content (Join-Path $project 'AppInfo.cs') -Raw), 'Version\s*=\s*"([^"]+)"').Groups[1].Value
$exe = Join-Path $project "dist\ProjectMantis-v$version.exe"
$desktop = [Environment]::GetFolderPath('Desktop')
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $desktop 'Project Mantis.lnk'))
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = Split-Path $exe
$shortcut.IconLocation = "$exe,0"
$shortcut.Description = 'Project Mantis — System Care'
$shortcut.Save()
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
New-ItemProperty -Path $runKey -Name 'ProjectMantis' -Value ('"' + $exe + '"') -PropertyType String -Force | Out-Null
Write-Host "Desktop shortcut created: $desktop\Project Mantis.lnk"
Write-Host 'Launch at login enabled.'
