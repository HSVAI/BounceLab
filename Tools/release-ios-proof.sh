#!/usr/bin/env bash
# Linux export uses the existing activated Unity editor. macOS does final compilation.
set -euo pipefail
cd -- "$(dirname -- "$0")/.."
git diff --quiet
git diff --cached --quiet
source_commit=$(git rev-parse HEAD)
release_tag="ios-proof-$(date -u +%Y%m%d-%H%M%S)-${source_commit:0:7}"
./build.sh ios
printf '%s\n' "$source_commit" > Builds/iOS/SOURCE_COMMIT.txt
mkdir -p Builds/iOSRelease
tar -czf Builds/iOSRelease/BounceLab-Xcode.tar.gz -C Builds/iOS .
export_sha256=$(sha256sum Builds/iOSRelease/BounceLab-Xcode.tar.gz | cut -d ' ' -f 1)
git tag "$release_tag" "$source_commit"
git push origin "$release_tag"
gh release create "$release_tag" Builds/iOSRelease/BounceLab-Xcode.tar.gz \
  --repo HSVAI/BounceLab --verify-tag --draft --prerelease \
  --title "Bounce Lab iOS build proof — unsigned IPA" \
  --notes-file Tools/ios-release-notes.md
gh workflow run ios-unsigned.yml --repo HSVAI/BounceLab --ref "$release_tag" \
  -f "export_tag=$release_tag" -f "export_sha256=$export_sha256"
printf 'Release tag: %s\nExport SHA-256: %s\n' "$release_tag" "$export_sha256"
