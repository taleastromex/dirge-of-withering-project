# Cathedral Slice (location art)

Игровой файл: [`Processed/CathedralSliceArt.glb`](Processed/CathedralSliceArt.glb)  
Сборка: `Tools/prepare_cathedral_slice_assets.sh` → `Tools/build_cathedral_slice_art.py` (Blender).

Сырые загрузки Sketchfab лежат в `Source/` (в git не коммитятся).  
В `Source/` лежит `.gdignore` — **не убирай**: иначе Godot начнёт импортировать сырые FBX/ZIP и зальёт Output тысячами ошибок.

## Использовано в `CathedralSliceArt.glb`

### Free Modular Dungeon Assets
- **Role:** пол, стены, апсида (`ApseAltar` / `ApseRuin_*` / `ApsePillar_*`), choke-завалы (`RubbleArt_*` из Cliff/Stone cube)
- **Sketchfab slug:** `free-modular-dungeon-assets` (поиск по имени на Sketchfab)
- **License:** как указано на странице модели (обычно CC Attribution / CC0 — сверить при обновлении)
- **Source folder:** `Source/free-modular-dungeon-assets/`

### Altar for Diana
- **Role:** алтарь в апсиде (`ApseAltar`)
- **Sketchfab slug:** `altar-for-diana` (или имя на странице модели)
- **License:** как указано на странице модели
- **Source folder:** `Source/altar-for-diana/`

### Altar Ruins
- **Role:** скачан, **не используется** в срезе (меш креста с парящим мусором от платформы)
- **Sketchfab slug:** `altar-ruins`
- **License:** как указано на странице модели (Free / Attribution — сверить)
- **Source folder:** `Source/altar-ruins/`

### Free Angels Statues (retopoed)
- **Role:** скачан, но не используется в срезе (меш-группа сплющивается в слоте Gate)
- **Sketchfab slug:** `free-angels-statues-retopoed-kinda`
- **License:** как указано на странице модели
- **Source folder:** `Source/free-angels-statues-retopoed-kinda/`

### Greek Pillar
- **Role:** 4 колонны по периметру нефа (`PerimeterColumn_A`–`D`, места PillarA–D)
- **Sketchfab slug:** `greek-pillar`
- **License:** как указано на странице модели
- **Source folder:** `Source/greek-pillar/` (в Processed конвертируется assimp → `.glb`)

### Broken and Overgrown Cemetery Figure
- **Role:** статуи у обоих пилонов входа (`PerimeterStatue_GateL` / `GateR`)
- **Sketchfab slug:** `broken-and-overgrown-cemetery-figure`
- **License:** как указано на странице модели
- **Source folder:** `Source/broken-and-overgrown-cemetery-figure/`

### Lantern (flashlight kit)
- **Role:** practicals у источников света (`HangingLantern_Mid` / `_Apse` / `_Altar`)
- **Source folder:** `Source/lantern/` (`SM_Flashlight.fbx`)
- **License:** как указано на странице модели

### Two Piles of Construction Rubble
- **Role:** скачан, **не используется** (меш отображался некорректно)
- **File:** `Source/two_piles_of_construction_rubble.glb`
- **License:** как указано на странице модели

## Скачано, но не вошло в финальный GLB

Оставлены в `Source/` на будущее / отбракованы по стилю, весу или масштабу:

| Folder | Причина |
|--------|---------|
| `ancient-stone-gate-ruin-moss-covered` | ~960k verts (Tripo), слишком тяжело для среза |
| `dark-scene-diorama` | слишком мелкая/шумная сцена, не модульная |
| `greek-pillar` | DAE/текстуры; колонны взяты из dungeon pack |
| `two_piles_of_construction_rubble.glb` | ~42 MB |
| `energy-archway` | sci-fi вид |
| `concrete-wall` | современный бетон |
| `cloitre-puy-en-velay` | тяжёлый скан |
| `broken-and-overgrown-cemetery-figure` | тяжёлые 4K текстуры |

При коммерческом релизе перепроверь лицензии каждой модели на Sketchfab и дополни атрибуцию здесь точными URL/авторами.
