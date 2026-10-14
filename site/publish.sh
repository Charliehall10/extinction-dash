#!/usr/bin/env bash
# Publish the case study and the playable game to GitHub Pages (the gh-pages branch).
#   bash site/publish.sh             publish
#   bash site/publish.sh --preview   assemble into site/dist only, to check locally
# Layout: / is the case study (site/index.html + site/media), /play/ is the WebGL build.
# Build the WebGL version first (see README). History is kept; nothing is force-pushed.
set -euo pipefail
cd "$(dirname "$0")/.."
[ -f Builds/WebGL/index.html ] || { echo "Build the WebGL version first (see README)."; exit 1; }

DIST=site/dist
rm -rf "$DIST"
mkdir -p "$DIST/play"
cp -r site/index.html site/media "$DIST/"
cp -r Builds/WebGL/. "$DIST/play/"
touch "$DIST/.nojekyll"
if [ "${1:-}" = "--preview" ]; then
  echo "Assembled $DIST. Preview with: cd $DIST && python3 -m http.server 8000"
  exit 0
fi

WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT
git clone -q --branch gh-pages --single-branch "$(git remote get-url origin)" "$WORK/pages"
find "$WORK/pages" -mindepth 1 -maxdepth 1 ! -name .git -exec rm -rf {} +
cp -r "$DIST/." "$WORK/pages/"
git -C "$WORK/pages" add -A
if git -C "$WORK/pages" diff --cached --quiet; then
  echo "No changes to publish."
  exit 0
fi
git -C "$WORK/pages" -c user.name="$(git config user.name)" -c user.email="$(git config user.email)" \
  commit -q -m "Publish case study and game ($(date +%F))"
git -C "$WORK/pages" push -q origin gh-pages
echo "Published: https://charliehall10.github.io/extinction-dash/  (can take a minute or two to update)"
