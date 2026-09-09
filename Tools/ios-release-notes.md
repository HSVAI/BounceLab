## Bounce Lab iOS build proof

This prerelease demonstrates compilation from a Linux-hosted Unity project to a physical-device ARM64 iOS application on a GitHub-hosted Mac.

- `BounceLab-unsigned.ipa`: real compiled iOS app in a Payload archive, **UNSIGNED**. It cannot be directly installed or uploaded to TestFlight/App Store.
- `BounceLab-Xcode.tar.gz`: Unity-exported Xcode project, including `SOURCE_COMMIT.txt`, for reproducing the build and performing a later signed archive.
- `SHA256SUMS.txt`: IPA checksum.
- `BUILD-INFO.txt`: source commit, export checksum, Xcode/SDK details, and validation scope.

No device, simulator, or gameplay tests were run. Only compilation, ARM64 architecture, bundle metadata, and archive integrity were checked.

To distribute through Apple, archive/export the Xcode project with an Apple Developer team and matching signing certificate/provisioning profile. Apple account setup, signing, TestFlight upload, and App Store submission are outside this build proof.
