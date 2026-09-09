#!/usr/bin/env bash
# Run on macOS with Xcode. Input: Unity's exported Xcode project.
set -euo pipefail
export_dir="${1:?Usage: build-ios-unsigned.sh EXPORT_DIRECTORY OUTPUT_DIRECTORY}"
output_dir="${2:?Output directory required}"
test "$(uname -s)" = Darwin
export_dir="$(cd "$export_dir" && pwd)"
mkdir -p "$output_dir"
output_dir="$(cd "$output_dir" && pwd)"
test -f "$export_dir/Unity-iPhone.xcodeproj/project.pbxproj"

xcodebuild -version
xcodebuild -project "$export_dir/Unity-iPhone.xcodeproj" \
  -scheme Unity-iPhone -configuration Release -sdk iphoneos \
  -destination 'generic/platform=iOS' \
  -archivePath "$output_dir/BounceLab.xcarchive" \
  -derivedDataPath "$output_dir/DerivedData" \
  -jobs 3 archive \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY= \
  DEVELOPMENT_TEAM= ENABLE_BITCODE=NO COMPILER_INDEX_STORE_ENABLE=NO \
  2>&1 | tee "$output_dir/xcodebuild.log"

shopt -s nullglob
apps=("$output_dir/BounceLab.xcarchive/Products/Applications/"*.app)
test "${#apps[@]}" -eq 1
app_dir="${apps[0]}"
test -d "$app_dir"
app_id=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$app_dir/Info.plist")
test "$app_id" = 'com.ghtnql.bouncelab'
executable=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$app_dir/Info.plist")
lipo -verify_arch arm64 "$app_dir/$executable"
lipo -verify_arch arm64 "$app_dir/Frameworks/UnityFramework.framework/UnityFramework"
test "$(/usr/libexec/PlistBuddy -c 'Print :DTPlatformName' "$app_dir/Info.plist")" = iphoneos
test ! -e "$app_dir/embedded.mobileprovision"

package_dir="$(mktemp -d "$output_dir/package.XXXXXX")"
mkdir -p "$package_dir/Payload"
ditto "$app_dir" "$package_dir/Payload/$(basename "$app_dir")"
(cd "$package_dir" && ditto -c -k --keepParent Payload "$output_dir/BounceLab-unsigned.ipa")
unzip -tq "$output_dir/BounceLab-unsigned.ipa"
(cd "$output_dir" && shasum -a 256 BounceLab-unsigned.ipa > SHA256SUMS.txt)
{
  echo 'Bounce Lab: unsigned iOS build proof'
  echo "Source commit: ${SOURCE_COMMIT:-unknown}"
  echo "Workflow commit: ${GITHUB_SHA:-local}"
  echo "Export SHA-256: ${EXPORT_SHA256:-unknown}"
  echo "Bundle identifier: $app_id"
  echo 'Architecture: ARM64; platform: physical iOS device (not simulator)'
  echo 'Signing: NONE; not directly installable and not uploadable to TestFlight/App Store.'
  echo 'Validation: Xcode archive, executable architectures, bundle metadata and ZIP integrity only.'
  echo 'Gameplay/device/simulator tests: NOT RUN (requested scope).'
  xcodebuild -version
  xcrun --sdk iphoneos --show-sdk-version
  sw_vers
} > "$output_dir/BUILD-INFO.txt"
echo 'BOUNCELAB_UNSIGNED_IPA_OK'
