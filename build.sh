#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "$0")"
unity_editor="${UNITY_EDITOR:-/opt/Unity/2022.3.62f3/Unity}"
case "${1:-webgl}" in
  ios) method=ExportIOS; target=iOS ;;
  android) method=BuildAndroid; target=Android ;;
  linux) method=BuildLinux; target=Linux64 ;;
  webgl) method=BuildWebGL; target=WebGL ;;
  test) method=VerifyRules; target=Linux64 ;;
  *) echo 'Usage: ./build.sh [webgl|ios|android|linux|test]' >&2; exit 2 ;;
esac
mkdir -p Logs
"$unity_editor" -batchmode -nographics -quit -projectPath "$PWD" \
  -buildTarget "$target" -executeMethod "BounceLab.Editor.BuildBounceLab.$method" \
  -logFile "$PWD/Logs/$method.log"
