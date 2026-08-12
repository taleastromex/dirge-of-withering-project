# Blood Knight (player)

Игровой файл: `Processed/BloodKnight.glb`.

## Sources
- **Character mesh:** Sketchfab / pack *Knight of the Blood Order* (Drakul), re-rigged via Adobe Mixamo Auto-Rigger
- **Animations:** Adobe Mixamo — Great Sword pack (idle / walk / run / slash / high spin / impact / death)
- Follow Adobe Mixamo terms for game use

## Processed clips
`idle`, `idle_2`…`idle_5`, `walk`, `run`, `attack`, `attack_heavy`, `stagger`, `death`
(Great Sword Pack has 5 idles; switch via Player `AnimDriver.IdleClip`.)

## Textures
Source 2K PBR (`CathedralSlice/Source/knight-of-the-blood-order/textures/`):
`drakulColor`, `drakulNormal`, `drakulMetallic`, `drakulSmoothness`→roughness, `1Ambient_Occlusion`, `drakulemissive`.

## Rebuild
```bash
# Combat set + Mixamo deaths + full 2K PBR in one pass (avoid GLB round-trip)
# World-space retarget is ON by default (needed: GS pack = Y Bot proportions ≠ Blood Knight).
blender --factory-startup --background --python Tools/merge_mixamo_anims.py -- \
  --base "Assets/ThirdParty/CathedralSlice/Source/knight-of-the-blood-order/source/bloodknight_mixamo_rigged.fbx" \
  --anims-dir "Assets/ThirdParty/CathedralSlice/Source/Great Sword Pack" \
  --out "Assets/ThirdParty/BloodKnight/Processed/BloodKnight.glb" \
  --preset blood_knight \
  --strip-root-motion \
  --textures-dir "Assets/ThirdParty/CathedralSlice/Source/knight-of-the-blood-order/textures" \
  --extra-anims-dir "Assets/ThirdParty/CathedralSlice/Source/HumanHostiles" \
  --extra-preset mixamo_deaths
```
