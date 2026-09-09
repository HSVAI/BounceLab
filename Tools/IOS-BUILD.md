# Reproducible iOS build from this Linux workspace

## User's accepted scope

Build Bounce Lab (`HSVAI/BounceLab`) into an IPA and upload it to GitHub Release. Do not run the game, a simulator, or device tests. Reuse existing authentication; no routine confirmation is needed. Label an unsigned IPA honestly: it is not installable or ready for TestFlight/App Store.

## Pipeline

1. Linux Unity 2022.3.62f3 uses its existing Personal activation to export `Builds/iOS` with `./build.sh ios`.
2. Package the exported Xcode project, record its source commit and SHA-256, and upload it to a draft GitHub prerelease.
3. Manually dispatch `.github/workflows/ios-unsigned.yml` for that exact git tag. GitHub's standard `macos-15-intel` runner validates the export checksum and commit, then runs `Tools/build-ios-unsigned.sh`.
4. Xcode builds an ARM64 `iphoneos` archive with signing disabled. The script packages the compiled app as `Payload/Bounce Lab.app` in `BounceLab-unsigned.ipa` and checks architecture, bundle identifier, platform, and ZIP integrity.
5. Only a successful build attaches the IPA, checksum, and metadata and publishes the prerelease. Failed builds leave a draft with the input export; inspect Actions logs and do not report completion.

## Repeat

Commit and push source changes first, then:

```bash
cd /home/ghtnql/BounceLab
bash Tools/release-ios-proof.sh
gh run list --repo HSVAI/BounceLab --workflow ios-unsigned.yml --limit 5
gh run view RUN_ID --repo HSVAI/BounceLab --log-failed
gh release view TAG --repo HSVAI/BounceLab
```

The helper exports, packages, creates a unique tag/draft, and dispatches the Mac build. Monitor that run to completion; do not rebuild Linux/Android/WebGL or run gameplay tests for this request.

## Authentication and dependencies

- GitHub: existing `gh` authentication on this host, repository `HSVAI/BounceLab`. The workflow uses its repository-scoped `GITHUB_TOKEN` with `contents: write` for Release assets.
- Unity: existing local activation under `/home/ghtnql/.config/unity3d/Unity/licenses/`. Never copy the license, Unity account database, passwords, or GitHub tokens into the public repository or release.
- No Unity account or license secret is needed on the Mac: it receives the exported Xcode project.
- Editor: `/opt/Unity/2022.3.62f3/Unity`, revision `96770f904ca7`.
- iOS module: `https://download.unity3d.com/download_unity/96770f904ca7/LinuxEditorTargetInstaller/UnitySetup-iOS-Support-for-Editor-2022.3.62f3.tar.xz`; official release metadata records MD5 `ba322ae18c0f5035620f849eec80beed`. Cached in `/home/ghtnql/.cache/unity-modules/`.
- Apple signing credentials were not found during initial setup. They are not required for this unsigned build proof, but are required for an installable distribution build. Do not call ZIP packaging alone an Apple-signed export.

## Scope boundaries

The standard macOS runner builds only this project's iOS code. No App Store/TestFlight upload, paid runner, account purchase, or new Apple agreement is part of this workflow. The WebGL game and GameLibrary already have their own deployment.
