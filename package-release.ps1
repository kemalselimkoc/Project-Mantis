# Project Mantis release packager — https://github.com/kemalselimkoc/Project-Mantis
# Produces two downloads: ProjectMantis-Setup-vX.exe (windowed installer, the same exe in setup mode)
# and Project-Mantis-vX-portable.zip (run without installing). Prints SHA-256 for both.
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$version = [regex]::Match((Get-Content (Join-Path $project 'AppInfo.cs') -Raw), 'Version\s*=\s*"([^"]+)"').Groups[1].Value
$builtExe = Join-Path $project "dist\ProjectMantis-v$version.exe"
if (-not (Test-Path $builtExe)) { & (Join-Path $project 'build.ps1') }

$releaseDir = Join-Path $project 'release'
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
$setup = Join-Path $releaseDir "ProjectMantis-Setup-v$version.exe"
Copy-Item $builtExe $setup -Force

$stage = Join-Path $releaseDir "Project-Mantis-v$version"
$zip = Join-Path $releaseDir "Project-Mantis-v$version-portable.zip"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'assets') | Out-Null
Copy-Item $builtExe (Join-Path $stage 'ProjectMantis.exe')
Copy-Item (Join-Path $project 'assets\mantis-logo.png') (Join-Path $stage 'assets\mantis-logo.png')
foreach ($doc in 'README.md', 'LICENSE', 'EULA.txt', 'THIRD_PARTY_NOTICES.md', 'PRIVACY.md') { Copy-Item (Join-Path $project $doc) (Join-Path $stage $doc) }
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Get-FileHash $setup, $zip -Algorithm SHA256 | Format-List
