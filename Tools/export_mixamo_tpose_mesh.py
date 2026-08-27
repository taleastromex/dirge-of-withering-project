"""
Export a mesh-only T-pose FBX for Mixamo Auto-Rigger.

If the source has an armature, pose it to a Mixamo-like T (arms along +X/-X),
apply the armature to the mesh, then delete bones.

Usage:
  blender --factory-startup --background --python Tools/export_mixamo_tpose_mesh.py -- \
    --src <fbx> --out <out.fbx> [--max-faces 40000] [--no-tpose]
"""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Euler, Vector


def clear_scene() -> None:
	bpy.ops.wm.read_factory_settings(use_empty=True)


def import_src(path: Path) -> None:
	suffix = path.suffix.lower()
	if suffix == ".fbx":
		bpy.ops.import_scene.fbx(
			filepath=str(path),
			automatic_bone_orientation=True,
			ignore_leaf_bones=False,
			use_anim=False,
		)
	elif suffix in {".glb", ".gltf"}:
		bpy.ops.import_scene.gltf(filepath=str(path))
	else:
		raise RuntimeError(f"Unsupported: {path}")


def find_armature() -> bpy.types.Object | None:
	arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
	return arms[0] if arms else None


def bone_leaf(name: str) -> str:
	return name.split(":")[-1].lower().replace(" ", "")


def apply_tpose(arm: bpy.types.Object) -> None:
	"""Approximate Mixamo T: arms horizontal, palms down. Rest pose first."""
	bpy.ops.object.select_all(action="DESELECT")
	arm.select_set(True)
	bpy.context.view_layer.objects.active = arm
	bpy.ops.object.mode_set(mode="POSE")
	bpy.ops.pose.select_all(action="SELECT")
	bpy.ops.pose.transforms_clear()

	# If rest is already T-ish, leave it. Otherwise swing arm bones to world +X/-X.
	bpy.context.view_layer.update()
	for pb in arm.pose.bones:
		leaf = bone_leaf(pb.name)
		is_left = leaf.startswith("l_") or "left" in leaf
		is_right = leaf.startswith("r_") or "right" in leaf
		is_upper = any(k in leaf for k in ("arm", "uparm", "upperarm", "shoulder")) and "fore" not in leaf and "hand" not in leaf
		if not is_upper or not (is_left or is_right):
			continue
		# World-space aim: left arm +X, right arm -X (Mixamo T).
		head = arm.matrix_world @ pb.head
		tail = arm.matrix_world @ pb.tail
		current = (tail - head)
		if current.length < 1e-5:
			continue
		target_dir = Vector((1.0 if is_left else -1.0, 0.0, 0.0))
		# Only correct if currently more vertical than horizontal.
		if abs(current.z) > abs(current.x) + 0.05:
			rot = current.normalized().rotation_difference(target_dir)
			pb.matrix = (rot.to_matrix().to_4x4() @ pb.matrix)

	bpy.ops.object.mode_set(mode="OBJECT")
	bpy.context.view_layer.update()


def apply_armature_and_delete(arm: bpy.types.Object) -> None:
	meshes = [o for o in bpy.data.objects if o.type == "MESH"]
	for mesh in meshes:
		bpy.ops.object.select_all(action="DESELECT")
		mesh.select_set(True)
		bpy.context.view_layer.objects.active = mesh
		# Visual pose -> mesh.
		for mod in list(mesh.modifiers):
			if mod.type == "ARMATURE":
				try:
					bpy.ops.object.modifier_apply(modifier=mod.name)
				except Exception:
					mesh.modifiers.remove(mod)
		# Orphan vertex groups.
		if mesh.vertex_groups:
			mesh.vertex_groups.clear()

	bpy.ops.object.select_all(action="DESELECT")
	arm.select_set(True)
	bpy.context.view_layer.objects.active = arm
	bpy.ops.object.delete()


def join_meshes() -> bpy.types.Object | None:
	meshes = [o for o in bpy.data.objects if o.type == "MESH"]
	if not meshes:
		return None
	bpy.ops.object.select_all(action="DESELECT")
	for m in meshes:
		m.select_set(True)
	bpy.context.view_layer.objects.active = meshes[0]
	if len(meshes) > 1:
		bpy.ops.object.join()
	joined = bpy.context.view_layer.objects.active
	bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
	return joined


def decimate_if_needed(obj: bpy.types.Object, max_faces: int) -> None:
	faces = len(obj.data.polygons)
	if max_faces <= 0 or faces <= max_faces:
		print(f"faces {faces} (no decimate)")
		return
	ratio = max_faces / float(faces)
	mod = obj.modifiers.new(name="MixamoDecimate", type="DECIMATE")
	mod.ratio = ratio
	bpy.ops.object.select_all(action="DESELECT")
	obj.select_set(True)
	bpy.context.view_layer.objects.active = obj
	bpy.ops.object.modifier_apply(modifier=mod.name)
	print(f"decimate {faces} -> {len(obj.data.polygons)} ratio={ratio:.3f}")


def export_fbx(path: Path) -> None:
	path.parent.mkdir(parents=True, exist_ok=True)
	bpy.ops.export_scene.fbx(
		filepath=str(path),
		use_selection=False,
		object_types={"MESH"},
		apply_scale_options="FBX_SCALE_ALL",
		axis_forward="-Z",
		axis_up="Y",
		add_leaf_bones=False,
		bake_anim=False,
		use_mesh_modifiers=True,
		path_mode="COPY",
		embed_textures=True,
	)


def main(argv: list[str]) -> int:
	parser = argparse.ArgumentParser()
	parser.add_argument("--src", required=True)
	parser.add_argument("--out", required=True)
	parser.add_argument("--max-faces", type=int, default=40000)
	parser.add_argument("--no-tpose", action="store_true")
	args = parser.parse_args(argv)

	src = Path(args.src)
	out = Path(args.out)
	clear_scene()
	import_src(src)
	arm = find_armature()
	print(f"imported armature={'yes '+arm.name if arm else 'none'} meshes={len([o for o in bpy.data.objects if o.type=='MESH'])}")
	if arm is not None:
		if not args.no_tpose:
			apply_tpose(arm)
		apply_armature_and_delete(arm)
	joined = join_meshes()
	if joined is None:
		raise RuntimeError("No mesh to export")
	decimate_if_needed(joined, args.max_faces)
	# Mixamo likes origin at feet.
	min_z = min((joined.matrix_world @ Vector(c)).z for c in joined.bound_box)
	joined.location.z -= min_z
	bpy.context.view_layer.update()
	bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
	export_fbx(out)
	print("WROTE", out, "mb", out.stat().st_size / 1024 / 1024, "faces", len(joined.data.polygons))
	return 0


if __name__ == "__main__":
	argv = sys.argv
	argv = argv[argv.index("--") + 1 :] if "--" in argv else []
	raise SystemExit(main(argv))
