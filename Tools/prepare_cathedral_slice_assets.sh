#!/usr/bin/env bash
# Unpack Sketchfab Source/ → Processed/ kits needed by build_cathedral_slice_art.py
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BASE="$ROOT/Assets/ThirdParty/CathedralSlice"
SRC="$BASE/Source"
PROC="$BASE/Processed"
mkdir -p "$PROC"

echo "== AltarRuins =="
rm -rf "$PROC/AltarRuins" "$PROC/_tmp_altar"
mkdir -p "$PROC/AltarRuins"
unzip -o -q "$SRC/altar-ruins/source/Unity2Skfb.zip" -d "$PROC/_tmp_altar"
mv "$PROC/_tmp_altar/Unity2Skfb.gltf" "$PROC/AltarRuins/AltarRuins.gltf"
mv "$PROC/_tmp_altar/Unity2Skfb.bin" "$PROC/AltarRuins/AltarRuins.bin"
mv "$PROC/_tmp_altar/Assets" "$PROC/AltarRuins/Assets"
python3 - <<PY
from pathlib import Path
p = Path("$PROC/AltarRuins/AltarRuins.gltf")
p.write_text(p.read_text().replace("Unity2Skfb.bin", "AltarRuins.bin"))
PY
rm -rf "$PROC/_tmp_altar"

echo "== ModularDungeon =="
rm -rf "$PROC/ModularDungeon"
mkdir -p "$PROC/ModularDungeon/textures"
cp "$SRC/free-modular-dungeon-assets/source/Dungeon assets.fbx" "$PROC/ModularDungeon/ModularDungeon.fbx"
cp -R "$SRC/free-modular-dungeon-assets/textures/"* "$PROC/ModularDungeon/textures/"

echo "== AngelStatues =="
rm -rf "$PROC/AngelStatues"
mkdir -p "$PROC/AngelStatues/textures"
cp "$SRC/free-angels-statues-retopoed-kinda/source/Angels.fbx" "$PROC/AngelStatues/Angels.fbx"
cp "$SRC/free-angels-statues-retopoed-kinda/textures/"Angels_*.png "$PROC/AngelStatues/textures/"

echo "== GreekPillar =="
rm -rf "$PROC/GreekPillar" "$PROC/_tmp_pillar"
mkdir -p "$PROC/GreekPillar"
unzip -o -q "$SRC/greek-pillar/source/model.zip" -d "$PROC/_tmp_pillar"
mv "$PROC/_tmp_pillar/model/"* "$PROC/GreekPillar/"
mv "$PROC/GreekPillar/model.dae" "$PROC/GreekPillar/GreekPillar.dae"
rm -rf "$PROC/_tmp_pillar"
# Blender 5 has no Collada op — convert via assimp for FBX/GLB import.
if command -v assimp >/dev/null 2>&1; then
  (cd "$PROC/GreekPillar" && assimp export GreekPillar.dae GreekPillar.glb >/dev/null)
  echo "GreekPillar.glb via assimp"
else
  echo "WARN: assimp not found; GreekPillar.glb missing"
fi

echo "== CemeteryFigure =="
rm -rf "$PROC/CemeteryFigure"
mkdir -p "$PROC/CemeteryFigure/textures"
cp "$SRC/broken-and-overgrown-cemetery-figure/source/figure_midpoly.fbx" "$PROC/CemeteryFigure/CemeteryFigure.fbx"
# Prefer smaller maps if present; copy all and resize step will shrink.
cp "$SRC/broken-and-overgrown-cemetery-figure/textures/"* "$PROC/CemeteryFigure/textures/" 2>/dev/null || true

echo "== Lantern (flashlight kit) =="
rm -rf "$PROC/Lantern"
mkdir -p "$PROC/Lantern/textures"
cp "$SRC/lantern/source/SM_Flashlight.fbx" "$PROC/Lantern/Lantern.fbx"
cp "$SRC/lantern/textures/"*.png "$PROC/Lantern/textures/" 2>/dev/null || true

echo "== AltarDiana =="
rm -rf "$PROC/AltarDiana" "$PROC/_tmp_diana"
mkdir -p "$PROC/AltarDiana"
unzip -o -q "$SRC/altar-for-diana/source/altar-1.zip" -d "$PROC/_tmp_diana"
mv "$PROC/_tmp_diana/altar.obj" "$PROC/AltarDiana/AltarDiana.obj"
mv "$PROC/_tmp_diana/altar.mtl" "$PROC/AltarDiana/AltarDiana.mtl"
# Prefer Sketchfab textures/ jpeg if present; zip ships altar01.jpg
if [[ -f "$SRC/altar-for-diana/textures/altar01.jpeg" ]]; then
  cp "$SRC/altar-for-diana/textures/altar01.jpeg" "$PROC/AltarDiana/altar01.jpg"
elif [[ -f "$PROC/_tmp_diana/altar01.jpg" ]]; then
  mv "$PROC/_tmp_diana/altar01.jpg" "$PROC/AltarDiana/altar01.jpg"
fi
# Fix mtl map reference / material file name
python3 - <<PY
from pathlib import Path
mtl = Path("$PROC/AltarDiana/AltarDiana.mtl")
text = mtl.read_text(errors="ignore")
lines = []
for line in text.splitlines():
    if line.lower().startswith("map_"):
        parts = line.split()
        if parts:
            parts[-1] = "altar01.jpg"
            line = " ".join(parts)
    lines.append(line)
mtl.write_text("\n".join(lines) + "\n")
obj = Path("$PROC/AltarDiana/AltarDiana.obj")
obj.write_text(obj.read_text(errors="ignore").replace("altar.mtl", "AltarDiana.mtl"))
PY
rm -rf "$PROC/_tmp_diana"

echo "== Resize textures to ≤1024 =="
python3 - <<PY
import subprocess
from pathlib import Path
proc = Path("$PROC")
exts = {".png", ".jpg", ".jpeg", ".JPG", ".JPEG", ".PNG"}
for p in proc.rglob("*"):
    if p.name == "CathedralSliceArt.glb" or p.suffix.lower() == ".glb":
        continue
    if p.suffix not in exts or p.stat().st_size < 50_000:
        continue
    out = subprocess.check_output(["sips", "-g", "pixelWidth", "-g", "pixelHeight", str(p)], text=True)
    w = h = 0
    for line in out.splitlines():
        if "pixelWidth" in line:
            w = int(line.split()[-1])
        if "pixelHeight" in line:
            h = int(line.split()[-1])
    if max(w, h) > 1024:
        subprocess.check_call(["sips", "-Z", "1024", str(p)], stdout=subprocess.DEVNULL)
        print("resized", p.name)
PY

echo "Ready. Next: blender -b --factory-startup --python Tools/build_cathedral_slice_art.py"
