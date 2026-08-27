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
- **Role:** Blood Knight / Cursed Knight (legacy grip on `PlayerWeaponAttach` / GripTune)
- **License:** as specified by the asset source (verify before commercial release)

## One-handed axe
- **Raw:** `Assets/ThirdParty/CathedralSlice/Source/one-handed-axe/`
- **Processed:** `one-handed-axe/Processed/BanditAxe.glb` — handle along +X, grip at origin (~0.85 m)
- **Equip def:** `res://Assets/Combat/Weapons/BanditAxe_Equip.tres`
- **License:** as specified by the asset source (verify before commercial release)

## One-handed sword
- **Raw:** `CathedralSlice/Source/sword-one-handed-01/`
- **Processed:** `one-handed-sword/Processed/OneHandedSword.glb`
- **Equip def:** `res://Assets/Combat/Weapons/OneHandedSword_Equip.tres`
- **Anims:** Mixamo → BloodKnight clips `1h_*`

## Removed (Aug 2026 cleanup)
Katana / Norman sword Processed + Source packs — not in PlayerLoadout; deleted to save space. Re-download from Sketchfab if needed later.
