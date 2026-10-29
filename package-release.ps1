# Project Mantis release packager — https://github.com/kemalselimkoc/Project-Mantis
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$version = [regex]::Match((Get-Content (Join-Path $project 'AppInfo.cs') -Raw), 'Version\s*=\s*"([^"]+)"').Groups[1].Value
$builtExe = Join-Path $project "dist\ProjectMantis-v$version.exe"
if (-not (Test-Path $builtExe)) { & (Join-Path $project 'build.ps1') }

$stage = Join-Path $project "release\Project-Mantis-v$version"
$zip = Join-Path $project "release\Project-Mantis-v$version-win64.zip"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'assets') | Out-Null
Copy-Item $builtExe (Join-Path $stage 'ProjectMantis.exe')
Copy-Item (Join-Path $project 'assets\mantis-logo.png') (Join-Path $stage 'assets\mantis-logo.png')
Copy-Item (Join-Path $project 'packaging\install-release.ps1') (Join-Path $stage 'install-release.ps1')
Copy-Item (Join-Path $project 'packaging\Install Project Mantis.bat') (Join-Path $stage 'Install Project Mantis.bat')
Copy-Item (Join-Path $project 'README.md') (Join-Path $stage 'README.md')
Copy-Item (Join-Path $project 'LICENSE') (Join-Path $stage 'LICENSE')
Copy-Item (Join-Path $project 'EULA.txt') (Join-Path $stage 'EULA.txt')
Copy-Item (Join-Path $project 'THIRD_PARTY_NOTICES.md') (Join-Path $stage 'THIRD_PARTY_NOTICES.md')
Copy-Item (Join-Path $project 'PRIVACY.md') (Join-Path $stage 'PRIVACY.md')
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Get-FileHash $zip -Algorithm SHA256 | Format-List
