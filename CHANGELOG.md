# Changelog

## Unreleased

### Security
- Cleaner: never follows junctions or symbolic links. Before this fix, a link planted in a temp folder could make an elevated Mantis delete files outside that folder.
- Links and files open only for `https`, Windows settings pages and Mantis' own reports and logs. As administrator, they now open without admin rights.
- Check-Up HTML reports carry a Content-Security-Policy that blocks scripts.
- A command line can no longer start the local network scan without the consent dialog.
- New SECURITY.md and CODE_SIGNING_POLICY.md.

### Fixes
- The build stores the logo as a stream resource, which fixes a rare start-up crash.

## 1.4.0 — 2026-10-29

### Security Check-Up (new)
- Five modules with a 0–100 health score: Hardening Audit, Persistence & Keylogger Hunt, Malware Scan (Microsoft Defender), Software Vulnerabilities and Local Network Scan.
- Guided fixes that explain the change and ask before applying; admin-only checks offer a restart as administrator.
- Local HTML/JSON report after every run.
- The network scan covers only your own subnet and asks you to confirm you own or may test the network.

### Updates (new)
- Outdated apps through winget, Windows Update status, update one or all after a confirmation.

### Tools (new)
- Startup Manager: enable/disable autostart entries the same way Task Manager does.
- Services: allowlisted safe-to-disable services with exact restore.
- Ease Up Windows: Bing results off in Start search, file extensions, hidden files, This PC, classic context menu, taskbar items, suggestions/ads; every tweak remembers the previous value.
- Grab Text (`Ctrl+Shift+X`): offline screen OCR with Windows' built-in engine; translation opens Google Translate in the browser on request.
- Quick Tools: network info, large/duplicate file finder (Recycle Bin only), hosts editor with backups, display/sleep timeouts.

### Install, design and legal
- Windowed per-user setup wizard with license agreement and an uninstaller in Settings → Apps; portable zip still available.
- New "organic precision" interface: graphite panels, mantis lime accents, live CPU/RAM/disk gauges.
- License agreement on first launch, PRIVACY.md, updated EULA and trademark notices.
- Builds and releases now run on GitHub Actions.

## 1.3.1 — 2026-10-20

- Added a symmetric **Support Left** preset to Audio Balance.
- Widened the balance dialog so Center, Support Left, Support Right and Apply remain clear and accessible.

## 1.3.0 — 2026-10-20

- Added a minimal Left / Right Audio Balance tool for stereo-capable Windows outputs.
- Added independent percentage sliders, Center and Support Right presets.
- Uses the built-in Windows Core Audio endpoint API; no driver, capture, injection, or third-party audio component.
- Added stereo capability detection and safe error handling for unavailable outputs.

## 1.2.0 — 2026-10-07

- Added safe allowlist-based Temp, Adobe cache, shader cache and crash-report cleanup.
- Added scan-before-clean and Adobe-active protection.
- Added memory working-set trimming, reversible performance power plan, Keep Awake and DNS refresh.
- Added Turkish/English interface, launch-at-login and administrator restart.
- Added first-run close behavior choice and system-tray support.
- Added GitHub, Buy Me a Coffee and publisher attribution.
- Added Ghost Overlay for compact browser Picture-in-Picture windows and media players.
- Added opacity control and `Ctrl+Shift+M` interaction lock toggle.
- Excluded full-sized browser windows from Ghost Overlay selection.
- Fixed application startup and logo resource loading.
