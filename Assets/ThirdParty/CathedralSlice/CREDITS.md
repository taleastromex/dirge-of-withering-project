# Cathedral Slice (location art)

Игровой файл: [`Processed/CathedralSliceArt.glb`](Processed/CathedralSliceArt.glb)  
Сборка: `Tools/prepare_cathedral_slice_assets.sh` → `Tools/build_cathedral_slice_art.py` (Blender).

Сырые загрузки Sketchfab лежат в `Source/` (в git не коммитятся).  
В `Source/` лежит `.gdignore` — **не убирай**: иначе Godot начнёт импортировать сырые FBX/ZIP и зальёт Output тысячами ошибок.

## Использовано в `CathedralSliceArt.glb`

### Free Modular Dungeon Assets
- **Role:** пол, стены, апсида (`ApseAltar` / `ApseRuin_*` / `ApsePillar_*`), choke-завалы (`RubbleArt_*` из Cliff/Stone cube)
- **Sketchfab slug:** `free-modular-dungeon-assets`
- **Source folder:** `Source/free-modular-dungeon-assets/`

### Altar for Diana
- **Role:** алтарь в апсиде (`ApseAltar`)
- **Sketchfab slug:** `altar-for-diana`
- **Source folder:** `Source/altar-for-diana/`

### Greek Pillar
- **Role:** 4 колонны по периметру нефа (`PerimeterColumn_A`–`D`)
- **Sketchfab slug:** `greek-pillar`
- **Source folder:** `Source/greek-pillar/` (в Processed конвертируется assimp → `.glb`)

### Broken and Overgrown Cemetery Figure
- **Role:** статуи у обоих пилонов входа (`PerimeterStatue_GateL` / `GateR`)
- **Sketchfab slug:** `broken-and-overgrown-cemetery-figure`
- **Source folder:** `Source/broken-and-overgrown-cemetery-figure/`

### Lantern (flashlight kit)
- **Role:** practicals у источников света (`HangingLantern_Mid` / `_Apse` / `_Altar`)
- **Source folder:** `Source/lantern/` (`SM_Flashlight.fbx`)

### Player / weapons rebuild inputs (also under Source/)
- `knight-of-the-blood-order/`, `Great Sword Pack/`, `Mixamo/Player/`, `HumanHostiles/Bandit/`, `one-handed-axe/`, `sword-one-handed-01/`
- In-game zweihander: `Assets/ThirdParty/Weapons/zweihander.glb`

## Удалено из Source (Aug 2026 cleanup)

Неиспользуемые / отбракованные паки убраны локально, чтобы не занимать диск:  
`altar-ruins`, `free-angels-statues-retopoed-kinda`, `katana`, `2-handed-norman-sword`, armor packs, `death-samurai`, `cultist-tpose`, `dark-scene-diorama`, `energy-archway`, `concrete-wall`, `cloitre-puy-en-velay`, `ancient-stone-gate-ruin-moss-covered`, rubble GLB, дубли death FBX в корне `HumanHostiles/`.

При коммерческом релизе перепроверь лицензии моделей на Sketchfab.
