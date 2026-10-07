# Project Mantis release packager — https://github.com/kemalselimkoc/Project-Mantis
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$builtExe = Join-Path $project 'dist\ProjectMantis-v1.2.0.exe'
if (-not (Test-Path $builtExe)) { & (Join-Path $project 'build.ps1') }

$stage = Join-Path $project 'release\Project-Mantis-v1.2.0'
$zip = Join-Path $project 'release\Project-Mantis-v1.2.0-win64.zip'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'assets') | Out-Null
Copy-Item (Join-Path $project 'dist\ProjectMantis-v1.2.0.exe') (Join-Path $stage 'ProjectMantis.exe')
Copy-Item (Join-Path $project 'assets\mantis-logo.png') (Join-Path $stage 'assets\mantis-logo.png')
Copy-Item (Join-Path $project 'packaging\install-release.ps1') (Join-Path $stage 'install-release.ps1')
Copy-Item (Join-Path $project 'packaging\Install Project Mantis.bat') (Join-Path $stage 'Install Project Mantis.bat')
Copy-Item (Join-Path $project 'README.md') (Join-Path $stage 'README.md')
Copy-Item (Join-Path $project 'LICENSE') (Join-Path $stage 'LICENSE')
Copy-Item (Join-Path $project 'EULA.txt') (Join-Path $stage 'EULA.txt')
Copy-Item (Join-Path $project 'THIRD_PARTY_NOTICES.md') (Join-Path $stage 'THIRD_PARTY_NOTICES.md')
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Get-FileHash $zip -Algorithm SHA256 | Format-List
