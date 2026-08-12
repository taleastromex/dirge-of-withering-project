# Blighted Wretch assets

Игровые файлы лежат в `Processed/`. Исходные Mixamo FBX из репозитория убраны.

## MutantBrute.glb
- **Source:** Adobe Mixamo — character *Parasite L Starkie* + mutant animation set
- Follow Adobe Mixamo terms for game use.

## ZombieThrall.glb
- **Source:** Adobe Mixamo — character *Ch10_nonPBR* + zombie animation set
- Textures downscaled to **1024** for the Vertical Slice

## Processed clips (logical names)
`idle`, `walk`, `run`, `attack`, `stagger`, `death`, `death_alt`, `death_flyback`

Shared Mixamo deaths (`Dying` / `Falling Back Death` / `Flying Back Death`) append via:
```bash
blender --factory-startup --background --python Tools/merge_mixamo_anims.py -- \
  --base "Assets/ThirdParty/BlightedWretch/Processed/ZombieThrall.glb" \
  --anims-dir "Assets/ThirdParty/CathedralSlice/Source/HumanHostiles/Bandit" \
  --out "Assets/ThirdParty/BlightedWretch/Processed/ZombieThrall.glb" \
  --preset mixamo_deaths --append --strip-root-motion
```
(same for `MutantBrute.glb`). Runtime: any `BasicEnemy` with those clip names gets random death / explosive flyback.
