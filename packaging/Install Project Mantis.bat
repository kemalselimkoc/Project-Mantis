@echo off
rem Project Mantis installer - https://github.com/kemalselimkoc/Project-Mantis
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-release.ps1"
if errorlevel 1 (
  echo.
  echo Installation failed. Keep all extracted files together and try again.
  pause
)
