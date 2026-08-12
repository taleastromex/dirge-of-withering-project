# Human hostiles / near-human packs

Игровые файлы: `Processed/`.
Исходники Mixamo / Sketchfab: `Assets/ThirdParty/CathedralSlice/Source/HumanHostiles/` (`.gdignore`).

## Bandit.glb
- **Source:** Adobe Mixamo — character *Ch05_nonPBR* + standing melee / locomotion + death set
- **Clips:** `idle`, `walk`, `run`, `attack`, `attack_alt`, `stagger`, `death` (Dying), `death_alt` (Falling Back Death), `death_flyback` (Flying Back Death)
- **Death rules:** `death` / `death_alt` random; `death_flyback` only when hit is heavy (≥75 dmg or ≥50% MaxHP) / burst, or `BasicEnemy.ArmExplosiveDeath()`
- **Weapon:** `Weapons/one-handed-axe/Processed/BanditAxe.glb` via `WeaponAttach3D`
- Follow Adobe Mixamo terms for game use.

Rebuild:
```bash
blender --factory-startup --background --python Tools/merge_mixamo_anims.py -- \
  --base "Assets/ThirdParty/CathedralSlice/Source/HumanHostiles/Bandit/Ch05_nonPBR.fbx" \
  --anims-dir "Assets/ThirdParty/CathedralSlice/Source/HumanHostiles/Bandit" \
  --out "Assets/ThirdParty/HumanHostiles/Processed/Bandit.glb" \
  --preset bandit \
  --strip-root-motion
```

## Cursed Knight
- **Raw:** Sketchfab / pack *Brother Knight* — custom Unreal-style skeleton (`spine_*_jnt`, etc.), **not** Mixamo. No combat animation set usable with our `EnemyAnimDriver` clips.
- **Processed/CursedKnight_BrotherMesh.glb:** mesh+textures reference export (no gameplay anims).
- **Slice stand-in:** `Scenes/Enemy/CursedKnight.tscn` currently instances `BloodKnight.glb` with a cursed ash tint until Brother Knight is Mixamo Auto-Rigged + animation pack merged (same workflow as Blood Knight CREDITS).

When Mixamo-ready FBX + clips (`idle` / `walk|run` / `attack` / `stagger` / `death`) are available, merge into `Processed/CursedKnight.glb` and point the scene at it.
