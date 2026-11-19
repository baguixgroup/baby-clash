#!/bin/bash
# Draws the installer's branding with ImageMagick (runs inside the toolchain container):
#   wizard.bmp   164x314  Welcome / Finish sidebar: puzzle-mat tiles, a cream card with the baby and the logo
#   header.bmp   150x57   inner-page header: baby, diaper, baby
#   BabyClash.ico         installer and uninstaller icon
# Usage: make-art.sh <icons dir> <out dir>
set -euo pipefail

ICONS="$1"
OUT="$2"
FONT=/usr/share/fonts/truetype/lilita/LilitaOne-Regular.ttf
INK="#4A2D5C"
CARD="#FFF8EC"
MAT=("#F4B6C8" "#F5E08E" "#A9DDB4" "#A9C8EA")
WORK="$(mktemp -d)"
mkdir -p "$OUT"

# One word of the logo: each letter in its own colour, outlined in ink, outline painted under the fill.
logo_word() {
    local word="$1" out="$2"; shift 2
    local colors=("$@") parts=() i
    for ((i = 0; i < ${#word}; i++)); do
        convert -size 40x44 xc:none -font "$FONT" -pointsize 31 -gravity center \
            -fill "$INK" -stroke "$INK" -strokewidth 5 -annotate +0+0 "${word:i:1}" \
            -fill "${colors[i]}" -stroke none -annotate +0+0 "${word:i:1}" \
            -trim +repage "$WORK/letter$i.png"
        parts+=("$WORK/letter$i.png")
    done
    convert -background none -gravity south "${parts[@]}" +append "$out"
}

# --- Sidebar ---------------------------------------------------------------------------------------------------
draw=()
for ((r = 0; r < 8; r++)); do
    for ((c = 0; c < 4; c++)); do
        x=$((c * 41)); y=$((r * 41))
        draw+=(-fill "${MAT[$(((c + r * 3) % 4))]}" -draw "rectangle $x,$y $((x + 40)),$((y + 40))")
    done
done
convert -size 164x314 xc:"#FBEFF3" "${draw[@]}" "$WORK/mat.png"

# Baby face in a peach circle.
convert -size 75x75 xc:none -fill "#FFE6D2" -draw "circle 37,37 37,0" "$WORK/disc.png"
convert "$ICONS/IconBaby1.png" -resize 75x75 "$WORK/baby75.png"
convert "$WORK/disc.png" "$WORK/baby75.png" -composite \
    \( -size 75x75 xc:none -fill white -draw "circle 37,37 37,0" \) -compose DstIn -composite "$WORK/face.png"

logo_word "BABY" "$WORK/baby.png" "#F07FA6" "#F5C518" "#4A90D9" "#4CC27A"
logo_word "CLASH" "$WORK/clash.png" "#A77FE0" "#F07FA6" "#F5C518" "#4A90D9" "#4CC27A"

convert "$WORK/mat.png" \
    -fill "rgba(74,45,92,0.25)" -draw "roundrectangle 15,56 148,240 17,17" \
    -fill "$CARD" -stroke "$INK" -strokewidth 2 -draw "roundrectangle 15,52 148,236 17,17" -stroke none \
    "$WORK/face.png" -gravity north -geometry +0+66 -composite \
    "$WORK/baby.png" -gravity north -geometry +0+146 -composite \
    "$WORK/clash.png" -gravity north -geometry +0+177 -composite \
    -font DejaVu-Sans -pointsize 7.5 -fill "#5E4470" -gravity north \
    -annotate +0+211 "a diaper-flinging" -annotate +0+221 "nursery showdown" \
    -flatten -alpha off -type TrueColor BMP3:"$OUT/wizard.bmp"

# --- Header ----------------------------------------------------------------------------------------------------
convert "$ICONS/IconBaby1.png" -resize 47x47 "$WORK/h1.png"
convert "$ICONS/IconDiaper.png" -resize 27x27 "$WORK/hd.png"
convert "$ICONS/IconBaby2.png" -resize 47x47 "$WORK/h2.png"
convert -background none "$WORK/h1.png" \( -size 4x1 xc:none \) "$WORK/hd.png" \( -size 4x1 xc:none \) "$WORK/h2.png" \
    -gravity center +append "$WORK/trio.png"
convert -size 150x57 xc:"$CARD" -fill "#F4B6C8" -draw "rectangle 0,0 2,56" \
    "$WORK/trio.png" -gravity center -geometry +1+0 -composite \
    -flatten -alpha off -type TrueColor BMP3:"$OUT/header.bmp"

# --- Icon ------------------------------------------------------------------------------------------------------
convert "$ICONS/IconBaby1.png" -background none -define icon:auto-resize=256,64,48,32,16 "$OUT/BabyClash.ico"

rm -rf "$WORK"
