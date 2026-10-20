# Project Mantis

**Project Mantis** is a lightweight, local-first Windows care utility by
[kemalselimkoc](https://github.com/kemalselimkoc), with a Turkish/English glass interface.

[Download the latest release](https://github.com/kemalselimkoc/Project-Mantis/releases/latest) ·
[Support development](https://buymeacoffee.com/kemalselimkoc)

## Install for regular users

1. Download `Project-Mantis-v1.3.0-win64.zip` from the latest GitHub Release.
2. Extract the complete ZIP to a folder.
3. Double-click **Install Project Mantis.bat**.
4. Mantis creates a desktop shortcut, optionally launches with Windows, and opens automatically.

No Python, Node.js, .NET SDK, or separate runtime download is required on a
current Windows 10/11 installation. Windows may show a SmartScreen reputation
warning for an unsigned community build; verify the SHA-256 value shown on the
GitHub Release before running it.

## What it does

- Scans and cleans user/Windows temp locations, DirectX shader cache, crash reports, and known Adobe After Effects / Premiere Pro media-cache folders.
- Detects running Adobe processes and skips Adobe caches while they are in use.
- Trims idle process working sets so Windows can reuse memory. It does not disable the page file or purge critical system memory.
- Toggles the High Performance power plan and restores the previous plan when toggled off or when Mantis exits.
- Keeps the PC awake during long renders/downloads.
- Flushes DNS cache.
- Adjusts the default Windows output's left and right channel levels independently through the built-in Core Audio API, with no driver or audio capture.
- Ghost Overlay accepts compact browser Picture-in-Picture windows and supported media players while excluding full-size browser windows. It keeps the video always on top, click-through, and optionally translucent. `Ctrl+Shift+M` toggles video interaction; **Release Active** removes the overlay completely.
- Optional launch-at-login, per-user and easy to disable.
- Stores settings and a small operation log locally under `%LOCALAPPDATA%\ProjectMantis`.

## Build from source

Run `install.ps1` from PowerShell. It builds `dist\ProjectMantis-v1.3.0.exe` using the Windows .NET Framework compiler and creates a desktop shortcut. No network download is required.

Closing Mantis asks once whether the close button should exit or minimize to the system tray. The choice can be changed later in Settings. The tray icon can restore or fully exit the app.

Mantis starts in standard-user mode. Use **Restart as administrator** inside Settings only when you want access to protected Windows Temp and Prefetch files. Locked files are skipped.

## Privacy

Mantis works locally and contains no telemetry or analytics. Settings and the
operation log stay under `%LOCALAPPDATA%\ProjectMantis`. The GitHub and Buy Me a
Coffee buttons only open the listed external pages.

## Safety notes

- Cleanup is allowlist-based; arbitrary user folders are never scanned or deleted.
- Adobe caches are skipped whenever a supported Adobe process is active.
- Power-plan changes are reversible.
- Memory trimming may make background apps reload some data; Windows automatically gives memory back when needed.
- Audio Balance changes the per-channel level exposed by the active Windows output. Begin at a comfortable volume and avoid sudden increases; Mantis is not a medical or hearing-calibration device.
- Ghost Overlay only changes standard Windows styles on the selected browser/media-player window. It does not inject into, inspect, hook, or modify a game process. No utility can guarantee compatibility with every third-party anti-cheat, so use the feature only with supported video windows and release it if a game objects to overlays.

## License and publisher

Copyright © 2026 **kemalselimkoc**. Original source code is available under the
[MIT License](LICENSE). See [EULA.txt](EULA.txt) for the end-user notice and
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for platform attribution.

- GitHub: https://github.com/kemalselimkoc
- Support: https://buymeacoffee.com/kemalselimkoc
