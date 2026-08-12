#!/usr/bin/env python3
"""
Bake a one-hand weapon into Mixamo RightHand socket space.

Result: when parented to mixamorig:RightHand with IDENTITY local transform,
the prop matches the previous runtime WeaponAttach3D pose (grip + slide + pre-rot).

Industry pattern (TES / Gothic / etc.): author once in socket space, equip = parent only.

Usage:
  blender --factory-startup --background --python Tools/bake_weapon_hand_socket.py
"""

from __future__ import annotations

import math
import sys
from pathlib import Path

import bpy
from mathutils import Euler, Matrix, Vector

ROOT = Path(__file__).resolve().parents[1]
SRC_AXE = ROOT / "Assets/ThirdParty/Weapons/one-handed-axe/Processed/BanditAxe.glb"
# If missing processed, fall back is not needed — we re-bake from current processed.
OUT_AXE = ROOT / "Assets/ThirdParty/Weapons/one-handed-axe/Processed/BanditAxe.glb"
BACKUP = ROOT / "Assets/ThirdParty/Weapons/one-handed-axe/Processed/BanditAxe_pre_socket.glb"
# Snapshot path is optional; bake copies SRC→BACKUP only if writing a new bake.

# Bandit.tscn WeaponAttach values at time of bake (pre-socket-authored).
GRIP_LOCAL_POSITION = Vector((0.0, 0.06, -0.03))
GRIP_LOCAL_ROTATION_DEG = (225.0, 0.0, -90.0)  # Godot YXZ
MESH_PRE_ROTATION_DEG = (180.0, 0.0, 0.0)
BLADE_SLIDE_M = 0.28
SLIDE_LOCAL_AXIS = Vector((1.0, 0.0, 0.0))
TARGET_LENGTH_M = 0.85


def godot_euler_yxz(deg_xyz: tuple[float, float, float]) -> Matrix:
	x, y, z = (math.radians(d) for d in deg_xyz)
	return Euler((x, y, z), "YXZ").to_matrix().to_4x4()


def clear_scene() -> None:
	bpy.ops.wm.read_factory_settings(use_empty=True)


def longest_aabb(obj: bpy.types.Object) -> float:
	coords = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
	mins = Vector((min(v.x for v in coords), min(v.y for v in coords), min(v.z for v in coords)))
	maxs = Vector((max(v.x for v in coords), max(v.y for v in coords), max(v.z for v in coords)))
	size = maxs - mins
	return max(size.x, size.y, size.z)


def main() -> int:
	if not SRC_AXE.exists():
		print("MISSING", SRC_AXE)
		return 1

	clear_scene()
	# Backup once.
	if not BACKUP.exists():
		BACKUP.write_bytes(SRC_AXE.read_bytes())
		print("BACKUP", BACKUP)

	bpy.ops.import_scene.gltf(filepath=str(SRC_AXE))
	meshes = [o for o in bpy.data.objects if o.type == "MESH"]
	if not meshes:
		raise RuntimeError("No mesh in axe GLB")

	bpy.ops.object.select_all(action="DESELECT")
	for m in meshes:
		m.select_set(True)
	bpy.context.view_layer.objects.active = meshes[0]
	if len(meshes) > 1:
		bpy.ops.object.join()
	axe = bpy.context.view_layer.objects.active
	axe.name = "BanditAxe"

	# Flatten glTF hierarchy.
	mw = axe.matrix_world.copy()
	axe.parent = None
	axe.matrix_world = mw
	bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

	# Fit length (same as WeaponAttach TargetLengthMeters).
	length = longest_aabb(axe)
	fit = TARGET_LENGTH_M / length if length > 1e-6 else 1.0
	axe.scale = (fit, fit, fit)
	bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

	# Runtime chain was: bone * grip * (mesh with MeshPreRotation on the mesh basis).
	# Weapon node transform = grip (with slide). Mesh child basis = MeshPreRotation.
	# Equivalent single local xform on mesh under bone with identity parent:
	#   T = grip_local @ mesh_pre
	pre = godot_euler_yxz(MESH_PRE_ROTATION_DEG)
	grip_r = godot_euler_yxz(GRIP_LOCAL_ROTATION_DEG)
	axis = SLIDE_LOCAL_AXIS.normalized()
	along = grip_r.to_3x3() @ axis
	origin = GRIP_LOCAL_POSITION - along * BLADE_SLIDE_M
	grip = Matrix.Translation(origin) @ grip_r
	bake = grip @ pre

	axe.matrix_world = bake
	bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

	# Clean orphans.
	for o in list(bpy.data.objects):
		if o != axe:
			bpy.data.objects.remove(o, do_unlink=True)

	for img in bpy.data.images:
		try:
			img.pack()
		except Exception:
			pass

	OUT_AXE.parent.mkdir(parents=True, exist_ok=True)
	bpy.ops.export_scene.gltf(
		filepath=str(OUT_AXE),
		export_format="GLB",
		export_animations=False,
		export_apply=True,
		export_materials="EXPORT",
	)

	# Verify AABB contains origin near grip.
	coords = [Vector(c) for c in axe.bound_box]
	mins = Vector((min(v.x for v in coords), min(v.y for v in coords), min(v.z for v in coords)))
	maxs = Vector((max(v.x for v in coords), max(v.y for v in coords), max(v.z for v in coords)))
	print(
		"REPORT",
		{
			"out": str(OUT_AXE),
			"fit": round(fit, 4),
			"aabb_min": tuple(round(v, 4) for v in mins),
			"aabb_max": tuple(round(v, 4) for v in maxs),
			"convention": "parent to mixamorig:RightHand with identity local xform",
		},
	)
	return 0


if __name__ == "__main__":
	sys.exit(main())
