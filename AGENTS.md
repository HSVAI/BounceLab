# Bounce Lab working notes

- Inherit `/home/ghtnql/AGENTS.md` for workspace release preferences.
- iOS IPA build and GitHub Release procedure: read `Tools/IOS-BUILD.md` before starting. It is model-independent and records commands, authentication locations, signing limitations, and the build-only scope.
- For the iOS build proof, the user explicitly requested no gameplay/device/simulator tests and no routine confirmation. Complete compilation, IPA packaging, and Release upload; report unsigned status accurately.
- Do not build Android APK unless explicitly requested. `./build.sh` defaults to WebGL.
