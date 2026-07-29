# Release process

JFlightShaker uses semantic versions and publishes Windows self-contained ZIP
files through GitHub Releases.

## First public beta

The planned first public tag is:

```text
v0.5.0-beta.1
```

Before tagging:

1. Confirm `BuildInfo.DisplayVersion`, `Version` and `InformationalVersion`.
2. Move the matching section in `CHANGELOG.md` from `Unreleased` to the date.
3. Run the release builder:

```powershell
.\scripts\Build-Release.ps1
```

4. Extract the generated ZIP into a clean folder.
5. Test startup, audio selection, Test Device, one effect preview and Start/Stop.
6. Compare the ZIP against `artifacts/SHA256SUMS.txt`.
7. Commit the release changes.
8. Create and push the tag:

```powershell
git tag -a v0.5.0-beta.1 -m "JFlightShaker C.E v0.5.0-beta.1"
git push origin main
git push origin v0.5.0-beta.1
```

The tag triggers `.github/workflows/release.yml`, which runs tests, publishes
the application, creates a ZIP and checksum, and marks hyphenated versions as
pre-releases.

## Initial GitHub repository setup

If this source directory has no valid Git history:

```powershell
git init
git branch -M main
git add .
git commit -m "Prepare JFlightShaker C.E public beta"
git remote add origin https://github.com/OWNER/JFlightShaker-CE.git
git push -u origin main
```

Before pushing, verify:

```powershell
git status
git ls-files | Select-String "bin/|obj/|Config/bindings.json|JFlightShaker.log"
```

The second command should return no runtime files.

## GitHub settings

Enable:

- Issues.
- Private vulnerability reporting.
- Dependabot alerts and security updates.
- Automatically delete head branches.

Optionally protect `main` by requiring the **Build and test / windows** status
check before merging pull requests.

## Stable release

Remove the prerelease suffix only after testing multiple HOTAS devices:

```text
v1.0.0
```
