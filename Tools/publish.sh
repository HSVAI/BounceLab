#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "$0")/.."
apk=Builds/Android/BounceLab.apk
publish_dir=docs
test -s "$apk"
/home/ghtnql/Android/Sdk/build-tools/34.0.0/apksigner verify "$apk"
mkdir -p "$publish_dir"
install -m 644 "$apk" "$publish_dir/BounceLab-0.2.0.apk"
install -m 644 Distribution/index.html "$publish_dir/index.html"
install -m 644 Builds/QA/02-play.png "$publish_dir/preview-game.png"
install -m 644 Builds/QA/04-editor.png "$publish_dir/preview-editor.png"
install -m 644 Builds/QA/06-community.png "$publish_dir/preview-community.png"
cd "$publish_dir"
sha256sum BounceLab-0.2.0.apk > SHA256SUMS.txt
echo "Published test artifacts to $publish_dir"
