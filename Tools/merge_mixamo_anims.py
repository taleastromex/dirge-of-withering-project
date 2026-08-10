"""
Merge Mixamo FBX clips onto a base character and export GLB for Godot.
Usage:
  blender --factory-startup --background --python merge_mixamo_anims.py -- \
    --base <fbx|glb> --anims-dir <dir> --out <out.glb> \
    [--scale 1.0] [--rotations-only] [--strip-root-motion]
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

import bpy


CLIP_MAPS = {
	"mutant": {
		"mutant breathing idle.fbx": "idle",
		"mutant walking.fbx": "walk",
		"mutant run.fbx": "run",
		"mutant swiping.fbx": "attack",
		"mutant punch.fbx": "attack_alt",
		"mutant jump attack.fbx": "attack_heavy",
		"mutant dying.fbx": "death",
		"mutant roaring.fbx": "stagger",
	},
	"zombie": {
		"zombie idle.fbx": "idle",
		"zombie walk.fbx": "walk",
		"zombie run.fbx": "run",
		"zombie attack.fbx": "attack",
		"zombie biting.fbx": "attack_alt",
		"zombie scream.fbx": "stagger",
		"zombie death.fbx": "death",
		"zombie dying.fbx": "death_alt",
	},
}

LOOP_CLIPS = {"idle", "walk", "run"}
HIPS_NAMES = {
	"mixamorig:Hips",
	"mixamorig5:Hips",
	"Hips",
	"mixamorig_Hips",
}


def clear_scene() -> None:
	bpy.ops.wm.read_factory_settings(use_empty=True)


def import_file(path: Path) -> None:
	suffix = path.suffix.lower()
	if suffix == ".fbx":
		bpy.ops.import_scene.fbx(
			filepath=str(path),
			automatic_bone_orientation=True,
			ignore_leaf_bones=False,
		)
	elif suffix in {".glb", ".gltf"}:
		bpy.ops.import_scene.gltf(filepath=str(path))
	else:
		raise RuntimeError(f"Unsupported format: {path}")


def find_armature() -> bpy.types.Object:
	arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
	if not arms:
		raise RuntimeError("No armature found")
	for arm in arms:
		for obj in bpy.data.objects:
			if obj.type != "MESH":
				continue
			for mod in obj.modifiers:
				if mod.type == "ARMATURE" and mod.object == arm:
					return arm
	return arms[0]


def sanitize_action_name(name: str) -> str:
	return re.sub(r"[^A-Za-z0-9_]+", "_", name).strip("_") or "clip"


def mesh_dimensions() -> tuple[float, float, float] | None:
	for obj in bpy.data.objects:
		if obj.type == "MESH":
			return tuple(round(v, 3) for v in obj.dimensions)
	return None


def apply_uniform_scale(scale: float) -> None:
	if abs(scale - 1.0) <= 1e-8:
		return
	bpy.ops.object.select_all(action="DESELECT")
	for obj in bpy.data.objects:
		obj.select_set(True)
	bpy.context.view_layer.objects.active = find_armature()
	bpy.ops.transform.resize(value=(scale, scale, scale))
	bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)


def push_action_to_nla(arm: bpy.types.Object, action: bpy.types.Action, name: str) -> None:
	if arm.animation_data is None:
		arm.animation_data_create()
	track = arm.animation_data.nla_tracks.new()
	track.name = name
	frame_start = int(action.frame_range[0])
	strip = track.strips.new(name, frame_start, action)
	strip.name = name
	arm.animation_data.action = None


def remove_objects(objs: list[bpy.types.Object]) -> None:
	bpy.ops.object.select_all(action="DESELECT")
	for obj in objs:
		if obj.name in bpy.data.objects:
			obj.select_set(True)
	if bpy.context.selected_objects:
		bpy.ops.object.delete()


def iter_action_fcurves(action: bpy.types.Action):
	if hasattr(action, "fcurves") and action.fcurves is not None:
		for fc in action.fcurves:
			yield fc
		return
	for layer in getattr(action, "layers", []) or []:
		for strip in getattr(layer, "strips", []) or []:
			for bag in getattr(strip, "channelbags", []) or []:
				for fc in bag.fcurves:
					yield fc


def remove_fcurve(action: bpy.types.Action, fcurve) -> None:
	# Blender 5 layered actions
	for layer in getattr(action, "layers", []) or []:
		for strip in getattr(layer, "strips", []) or []:
			for bag in getattr(strip, "channelbags", []) or []:
				try:
					bag.fcurves.remove(fcurve)
					return
				except Exception:
					pass
	if hasattr(action, "fcurves"):
		try:
			action.fcurves.remove(fcurve)
		except Exception:
			pass


def sanitize_action_tracks(action: bpy.types.Action, rotations_only: bool, strip_root_motion: bool) -> dict:
	removed = {"location": 0, "hips_location": 0}
	to_remove = []
	for fc in list(iter_action_fcurves(action)):
		path = fc.data_path
		m = re.search(r'pose\.bones\["([^"]+)"\]', path)
		bone = m.group(1) if m else ""
		is_location = path.endswith(".location") or path.endswith("]location")
		if not is_location and ".location" not in path:
			# also match pose.bones["X"].location
			is_location = ".location" in path

		if rotations_only and is_location:
			to_remove.append(fc)
			removed["location"] += 1
			continue

		is_hips = bone in HIPS_NAMES or bone.endswith(":Hips") or bone == "Hips"
		if strip_root_motion and is_location and is_hips:
			to_remove.append(fc)
			removed["hips_location"] += 1

	for fc in to_remove:
		remove_fcurve(action, fc)
	return removed


def mark_loop(action: bpy.types.Action) -> None:
	# Helps some exporters / NLA cyclic use.
	try:
		action.use_cyclic = True
	except Exception:
		pass


def cleanup_scene_graph(base_arm: bpy.types.Object) -> None:
	"""Drop orphan empties and bake armature scale so Godot gets one sane root."""
	orphans = []
	for obj in list(bpy.data.objects):
		if obj == base_arm:
			continue
		if obj.type == "EMPTY" and len(obj.children) == 0:
			orphans.append(obj)
			continue
		# Leftover helper roots from glTF/Sketchfab imports.
		if obj.type == "EMPTY" and obj.name.lower() in {"root", "scene", "sketchup"}:
			orphans.append(obj)
	remove_objects(orphans)

	# Apply armature object scale so rest pose is ~meters.
	bpy.ops.object.select_all(action="DESELECT")
	base_arm.select_set(True)
	bpy.context.view_layer.objects.active = base_arm
	bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

	# Parent mesh(es) stay under armature; ensure no second unparented mesh root.
	for obj in list(bpy.data.objects):
		if obj.type != "MESH":
			continue
		if obj.parent is None:
			obj.parent = base_arm


def action_bone_coverage(action: bpy.types.Action, arm: bpy.types.Object) -> dict:
	bone_names = {b.name for b in arm.data.bones}
	fcurve_bones: set[str] = set()
	for fc in iter_action_fcurves(action):
		m = re.search(r'pose\.bones\["([^"]+)"\]', fc.data_path)
		if m:
			fcurve_bones.add(m.group(1))
	missing = sorted(fcurve_bones - bone_names)
	present = sorted(fcurve_bones & bone_names)
	return {
		"present": len(present),
		"missing": len(missing),
		"missing_sample": missing[:8],
	}


def merge(
	base: Path,
	anims_dir: Path,
	out: Path,
	scale: float,
	rotations_only: bool,
	strip_root_motion: bool,
	preset: str,
) -> dict:
	clip_map = CLIP_MAPS.get(preset)
	if not clip_map:
		raise RuntimeError(f"Unknown preset '{preset}'. Known: {sorted(CLIP_MAPS)}")

	clear_scene()
	import_file(base)
	apply_uniform_scale(scale)

	base_arm = find_armature()
	keep_names = {o.name for o in bpy.data.objects}

	for action in list(bpy.data.actions):
		bpy.data.actions.remove(action)
	if base_arm.animation_data:
		base_arm.animation_data_clear()

	added: list[str] = []
	coverage: dict[str, dict] = {}
	sanitized: dict[str, dict] = {}

	for fname, clip in clip_map.items():
		path = anims_dir / fname
		if not path.exists():
			print(f"SKIP missing {fname}")
			continue

		before_actions = set(bpy.data.actions)
		import_file(path)
		new_actions = [a for a in bpy.data.actions if a not in before_actions]
		if not new_actions:
			print(f"WARN: no action in {path.name}")
		else:
			action = new_actions[-1]
			action.name = sanitize_action_name(clip)
			sanitized[action.name] = sanitize_action_tracks(
				action, rotations_only=rotations_only, strip_root_motion=strip_root_motion
			)
			if clip in LOOP_CLIPS:
				mark_loop(action)
			coverage[action.name] = action_bone_coverage(action, base_arm)
			push_action_to_nla(base_arm, action, action.name)
			added.append(action.name)

		new_objs = [o for o in bpy.data.objects if o.name not in keep_names]
		remove_objects(new_objs)
		keep_names = {o.name for o in bpy.data.objects}

	cleanup_scene_graph(base_arm)

	for img in bpy.data.images:
		try:
			img.pack()
		except Exception:
			pass

	out.parent.mkdir(parents=True, exist_ok=True)
	export_kwargs = dict(
		filepath=str(out),
		export_format="GLB",
		export_animations=True,
		export_skins=True,
		export_materials="EXPORT",
		export_apply=False,
	)
	try:
		export_kwargs["export_animation_mode"] = "NLA_TRACKS"
	except Exception:
		pass
	# Prefer export without animating armature object transforms as root motion.
	try:
		export_kwargs["export_anim_slide_to_zero"] = True
	except Exception:
		pass
	bpy.ops.export_scene.gltf(**export_kwargs)

	report = {
		"out": str(out),
		"clips": added,
		"mesh_dimensions": mesh_dimensions(),
		"bone_count": len(base_arm.data.bones),
		"coverage": coverage,
		"sanitized": sanitized,
		"rotations_only": rotations_only,
		"strip_root_motion": strip_root_motion,
	}
	print("REPORT", report)
	return report


def main(argv: list[str]) -> int:
	parser = argparse.ArgumentParser()
	parser.add_argument("--base", required=True)
	parser.add_argument("--anims-dir", required=True)
	parser.add_argument("--out", required=True)
	parser.add_argument("--scale", type=float, default=1.0)
	parser.add_argument("--rotations-only", action="store_true")
	parser.add_argument("--strip-root-motion", action="store_true")
	parser.add_argument("--preset", default="mutant", choices=sorted(CLIP_MAPS.keys()))
	args = parser.parse_args(argv)
	merge(
		Path(args.base),
		Path(args.anims_dir),
		Path(args.out),
		args.scale,
		args.rotations_only,
		args.strip_root_motion,
		args.preset,
	)
	return 0


if __name__ == "__main__":
	argv = sys.argv
	argv = argv[argv.index("--") + 1 :] if "--" in argv else []
	raise SystemExit(main(argv))
