# Code signing

Release builds are produced by GitHub Actions (`.github/workflows/build.yml`) on
GitHub-hosted Windows runners, so the signed exe can be traced back to a public
commit. Until signing is configured, releases are published **unsigned** and
the release notes list the SHA-256 of every file.

## How a release is made

1. Bump `Version` in `AppInfo.cs`, update `CHANGELOG.md`, commit and push.
2. Tag the commit with the same version and push the tag:
   `git tag v1.4.0` then `git push origin v1.4.0`
3. The workflow builds, runs the self-test, signs (if configured), runs
   `package-release.ps1` and publishes the GitHub Release with
   `ProjectMantis-Setup-vX.exe`, `Project-Mantis-vX-portable.zip` and their hashes.
   A tag that does not match `AppInfo.cs` stops the release.

## Option A — SignPath Foundation (free for open source)

SignPath Foundation signs open-source projects at no cost. Requirements:
an OSI-approved license (MIT ✓), a public repository (✓), builds on
GitHub-hosted runners (✓), and a project that is actively maintained and
released. The certificate is issued to SignPath Foundation, which appears as
the publisher.

1. Apply at https://signpath.org (open-source program) with the repository URL.
2. After approval, in the SignPath portal create the project, an artifact
   configuration for a single PE file (`ProjectMantis-v*.exe`), and a
   release-signing policy; link the GitHub repository as a trusted build system.
3. In GitHub → Settings → Secrets and variables → Actions add:
   - secret `SIGNPATH_API_TOKEN`
   - variables `SIGNPATH_ORGANIZATION_ID`, `SIGNPATH_PROJECT_SLUG`,
     `SIGNPATH_SIGNING_POLICY_SLUG`
4. The `sign` job switches on by itself on the next `v*` tag.

## Option B — Certum Open Source Code Signing (paid, in your own name)

An affordable certificate for individual open-source developers, issued after
identity verification; the key lives in Certum's cloud (SimplySign). Signing
then runs on the maintainer's PC:

```
signtool sign /fd sha256 /tr http://time.certum.pl /td sha256 /a release\ProjectMantis-Setup-vX.exe
```

`signtool` comes with the Windows SDK, so this step runs on the maintainer's
machine and is not part of the SDK-free build.

## SmartScreen

A signature does not remove the SmartScreen warning on day one. Microsoft
builds reputation per certificate and per file over downloads. Signing makes
that reputation accumulate and shows a verified publisher instead of
"Unknown publisher".
