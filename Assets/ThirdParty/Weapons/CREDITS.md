# Weapons

## Socket convention (Mixamo)
Held props for humanoids should be **socket-authored**:
1. Pose relative to `mixamorig:RightHand` (or LeftHand).
2. Bake that transform into the GLB (`Tools/bake_weapon_hand_socket.py`).
3. Equip via `WeaponEquipData` with `SocketAuthored = true` and identity local xform.
4. Runtime only parents to the bone — same pattern as TES / Gothic / most RPGs.

Inventory later: swap `WeaponEquipData` through `WeaponAttach3D.Equip(...)`.

## Zweihander
- **File:** `zweihander.glb`
- **Role:** Blood Knight / Cursed Knight (still legacy grip on `WeaponAttach3D` / `PlayerWeaponAttach` until socket-baked)
- **Note:** Copied from `CathedralSlice/Source/` (`.gdignored`).
- **License:** as specified by the asset source (verify before commercial release)

## One-handed axe
- **Raw:** `Assets/ThirdParty/CathedralSlice/Source/one-handed-axe/source/Little_Axe.obj` + `textures/`
- **Processed:** `one-handed-axe/Processed/BanditAxe.glb` — handle along +X, grip at origin (~0.85 m)
- **Equip def:** `res://Assets/Combat/Weapons/BanditAxe_Equip.tres` (legacy grip offsets until a correct socket bake)
- **License:** as specified by the asset source (verify before commercial release)
