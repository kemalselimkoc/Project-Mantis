# Third-party notices

Project Mantis is built against Microsoft .NET Framework and Windows desktop
APIs already supplied by Windows (including the `Windows.Media.Ocr` WinRT API,
whose metadata ships with Windows). These platform components are not
distributed with Project Mantis and are not covered by its MIT License.

No third-party font files, runtimes, libraries, drivers, services or binaries
are bundled in the release package. The interface requests fonts supplied by
Windows (Bahnschrift, Cascadia Mono, Segoe UI) and falls back according to the
operating system's font resolution.

The Project Mantis logo is an original AI-assisted artwork created for this
project by kemalselimkoc.

## Tools and services Mantis can start for you

Mantis invokes these tools that come with Windows, only when you ask:
`winget`, `MpCmdRun.exe`, `powershell`, `netsh`, `sc`, `schtasks`, `reg`,
`manage-bde`, `powercfg`. Each is governed by its own license from Microsoft.
Software installed or updated through winget is licensed to you by its own
publisher, not by Project Mantis.

Optional online services opened on your click — Google Translate
(translate.google.com, in your browser) and ipify (api.ipify.org) — are
provided by their operators under their own terms. Mantis is not affiliated
with, endorsed by or sponsored by them.

## Trademarks

Microsoft, Windows, Microsoft Defender, BitLocker, Xbox and Bing are
trademarks of the Microsoft group of companies. Google and Google Translate
are trademarks of Google LLC. Adobe is a trademark of Adobe Inc. All other
names are the property of their respective owners and are used only to
describe compatibility. Project Mantis is an independent project and is not
affiliated with, endorsed by or sponsored by any of these companies.
