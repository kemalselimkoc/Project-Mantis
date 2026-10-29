# Privacy policy

_Last updated: 2026-10-29 · applies to Project Mantis v1.4 and later_

Project Mantis is a local Windows utility. The developer (kemalselimkoc) does
not run any server for it and **does not receive, collect, sell or share any
data about you or your computer**. There is no telemetry, analytics, crash
upload, advertising or account.

## What stays on your computer

Everything Mantis stores is kept under `%LOCALAPPDATA%\ProjectMantis` and never
leaves the machine unless you copy it yourself:

| File | Contents |
| --- | --- |
| `settings.ini` | Language, tray behaviour, Check-Up module choice, accepted terms version |
| `mantis.log` | Your own activity log (cleanups, updates, fixes, toggles) |
| `reports\` | Security Check-Up HTML/JSON reports you generated |
| `*-backup.ini`, `hosts-backups\` | Previous values, so every change can be undone |

Check-Up reports can include device names, IP and MAC addresses seen on your
local network. They are written only to your own disk. Delete the folder above
to remove all Mantis data.

## Network connections

Mantis connects to the internet only for the actions below, and only when you
start them:

| Action | Where it goes | What is sent |
| --- | --- | --- |
| App updates (Updates page) | `winget` and the sources it is configured with (Microsoft Store / winget community repository, publisher download servers) | Standard winget requests, handled by Windows' own winget tool |
| Defender signature update / scan | Microsoft Defender (`MpCmdRun.exe`) | Handled by Windows itself |
| "Show public IP" (Quick Tools) | `https://api.ipify.org` | A single request; your IP address is visible to that service as with any website |
| "Translate" (Grab Text) | Opens `https://translate.google.com` in **your browser** | The text you choose is placed in the link; Google's own privacy policy and terms apply |
| GitHub / Buy Me a Coffee buttons | Opens those websites in your browser | Nothing from Mantis |

Text recognition (OCR) runs fully offline through Windows' built-in
`Windows.Media.Ocr`. The local network scan contacts only devices on your own
subnet and never the internet.

## Third-party services

When you use winget, Google Translate or api.ipify.org you use those services
directly, under their own terms and privacy policies. Mantis does not add any
identifier to those requests.

## Children

Mantis does not knowingly process personal data of anyone, including children.

## Contact

Questions: kemalselimkoc@gmail.com · https://github.com/kemalselimkoc/Project-Mantis
