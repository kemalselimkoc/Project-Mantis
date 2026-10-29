# Project Mantis developer install script — https://github.com/kemalselimkoc/Project-Mantis
# Copyright (c) 2026 kemalselimkoc. MIT licensed.
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path

# Like any installer: show the license agreement first and continue only after it is accepted.
Write-Host ''
Write-Host (Get-Content (Join-Path $project 'EULA.txt') -Raw -Encoding UTF8)
Write-Host ''
Write-Host 'By continuing you accept the agreement above, including that you use Project Mantis at your own risk'
Write-Host 'and that the developer is not liable for any damage to your computer, data or hardware.'
Write-Host 'Devam ederek yukaridaki sozlesmeyi, yazilimi kendi riskinizle kullandiginizi ve bilgisayariniza,'
Write-Host 'verilerinize veya donaniminiza gelebilecek zararlardan gelistiricinin sorumlu olmadigini kabul edersiniz.'
$answer = Read-Host 'Accept? / Kabul ediyor musunuz? (Y/E = yes, anything else = no)'
if ($answer -notmatch '^\s*[YyEe]') { Write-Host 'Installation cancelled. / Kurulum iptal edildi.'; exit 1 }

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
Write-Host "Desktop shortcut created: $desktop\Project Mantis.lnk"
# Launch at login is opt-in; it can also be changed later in Mantis Settings.
$login = Read-Host 'Start Project Mantis at Windows login? / Windows acilisinda baslasin mi? (Y/E = yes)'
if ($login -match '^\s*[YyEe]') {
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    New-ItemProperty -Path $runKey -Name 'ProjectMantis' -Value ('"' + $exe + '"') -PropertyType String -Force | Out-Null
    Write-Host 'Launch at login enabled.'
}
