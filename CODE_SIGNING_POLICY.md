# Code signing policy

Free code signing for Project Mantis is planned through [SignPath.io](https://about.signpath.io/), with a certificate by the [SignPath Foundation](https://signpath.org/). Until that is in place, release files are unsigned and their SHA-256 values are published on each release page.

## What gets signed

Only files built by the public GitHub Actions workflow ([`.github/workflows/build.yml`](.github/workflows/build.yml)) from a version tag in this repository:

- `ProjectMantis-Setup-vX.Y.Z.exe`
- `ProjectMantis.exe` inside `Project-Mantis-vX.Y.Z-portable.zip` (the same binary)

Nothing built on a personal computer is signed. No third-party binaries are bundled or signed.

## Team roles

| Role | Who |
| --- | --- |
| Committers and reviewers | [Kemal Selim Koc](https://github.com/kemalselimkoc) |
| Approvers (approve each signing request) | [Kemal Selim Koc](https://github.com/kemalselimkoc) |

Outside contributions arrive as pull requests and are reviewed before they are merged. Everyone with commit or approval rights is required to use multi-factor authentication on GitHub and SignPath.

## Privacy

This program does not send any information to networked systems unless the user asks it to. The connections it can make, each started by the user, are listed in [PRIVACY.md](PRIVACY.md).
