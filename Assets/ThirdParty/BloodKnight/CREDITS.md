# Blood Knight (player)

Игровой файл: `Processed/BloodKnight.glb`.

## Sources
- **Character mesh:** Sketchfab / pack *Knight of the Blood Order* (Drakul), re-rigged via Adobe Mixamo Auto-Rigger
- **Animations:** Adobe Mixamo — Great Sword pack (idle / walk / run / slash / high spin / impact / death)
- Follow Adobe Mixamo terms for game use

## Processed clips
`idle`, `walk`, `run`, `attack`, `attack_heavy`, `stagger`, `death`

## Rebuild
```bash
blender --factory-startup --background --python Tools/merge_mixamo_anims.py -- \
  --base "Assets/ThirdParty/CathedralSlice/Source/knight-of-the-blood-order/source/bloodknight_mixamo_rigged.fbx" \
  --anims-dir "Assets/ThirdParty/CathedralSlice/Source/Great Sword Pack" \
  --out "Assets/ThirdParty/BloodKnight/Processed/BloodKnight.glb" \
  --preset blood_knight \
  --strip-root-motion
```
