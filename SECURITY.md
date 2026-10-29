# Security policy

## Supported versions

Only the latest release gets security fixes. Please update before reporting.

## Reporting a vulnerability

Please **do not open a public issue** for security problems. Report them privately through GitHub: [Report a vulnerability](https://github.com/kemalselimkoc/Project-Mantis/security/advisories/new).

Include the Mantis version, your Windows version, and the steps to reproduce. You will get an answer within 7 days. Once a fix is released, the advisory is published and you are credited unless you ask not to be.

## Scope

In scope: anything in this repository, including the app, the installer and uninstaller, the build scripts and the release workflow.

Out of scope: problems in Windows itself or in tools Mantis calls (winget, Microsoft Defender, PowerShell), and Defender or SmartScreen warnings about unsigned builds. Those are known; see [CODE_SIGNING_POLICY.md](CODE_SIGNING_POLICY.md).

## Design rules

These rules are checked in every review:

- External tools are started from `System32` by full path. User values never reach a shell: PowerShell gets `-EncodedCommand`, other tools get quoted arguments or allowlisted tokens.
- Nothing runs in the background, and every system change asks first.
- File operations never follow junctions or symbolic links, so a link planted in a user-writable folder cannot redirect an elevated Mantis.
- Links are opened only for `https`, Windows settings pages and Mantis' own files. As administrator, Mantis hands them to Explorer so browsers start without admin rights.
- Local HTML reports escape every value and carry a Content-Security-Policy that blocks scripts.
