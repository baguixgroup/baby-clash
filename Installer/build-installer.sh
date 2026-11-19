#!/bin/bash
# Packages Builds/Windows (the Unity player from "Baby Clash -> Build Windows App") into a single setup .exe:
#   Builds/Installer/BabyClash-<version>-Setup.exe
# Runs NSIS in a Docker container, so it works the same on macOS, Linux and Windows (Git Bash / WSL).
# Usage: Installer/build-installer.sh [path/to/Windows/build]
set -euo pipefail

PROJECT="$(cd "$(dirname "$0")/.." && pwd)"
BUILD_DIR="${1:-$PROJECT/Builds/Windows}"
BUILD_DIR="$(cd "$BUILD_DIR" && pwd)"
IMAGE=babyclash-installer-tools

if [[ ! -f "$BUILD_DIR/BabyClash.exe" ]]; then
    echo "No Windows build in $BUILD_DIR. Build it first: Baby Clash -> Build Windows App." >&2
    exit 1
fi
case "$BUILD_DIR" in
    "$PROJECT"/*) ;;
    *) echo "The build folder must be inside the project ($PROJECT)." >&2; exit 1 ;;
esac

VERSION="$(sed -n 's/^  bundleVersion: *//p' "$PROJECT/ProjectSettings/ProjectSettings.asset" | tr -d '\r')"
VERSION="${VERSION:-1.0.0}"
IFS=. read -r V1 V2 V3 V4 <<< "$VERSION"
VI_VERSION="${V1:-0}.${V2:-0}.${V3:-0}.${V4:-0}"

echo "Packaging Baby Clash $VERSION from ${BUILD_DIR#$PROJECT/}"
docker build --quiet -t "$IMAGE" "$PROJECT/Installer" > /dev/null

REL_BUILD="${BUILD_DIR#$PROJECT/}"
docker run --rm -v "$PROJECT:/project" "$IMAGE" bash -euo pipefail -c '
    ART=/tmp/art
    bash Installer/make-art.sh Assets/BabyClash/Generated/Icons "$ART"

    # Uninstall list: every top-level item of the build, as Setup copies it.
    for entry in "/project/'"$REL_BUILD"'"/*; do
        name="$(basename "$entry")"
        case "$name" in *_DoNotShip|*_ButDontShipItWithYourGame) continue ;; esac
        if [[ -d "$entry" ]]; then echo "RMDir /r \"\$INSTDIR\\$name\""; else echo "Delete \"\$INSTDIR\\$name\""; fi
    done > "$ART/uninstall-files.nsh"

    mkdir -p /project/Builds/Installer
    makensis -V2 \
        -DAPP_VERSION='"$VERSION"' -DVI_VERSION='"$VI_VERSION"' \
        -DBUILD_DIR="/project/'"$REL_BUILD"'" -DART_DIR="$ART" \
        -DOUT_FILE="/project/Builds/Installer/BabyClash-'"$VERSION"'-Setup.exe" \
        Installer/BabyClash.nsi
'

OUT="$PROJECT/Builds/Installer/BabyClash-$VERSION-Setup.exe"
echo "Installer: ${OUT#$PROJECT/} ($(du -h "$OUT" | cut -f1))"
