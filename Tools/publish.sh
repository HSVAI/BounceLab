#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "$0")/.."
apk=Builds/Android/BounceLab.apk
webgl=Builds/WebGL
publish_dir=docs
test -s "$apk"
test -s "$webgl/index.html"
/home/ghtnql/Android/Sdk/build-tools/34.0.0/apksigner verify "$apk"
mkdir -p "$publish_dir"
mkdir -p "$publish_dir/play"
mkdir -p "$publish_dir/admin"
install -m 644 "$apk" "$publish_dir/BounceLab-0.3.2.apk"
cp -a "$webgl/." "$publish_dir/play/"
find "$publish_dir/play" -type f -exec chmod 644 {} +
install -m 644 Distribution/index.html "$publish_dir/index.html"
install -m 644 Distribution/admin/index.html "$publish_dir/admin/index.html"
install -m 644 Builds/QA/02-play.png "$publish_dir/preview-game.png"
install -m 644 Builds/QA/04-editor.png "$publish_dir/preview-editor.png"
install -m 644 Builds/QA/06-community.png "$publish_dir/preview-community.png"
cd "$publish_dir"
sha256sum BounceLab-0.3.2.apk > SHA256SUMS.txt
echo "Published test artifacts to $publish_dir"
