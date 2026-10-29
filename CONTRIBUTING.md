# Contributing to Project Mantis

Thanks for helping. Issues and pull requests are welcome in English or Turkish.

## Ground rules

These keep Mantis small, safe and trustworthy. Pull requests that break them will be asked to change.

1. **No new dependencies.** Only the .NET Framework 4.8 class library, CLI tools that ship with Windows
   (`powershell`, `winget`, `MpCmdRun.exe`, `netsh`, `sc`, `schtasks`, `reg`, `manage-bde`, `powercfg`) and P/Invoke.
   No NuGet packages. The single-exe, SDK-free build must keep working.
2. **Nothing changes silently.** Every action that changes a setting says what it will do, waits for confirmation
   and can be reverted. Nothing runs in the background.
3. **Defensive only.** No exploits, no scanning of remote targets, no web scanning. Network features stay on the
   user's own local subnet. The security module never deletes or quarantines files.
4. **Resilient.** Wrap every external command in `try/catch`; a missing tool is a message, not a crash. Do not rely
   on English command output (Windows may be Turkish or any other language).
5. **Safe arguments.** Validate or escape any user input (paths, winget ids) before it reaches a command line.
6. **Privacy.** No telemetry. Any new network request must be user-initiated and listed in `PRIVACY.md`.

## Building

```powershell
.\build.ps1                          # dist\ProjectMantis-v<version>.exe
.\build.ps1 -OutDir obj\testbuild    # while a copy of Mantis is running
dist\ProjectMantis-v<version>.exe --self-test
```

Useful developer flags: `--page=<dashboard|clean|tools|updates|security|settings|startup|services|tweaks|quick>`,
`--lang=tr|en`, `--snapshot="obj\snap\x.png"` (off-screen render, never a desktop capture), `--wait=N`,
`--size=WxH`, `--run=hardening,persistence`, `--setup-snapshot=file.png --setup-step=N`.

## Code style

- C# 5 only (the Framework `csc.exe`): no `$""`, `?.`, `nameof`, expression-bodied members, `out var` or tuples.
- The UI is built in code with the components in `UI\Components.cs` and the tokens in `UI\Theme.cs`.
- Every user-facing string comes in Turkish and English.
- Match the surrounding code: naming, comment density, file header.

## Pull requests

- One topic per pull request, with a short description of what changed and how you tested it.
- Screenshots for UI changes (use `--snapshot`; avoid showing personal data such as computer names or MAC addresses).
- CI must be green.

By contributing you agree that your contribution is licensed under the [MIT License](LICENSE).
