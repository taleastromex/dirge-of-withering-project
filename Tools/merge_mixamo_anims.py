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
from mathutils import Matrix


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
	"blood_knight": {
		"great sword idle.fbx": "idle",
		"great sword idle (2).fbx": "idle_2",
		"great sword idle (3).fbx": "idle_3",
		"great sword idle (4).fbx": "idle_4",
		"great sword idle (5).fbx": "idle_5",
		"great sword walk.fbx": "walk",
		"great sword run.fbx": "run",
		"great sword slash.fbx": "attack",
		"great sword slash (2).fbx": "attack_heavy",
		"great sword high spin attack.fbx": "attack_spin",
		"great sword impact.fbx": "stagger",
		"two handed sword death.fbx": "death",
	},
	# Mixamo Ch05 + standing melee + dedicated death set.
	"bandit": {
		"standing idle.fbx": "idle",
		"standing walk forward.fbx": "walk",
		"standing run forward.fbx": "run",
		"standing melee attack horizontal.fbx": "attack",
		"standing melee combo attack ver. 1.fbx": "attack_alt",
		"standing react large from left.fbx": "stagger",
		"Dying.fbx": "death",
		"Falling Back Death.fbx": "death_alt",
		"Flying Back Death.fbx": "death_flyback",
	},
	# Shared Mixamo deaths — append WITHOUT replacing the model's original `death`.
	"mixamo_deaths": {
		"Dying.fbx": "death_dying",
		"Falling Back Death.fbx": "death_alt",
		"Flying Back Death.fbx": "death_flyback",
	},
	# Calm unarmed (sheathed, default). Paths relative to Mixamo/Player/Unarmed/Action Adventure Pack/.
	"unarmed_calm": {
		"idle.fbx": "calm_idle",
		"walking.fbx": "calm_walk",
		"running.fbx": "calm_run",
	},
	# Unarmed combat (after a punch, until 5s idle). Paths relative to Mixamo/Player/Unarmed/Combat/.
	"unarmed_combat": {
		"Idle.fbx": "unarmed_idle",
		"Punching.fbx": "unarmed_attack",
		"Cross Punch.fbx": "unarmed_attack_alt",
		"Roundhouse Kick.fbx": "unarmed_attack_heavy",
		"Medium Hit To Head.fbx": "unarmed_stagger",
	},
	# 1H: unshielded Mixamo clips + Sword+Shield locomotion/idle.
	"one_hand_sword": {
		"Sword and Shield Pack/sword and shield idle.fbx": "1h_idle",
		"Sword and Shield Pack/sword and shield walk.fbx": "1h_walk",
		"Run With Sword.fbx": "1h_run",
		"Stable Sword Outward Slash.fbx": "1h_attack",
		"Stable Sword Inward Slash.fbx": "1h_attack_alt",
		"One Hand Sword Combo.fbx": "1h_attack_heavy",
		"Sword and Shield Pack/sword and shield impact.fbx": "1h_stagger",
		"Withdrawing Sword.fbx": "1h_draw",
		"Sheathing Sword.fbx": "1h_sheathe",
	},
	# Knife / club extras (optional; not in default BloodKnight rebuild).
	"knife": {
		"Knife Idle.fbx": "knife_idle",
		"Upward Thrust.fbx": "knife_attack",
		"One Hand Club Combo.fbx": "knife_combo",
	},
	# Great Sword Pack draw / sheathe.
	"great_sword_draw": {
		"draw a great sword 1.fbx": "2h_draw",
		"draw a great sword 2.fbx": "2h_draw_alt",
		"Sheath A Great Sword 1.fbx": "2h_sheathe",
		"Sheath A Great Sword 2.fbx": "2h_sheathe_alt",
	},
	"player_roll": {
		"Roll.fbx": "roll",
	},
}

LOOP_CLIPS = {
	"idle",
	"idle_2",
	"idle_3",
	"idle_4",
	"idle_5",
	"walk",
	"run",
	"calm_idle",
	"calm_walk",
	"calm_run",
	"unarmed_idle",
	"1h_idle",
	"1h_walk",
	"1h_run",
	"knife_idle",
}
# Death needs hips translation so the body settles on the floor.
KEEP_HIPS_LOCATION_CLIPS = {
	"mutant": {"death", "death_alt", "death_dying", "death_flyback"},
	"zombie": {"death", "death_alt", "death_dying", "death_flyback"},
	"blood_knight": {"death", "death_alt", "death_dying", "death_flyback"},
	"bandit": {"death", "death_alt", "death_flyback"},
	"mixamo_deaths": {"death_alt", "death_dying", "death_flyback"},
	"unarmed_calm": set(),
	"unarmed_combat": set(),
	"one_hand_sword": set(),
	"knife": set(),
	"great_sword_draw": set(),
	"player_roll": set(),
}
HIPS_NAMES = {
	"mixamorig:Hips",
	"mixamorig5:Hips",
	"Hips",
	"hips",
	"mixamorig_Hips",
}

# Newer Mixamo FBX (Idle.fbx / Sword+Shield / Knife) use short names (L_arm, hips)
# instead of mixamorig:LeftArm. Map Mixamo leaf -> donor bone.
_SIMPLE_CORE = {
	"Hips": "hips",
	"Spine": "spine",
	"Spine1": "chest",
	# Spine2 has no counterpart on short Mixamo rigs (hips/spine/chest).
	# Mapping it to chest folded the torso (DCP pose).
	"Neck": "neck",
	"Head": "head",
	"HeadTop_End": "top",
}
_SIMPLE_LIMB = {
	"Shoulder": "shoulder",
	"Arm": "arm",
	"ForeArm": "elbow",
	"Hand": "wrist",
	"UpLeg": "leg",
	"Leg": "knee",
	"Foot": "ankle",
	"ToeBase": "foot",
	"Toe_End": "toes",
}
_SIMPLE_FINGER = {
	"HandIndex": "point",
	"HandMiddle": "middle",
	"HandRing": "ring",
	"HandPinky": "pink",
	"HandThumb": "thumb",
}


def mixamo_leaf_to_simple(leaf: str) -> str | None:
	if leaf in _SIMPLE_CORE:
		return _SIMPLE_CORE[leaf]
	for prefix, side in (("Left", "L"), ("Right", "R")):
		if not leaf.startswith(prefix):
			continue
		rest = leaf[len(prefix) :]
		if rest in _SIMPLE_LIMB:
			return f"{side}_{_SIMPLE_LIMB[rest]}"
		for mix_finger, simple in _SIMPLE_FINGER.items():
			if rest.startswith(mix_finger):
				idx = rest[len(mix_finger) :]
				return f"{side}_{simple}{idx}"
	return None


def retarget_src_bone_name(dst_bone: str, src_pose_bones) -> str | None:
	if dst_bone in src_pose_bones:
		return dst_bone
	leaf = dst_bone.split(":")[-1]
	if leaf in src_pose_bones:
		return leaf
	simple = mixamo_leaf_to_simple(leaf)
	if simple and simple in src_pose_bones:
		return simple
	return None


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


def scale_hips_location_cm_to_m(action: bpy.types.Action, threshold: float = 5.0, factor: float = 0.01) -> dict:
	"""
	Mixamo FBX often keeps hips translation in centimeters while the mesh is meters.
	If key magnitudes look like cm, scale them down so death doesn't bury the body.
	"""
	hips_fcurves = []
	max_abs = 0.0
	for fc in iter_action_fcurves(action):
		path = fc.data_path
		m = re.search(r'pose\.bones\["([^"]+)"\]', path)
		bone = m.group(1) if m else ""
		is_location = ".location" in path
		is_hips = bone in HIPS_NAMES or bone.endswith(":Hips") or bone == "Hips"
		if not (is_location and is_hips):
			continue
		hips_fcurves.append(fc)
		for kp in fc.keyframe_points:
			max_abs = max(max_abs, abs(float(kp.co.y)))

	if not hips_fcurves or max_abs < threshold:
		return {"scaled": False, "max_abs": max_abs}

	for fc in hips_fcurves:
		for kp in fc.keyframe_points:
			kp.co.y *= factor
			kp.handle_left.y *= factor
			kp.handle_right.y *= factor

	return {"scaled": True, "max_abs": max_abs, "factor": factor}


def mark_loop(action: bpy.types.Action) -> None:
	# Helps some exporters / NLA cyclic use.
	try:
		action.use_cyclic = True
	except Exception:
		pass


def _find_texture(textures_dir: Path, patterns: tuple[str, ...]) -> Path | None:
	files = [p for p in textures_dir.iterdir() if p.is_file() and p.suffix.lower() in {".png", ".jpg", ".jpeg", ".tga", ".webp"}]
	lowered = [(p, p.name.lower()) for p in files]
	for pat in patterns:
		pat_l = pat.lower()
		for path, name in lowered:
			if pat_l in name:
				return path
	return None


def load_image(path: Path, non_color: bool = False) -> bpy.types.Image:
	img = bpy.data.images.load(str(path), check_existing=True)
	if non_color:
		try:
			img.colorspace_settings.name = "Non-Color"
		except Exception:
			pass
	# Keep full bit depth / size; pack later before export.
	try:
		img.pack()
	except Exception:
		pass
	return img


def _bake_ao_into_albedo(albedo: bpy.types.Image, ao: bpy.types.Image) -> bpy.types.Image:
	"""Multiply AO into albedo pixels so glTF export keeps occlusion (no multi-tex baseColor)."""
	w, h = albedo.size
	if tuple(ao.size) != (w, h):
		ao.scale(w, h)
	n = w * h * 4
	alb_px = [0.0] * n
	ao_px = [0.0] * n
	albedo.pixels.foreach_get(alb_px)
	ao.pixels.foreach_get(ao_px)
	out = bpy.data.images.new(name=f"{albedo.name}_AO", width=w, height=h, alpha=True, float_buffer=False)
	out_px = alb_px[:]
	for i in range(0, n, 4):
		ao_v = ao_px[i]
		out_px[i] *= ao_v
		out_px[i + 1] *= ao_v
		out_px[i + 2] *= ao_v
	out.pixels.foreach_set(out_px)
	out.pack()
	return out


def apply_pbr_from_textures_dir(textures_dir: Path) -> dict:
	"""
	Rebuild mesh materials with albedo + normal + metal/rough(+smoothness) + AO + emissive.
	Designed for Sketchfab Drakul / Blood Knight 2K maps, but matches by filename substring.
	"""
	textures_dir = textures_dir.resolve()
	if not textures_dir.is_dir():
		raise RuntimeError(f"textures dir missing: {textures_dir}")

	albedo = _find_texture(textures_dir, ("color", "albedo", "diffuse", "basecolor"))
	normal = _find_texture(textures_dir, ("normal",))
	metallic = _find_texture(textures_dir, ("metallic", "metalness", "metal"))
	roughness = _find_texture(textures_dir, ("roughness", "rough"))
	smoothness = _find_texture(textures_dir, ("smoothness", "smooth"))
	emissive = _find_texture(textures_dir, ("emissive", "emission"))
	ao = _find_texture(textures_dir, ("ambient_occlusion", "occlusion", "ao"))

	# Prefer explicit roughness over smoothness if both somehow match.
	if roughness and smoothness and roughness == smoothness:
		# Filename contained both tokens; treat as roughness only if "rough" wins.
		if "rough" in roughness.name.lower():
			smoothness = None
		else:
			roughness = None

	report = {
		"dir": str(textures_dir),
		"albedo": albedo.name if albedo else None,
		"normal": normal.name if normal else None,
		"metallic": metallic.name if metallic else None,
		"roughness": roughness.name if roughness else None,
		"smoothness": smoothness.name if smoothness else None,
		"emissive": emissive.name if emissive else None,
		"ao": ao.name if ao else None,
		"ao_baked": False,
		"materials": 0,
	}
	if albedo is None:
		raise RuntimeError(f"No albedo/color texture in {textures_dir}")

	alb_img = load_image(albedo, non_color=False)
	ao_img = load_image(ao, non_color=True) if ao else None
	if ao_img is not None:
		alb_img = _bake_ao_into_albedo(alb_img, ao_img)
		report["ao_baked"] = True
	n_img = load_image(normal, non_color=True) if normal else None
	m_img = load_image(metallic, non_color=True) if metallic else None
	r_img = load_image(roughness, non_color=True) if roughness else None
	s_img = load_image(smoothness, non_color=True) if smoothness else None
	e_img = load_image(emissive, non_color=False) if emissive else None

	skinned_meshes = []
	for obj in bpy.data.objects:
		if obj.type != "MESH":
			continue
		has_arm = any(mod.type == "ARMATURE" for mod in obj.modifiers)
		if not has_arm and obj.parent is not None and obj.parent.type == "ARMATURE":
			has_arm = True
		if has_arm:
			skinned_meshes.append(obj)

	targets = skinned_meshes or [o for o in bpy.data.objects if o.type == "MESH"]
	for obj in targets:
		mat = bpy.data.materials.new(name=f"{obj.name}_PBR")
		mat.use_nodes = True
		nt = mat.node_tree
		nt.nodes.clear()
		out = nt.nodes.new("ShaderNodeOutputMaterial")
		bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
		nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])

		tex_alb = nt.nodes.new("ShaderNodeTexImage")
		tex_alb.image = alb_img
		tex_alb.label = "BaseColor"
		nt.links.new(tex_alb.outputs["Color"], bsdf.inputs["Base Color"])

		if n_img is not None:
			tex_n = nt.nodes.new("ShaderNodeTexImage")
			tex_n.image = n_img
			tex_n.label = "Normal"
			nmap = nt.nodes.new("ShaderNodeNormalMap")
			nt.links.new(tex_n.outputs["Color"], nmap.inputs["Color"])
			nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])

		if m_img is not None:
			tex_m = nt.nodes.new("ShaderNodeTexImage")
			tex_m.image = m_img
			tex_m.label = "Metallic"
			nt.links.new(tex_m.outputs["Color"], bsdf.inputs["Metallic"])

		if r_img is not None:
			tex_r = nt.nodes.new("ShaderNodeTexImage")
			tex_r.image = r_img
			tex_r.label = "Roughness"
			nt.links.new(tex_r.outputs["Color"], bsdf.inputs["Roughness"])
		elif s_img is not None:
			tex_s = nt.nodes.new("ShaderNodeTexImage")
			tex_s.image = s_img
			tex_s.label = "Smoothness"
			invert = nt.nodes.new("ShaderNodeInvert")
			nt.links.new(tex_s.outputs["Color"], invert.inputs["Color"])
			nt.links.new(invert.outputs["Color"], bsdf.inputs["Roughness"])

		if e_img is not None:
			tex_e = nt.nodes.new("ShaderNodeTexImage")
			tex_e.image = e_img
			tex_e.label = "Emissive"
			# Principled emission sockets differ slightly across Blender versions.
			if "Emission Color" in bsdf.inputs:
				nt.links.new(tex_e.outputs["Color"], bsdf.inputs["Emission Color"])
				if "Emission Strength" in bsdf.inputs:
					bsdf.inputs["Emission Strength"].default_value = 1.0
			elif "Emission" in bsdf.inputs:
				nt.links.new(tex_e.outputs["Color"], bsdf.inputs["Emission"])

		if obj.data.materials:
			obj.data.materials[0] = mat
			for i in range(1, len(obj.data.materials)):
				obj.data.materials[i] = mat
		else:
			obj.data.materials.append(mat)
		report["materials"] += 1

	print("PBR", report)
	return report


def mesh_bound_to_armature(obj: bpy.types.Object, arm: bpy.types.Object) -> bool:
	if obj.type != "MESH":
		return False
	if obj.parent == arm:
		return True
	for mod in obj.modifiers:
		if mod.type == "ARMATURE" and mod.object == arm:
			return True
	return False


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
			continue
		# Mixamo death FBX often brings a dummy mesh (Icosphere) — never ship it.
		if obj.type == "MESH" and (
			not mesh_bound_to_armature(obj, base_arm)
			or "icosphere" in obj.name.lower()
		):
			orphans.append(obj)
			continue
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


def detect_mixamo_prefix(arm: bpy.types.Object) -> str:
	for bone in arm.data.bones:
		name = bone.name
		if "mixamorig" in name and ":" in name:
			return name.split(":", 1)[0] + ":"
	return "mixamorig:"


def action_mixamo_prefix(action: bpy.types.Action) -> str | None:
	for fc in iter_action_fcurves(action):
		m = re.search(r'pose\.bones\["([^"]+)"\]', fc.data_path)
		if not m:
			continue
		bone = m.group(1)
		if "mixamorig" in bone and ":" in bone:
			return bone.split(":", 1)[0] + ":"
	return None


def remap_action_mixamo_prefix(action: bpy.types.Action, to_prefix: str) -> int:
	from_prefix = action_mixamo_prefix(action)
	if not from_prefix or from_prefix == to_prefix:
		return 0
	changed = 0
	for fc in iter_action_fcurves(action):
		if from_prefix in fc.data_path:
			fc.data_path = fc.data_path.replace(from_prefix, to_prefix)
			changed += 1
	return changed


def remove_nla_track_named(arm: bpy.types.Object, name: str) -> None:
	if arm.animation_data is None:
		return
	for track in list(arm.animation_data.nla_tracks):
		if track.name == name:
			arm.animation_data.nla_tracks.remove(track)


def bind_action(arm: bpy.types.Object, action: bpy.types.Action) -> None:
	"""Assign action and Blender 4.4+/5 action slot so playback evaluates."""
	ad = arm.animation_data_create() if arm.animation_data is None else arm.animation_data
	ad.action = action
	slots = list(getattr(ad, "action_suitable_slots", []) or [])
	if slots:
		ad.action_slot = slots[0]


def strip_non_hips_locations(action: bpy.types.Action) -> int:
	"""After visual bake, drop location keys on non-hips bones (keep bone lengths)."""
	removed = 0
	to_remove = []
	for fc in list(iter_action_fcurves(action)):
		path = fc.data_path
		if ".location" not in path:
			continue
		m = re.search(r'pose\.bones\["([^"]+)"\]', path)
		bone = m.group(1) if m else ""
		is_hips = bone in HIPS_NAMES or bone.endswith(":Hips") or bone == "Hips"
		if not is_hips:
			to_remove.append(fc)
	for fc in to_remove:
		remove_fcurve(action, fc)
		removed += 1
	return removed


def is_foreign_mixamo_rig(src_arm: bpy.types.Object) -> bool:
	names = {b.name for b in src_arm.data.bones}
	if any(n.startswith("mixamorig") for n in names):
		return False
	return "L_arm" in names or "hips" in names


def iter_bones_parent_first(arm: bpy.types.Object):
	def walk(bone):
		yield bone
		for child in bone.children:
			yield from walk(child)

	for bone in arm.data.bones:
		if bone.parent is None:
			yield from walk(bone)


def _axes_to_matrix(x, y, z):
	return Matrix(
		(
			(x.x, y.x, z.x),
			(x.y, y.y, z.y),
			(x.z, y.z, z.z),
		)
	)


def retarget_aim_y(
	src_arm: bpy.types.Object,
	dst_arm: bpy.types.Object,
	src_action: bpy.types.Action,
	clip_name: str,
	axis_euler,
	copy_hips_location: bool = False,
) -> bpy.types.Action:
	"""
	Aim each dest bone +Y at the mapped source bone +Y, keeping dest roll.
	Needed for Mixamo's short rig (L_arm): rest X is ~180° from mixamorig, so
	WORLD copy-rotation twists the mesh; local copy keeps T-pose instead of the clip.
	"""
	from mathutils import Matrix, Vector

	src_arm.location = (0.0, 0.0, 0.0)
	dst_arm.location = (0.0, 0.0, 0.0)
	src_arm.rotation_euler = axis_euler.copy()
	dst_arm.rotation_euler = axis_euler.copy()
	bpy.context.view_layer.update()

	bind_action(src_arm, src_action)

	src_map: dict[str, str] = {}
	for b in dst_arm.data.bones:
		src_name = retarget_src_bone_name(b.name, src_arm.pose.bones)
		if src_name:
			src_map[b.name] = src_name
	unmapped = [b.name for b in dst_arm.data.bones if b.name not in src_map]
	if unmapped:
		print(f"RETARGET {clip_name} aim-y unmapped={unmapped}")

	for old in list(bpy.data.actions):
		if sanitize_action_name(old.name) == clip_name and old != src_action:
			bpy.data.actions.remove(old)

	dst_act = bpy.data.actions.new(clip_name)
	if dst_arm.animation_data is None:
		dst_arm.animation_data_create()
	dst_arm.animation_data.action = None
	bind_action(dst_arm, dst_act)

	for pb in dst_arm.pose.bones:
		pb.rotation_mode = "QUATERNION"
		pb.matrix_basis.identity()

	f0 = int(round(src_action.frame_range[0]))
	f1 = int(round(src_action.frame_range[1]))
	if f1 <= f0:
		f1 = f0 + 1

	scene = bpy.context.scene
	y_axis = Vector((0.0, 1.0, 0.0))
	x_axis = Vector((1.0, 0.0, 0.0))
	for frame in range(f0, f1 + 1):
		scene.frame_set(frame)
		bpy.context.view_layer.update()
		for bone in iter_bones_parent_first(dst_arm):
			dst_name = bone.name
			src_name = src_map.get(dst_name)
			if not src_name:
				continue
			dst_pb = dst_arm.pose.bones[dst_name]
			src_pb = src_arm.pose.bones[src_name]
			src_world = src_arm.matrix_world @ src_pb.matrix.to_4x4()
			src_y_world = (src_world.to_3x3() @ y_axis).normalized()
			if dst_pb.parent:
				parent_world = (dst_arm.matrix_world @ dst_pb.parent.matrix.to_4x4()).to_3x3()
			else:
				parent_world = dst_arm.matrix_world.to_3x3()
			src_y_parent = (parent_world.inverted() @ src_y_world).normalized()
			rest_local = dst_pb.bone.matrix_local.to_4x4()
			rest_3 = rest_local.to_3x3()
			rest_y = (rest_3 @ y_axis).normalized()
			rest_x = (rest_3 @ x_axis).normalized()
			rot = rest_y.rotation_difference(src_y_parent)
			x = (rot @ rest_x).normalized()
			y = src_y_parent
			z = y.cross(x)
			if z.length < 1e-6:
				z = Vector((0.0, 0.0, 1.0))
			z.normalize()
			x = z.cross(y).normalized()
			posed_local = _axes_to_matrix(x, y, z).to_4x4()
			posed_local.translation = rest_local.translation
			if dst_pb.parent:
				desired = dst_pb.parent.matrix.to_4x4() @ posed_local
			else:
				desired = posed_local
			dst_pb.matrix = desired
			is_hips = (
				dst_name in HIPS_NAMES
				or dst_name.endswith(":Hips")
				or dst_name.endswith(":hips")
				or dst_name in {"Hips", "hips"}
			)
			if copy_hips_location and is_hips:
				dst_pb.location = src_pb.location.copy()
		bpy.context.view_layer.update()
		for dst_name in src_map:
			pb = dst_arm.pose.bones[dst_name]
			pb.keyframe_insert(data_path="rotation_quaternion", frame=frame)
			is_hips = (
				dst_name in HIPS_NAMES
				or dst_name.endswith(":Hips")
				or dst_name.endswith(":hips")
				or dst_name in {"Hips", "hips"}
			)
			if copy_hips_location and is_hips:
				pb.keyframe_insert(data_path="location", frame=frame)

	dst_arm.animation_data.action = None
	print(
		f"RETARGET {clip_name}: mode=aim-y bones={len(src_map)} frames={f0}-{f1} "
		f"hips_loc={copy_hips_location}"
	)
	return dst_act


def retarget_world_rotations(
	src_arm: bpy.types.Object,
	dst_arm: bpy.types.Object,
	src_action: bpy.types.Action,
	clip_name: str,
	axis_euler,
	copy_hips_location: bool = False,
) -> bpy.types.Action:
	"""
	Bake WORLD-space bone rotations from a Mixamo anim armature onto the destination
	character. Needed when the anim FBX is a different Mixamo body (Y Bot vs custom
	Auto-Rig) — raw fcurve copy crosses the arms / collapses the great-sword grip.
	"""
	# IMPORTANT: Mixamo FBX import leaves armature.rotation_euler.x ≈ +90° (Y-up → Z-up).
	# Both arms must share that same object rotation while baking. Identity = lying down.
	# Never copy donor object scale (often 0.01 cm→m) onto the hero.
	src_scale = src_arm.scale.copy()
	src_arm.location = (0.0, 0.0, 0.0)
	dst_arm.location = (0.0, 0.0, 0.0)
	src_arm.rotation_euler = axis_euler.copy()
	dst_arm.rotation_euler = axis_euler.copy()
	src_arm.scale = (1.0, 1.0, 1.0)
	bpy.context.view_layer.update()

	bind_action(src_arm, src_action)

	for pb in dst_arm.pose.bones:
		for c in list(pb.constraints):
			if c.name.startswith("RT_"):
				pb.constraints.remove(c)

	src_map: dict[str, str] = {}
	for b in dst_arm.data.bones:
		src_name = retarget_src_bone_name(b.name, src_arm.pose.bones)
		if src_name:
			src_map[b.name] = src_name
	bones = list(src_map.keys())
	unmapped = [b.name for b in dst_arm.data.bones if b.name not in src_map]
	if unmapped:
		print(f"RETARGET {clip_name} unmapped={unmapped}")
	for name in bones:
		pb = dst_arm.pose.bones[name]
		src_name = src_map[name]
		c = pb.constraints.new("COPY_ROTATION")
		c.name = "RT_ROT"
		c.target = src_arm
		c.subtarget = src_name
		c.target_space = "WORLD"
		c.owner_space = "WORLD"
		c.mix_mode = "REPLACE"
		# Death / fall clips need hips translation so the body settles on the floor.
		is_hips = name in HIPS_NAMES or name.endswith(":Hips") or name.endswith(":hips") or name in {"Hips", "hips"}
		if copy_hips_location and is_hips:
			cl = pb.constraints.new("COPY_LOCATION")
			cl.name = "RT_LOC"
			cl.target = src_arm
			cl.subtarget = src_name
			cl.target_space = "WORLD"
			cl.owner_space = "WORLD"
			cl.use_offset = False

	for old in list(bpy.data.actions):
		if sanitize_action_name(old.name) == clip_name and old != src_action:
			bpy.data.actions.remove(old)

	dst_act = bpy.data.actions.new(clip_name)
	if dst_arm.animation_data is None:
		dst_arm.animation_data_create()
	# Detach any prior action before binding the bake target.
	dst_arm.animation_data.action = None
	bind_action(dst_arm, dst_act)

	f0 = int(round(src_action.frame_range[0]))
	f1 = int(round(src_action.frame_range[1]))
	if f1 <= f0:
		f1 = f0 + 1

	bpy.ops.object.mode_set(mode="OBJECT")
	bpy.ops.object.select_all(action="DESELECT")
	dst_arm.select_set(True)
	bpy.context.view_layer.objects.active = dst_arm
	bpy.ops.object.mode_set(mode="POSE")
	bpy.ops.pose.select_all(action="SELECT")
	bpy.ops.nla.bake(
		frame_start=f0,
		frame_end=f1,
		step=1,
		only_selected=True,
		visual_keying=True,
		clear_constraints=True,
		use_current_action=True,
		bake_types={"POSE"},
	)
	bpy.ops.object.mode_set(mode="OBJECT")

	for pb in dst_arm.pose.bones:
		for c in list(pb.constraints):
			if c.name.startswith("RT_"):
				pb.constraints.remove(c)

	baked = dst_arm.animation_data.action
	if baked is None:
		raise RuntimeError(f"Retarget bake produced no action for '{clip_name}'")
	baked.name = clip_name
	stripped = strip_non_hips_locations(baked)
	print(
		f"RETARGET {clip_name}: bones={len(bones)} frames={f0}-{f1} "
		f"stripped_loc={stripped} hips_loc={copy_hips_location}"
	)
	# Clear active action; caller pushes into NLA.
	dst_arm.animation_data.action = None
	# Restore donor scale (we're about to delete it anyway).
	src_arm.scale = src_scale
	return baked


def import_clip_map(
	*,
	anims_dir: Path,
	preset: str,
	base_arm: bpy.types.Object,
	keep_names: set[str],
	rotations_only: bool,
	strip_root_motion: bool,
	added: list[str],
	coverage: dict[str, dict],
	sanitized: dict[str, dict],
	retarget: bool = True,
	axis_euler=None,
) -> set[str]:
	clip_map = CLIP_MAPS.get(preset)
	if not clip_map:
		raise RuntimeError(f"Unknown preset '{preset}'. Known: {sorted(CLIP_MAPS)}")

	if axis_euler is None:
		axis_euler = base_arm.rotation_euler.copy()

	for fname, clip in clip_map.items():
		path = anims_dir / fname
		if not path.exists():
			print(f"SKIP missing {fname}")
			continue

		before_actions = set(bpy.data.actions)
		before_objs = set(bpy.data.objects)
		import_file(path)
		new_actions = [a for a in bpy.data.actions if a not in before_actions]
		new_actions.sort(key=lambda a: float(a.frame_range[1] - a.frame_range[0]))
		new_arms = [o for o in bpy.data.objects if o not in before_objs and o.type == "ARMATURE"]
		if not new_actions:
			print(f"WARN: no action in {path.name}")
		else:
			src_action = new_actions[-1]
			clip_name = sanitize_action_name(clip)
			for old in list(bpy.data.actions):
				if sanitize_action_name(old.name) == clip_name and old != src_action:
					bpy.data.actions.remove(old)
			remove_nla_track_named(base_arm, clip_name)

			src_arm = new_arms[0] if new_arms else None
			if new_arms and src_action is not None:
				for arm in new_arms:
					if arm.animation_data and arm.animation_data.action == src_action:
						src_arm = arm
						break
			keep_hips = KEEP_HIPS_LOCATION_CLIPS.get(preset, {"death", "death_alt", "death_flyback"})
			skipped = False
			if retarget and src_arm is not None and src_arm != base_arm:
				if is_foreign_mixamo_rig(src_arm):
					print(
						f"SKIP {path.name}: Mixamo short rig (hips/L_arm), not mixamorig. "
						"Re-download this clip onto Blood Knight in Mixamo."
					)
					skipped = True
				else:
					action = retarget_world_rotations(
						src_arm,
						base_arm,
						src_action,
						clip_name,
						axis_euler=axis_euler,
						copy_hips_location=(clip in keep_hips),
					)
					if src_action and src_action.name in bpy.data.actions:
						bpy.data.actions.remove(src_action)
			else:
				action = src_action
				action.name = clip_name
				target_prefix = detect_mixamo_prefix(base_arm)
				remapped = remap_action_mixamo_prefix(action, target_prefix)
				if remapped:
					print(f"REMAP {clip_name}: {remapped} fcurves -> {target_prefix}")

			if not skipped:
				strip_hips = strip_root_motion and clip not in keep_hips
				sanitized[action.name] = sanitize_action_tracks(
					action, rotations_only=rotations_only, strip_root_motion=strip_hips
				)
				if clip in keep_hips:
					scaled = scale_hips_location_cm_to_m(action)
					sanitized[action.name]["hips_cm_scale"] = scaled
					if scaled.get("scaled"):
						print(f"SCALE_HIPS {clip_name}: max_abs={scaled['max_abs']:.1f} -> x{scaled['factor']}")
				if clip in LOOP_CLIPS:
					mark_loop(action)
				coverage[action.name] = action_bone_coverage(action, base_arm)
				push_action_to_nla(base_arm, action, action.name)
				added.append(action.name)

		new_objs = [o for o in bpy.data.objects if o.name not in keep_names]
		remove_objects(new_objs)
		keep_names = {o.name for o in bpy.data.objects}
	return keep_names


def merge(
	base: Path,
	anims_dir: Path,
	out: Path,
	scale: float,
	rotations_only: bool,
	strip_root_motion: bool,
	preset: str,
	append: bool,
	textures_dir: Path | None = None,
	extra_anims_dir: Path | None = None,
	extra_preset: str | None = None,
	retarget: bool = True,
	extra_passes: list[tuple[Path, str]] | None = None,
) -> dict:
	if not CLIP_MAPS.get(preset):
		raise RuntimeError(f"Unknown preset '{preset}'. Known: {sorted(CLIP_MAPS)}")
	if extra_preset and not CLIP_MAPS.get(extra_preset):
		raise RuntimeError(f"Unknown extra preset '{extra_preset}'. Known: {sorted(CLIP_MAPS)}")
	if extra_preset and extra_anims_dir is None:
		raise RuntimeError("--extra-preset requires --extra-anims-dir")
	passes: list[tuple[Path, str]] = list(extra_passes or [])
	if extra_preset and extra_anims_dir is not None:
		passes.insert(0, (extra_anims_dir, extra_preset))
	for extra_dir, extra_name in passes:
		if not CLIP_MAPS.get(extra_name):
			raise RuntimeError(f"Unknown extra preset '{extra_name}'. Known: {sorted(CLIP_MAPS)}")
		if extra_dir is None:
			raise RuntimeError(f"--extra requires a directory for preset '{extra_name}'")

	clear_scene()
	import_file(base)
	apply_uniform_scale(scale)

	base_arm = find_armature()
	keep_names = {o.name for o in bpy.data.objects}
	axis_euler = base_arm.rotation_euler.copy()

	if not append:
		for action in list(bpy.data.actions):
			bpy.data.actions.remove(action)
		if base_arm.animation_data:
			base_arm.animation_data_clear()
	elif base_arm.animation_data is None:
		base_arm.animation_data_create()

	added: list[str] = []
	coverage: dict[str, dict] = {}
	sanitized: dict[str, dict] = {}

	keep_names = import_clip_map(
		anims_dir=anims_dir,
		preset=preset,
		base_arm=base_arm,
		keep_names=keep_names,
		rotations_only=rotations_only,
		strip_root_motion=strip_root_motion,
		added=added,
		coverage=coverage,
		sanitized=sanitized,
		retarget=retarget,
		axis_euler=axis_euler,
	)
	for extra_dir, extra_name in passes:
		keep_names = import_clip_map(
			anims_dir=extra_dir,
			preset=extra_name,
			base_arm=base_arm,
			keep_names=keep_names,
			rotations_only=rotations_only,
			strip_root_motion=strip_root_motion,
			added=added,
			coverage=coverage,
			sanitized=sanitized,
			retarget=retarget,
			axis_euler=axis_euler,
		)

	# Restore hero armature axis before export (retarget may have touched it).
	base_arm.rotation_euler = axis_euler
	if base_arm.animation_data:
		base_arm.animation_data.action = None
	for pb in base_arm.pose.bones:
		pb.matrix_basis.identity()
	bpy.context.view_layer.update()

	cleanup_scene_graph(base_arm)

	pbr_report = None
	if textures_dir is not None:
		pbr_report = apply_pbr_from_textures_dir(textures_dir)

	for img in bpy.data.images:
		try:
			img.pack()
		except Exception:
			pass

	out.parent.mkdir(parents=True, exist_ok=True)
	# Keep sidecars next to GLB for Godot reimport / inspection.
	if textures_dir is not None and textures_dir.is_dir():
		import shutil

		for src in textures_dir.iterdir():
			if src.suffix.lower() not in {".png", ".jpg", ".jpeg", ".tga", ".webp"}:
				continue
			dst = out.parent / f"{out.stem}_{src.name}"
			try:
				shutil.copy2(src, dst)
			except Exception as exc:
				print(f"WARN copy texture {src.name}: {exc}")

	export_kwargs = dict(
		filepath=str(out),
		export_format="GLB",
		export_animations=True,
		export_skins=True,
		export_materials="EXPORT",
		export_apply=False,
		export_texcoords=True,
		export_normals=True,
		export_tangents=True,
		export_image_format="AUTO",
	)
	try:
		export_kwargs["export_animation_mode"] = "NLA_TRACKS"
	except Exception:
		pass
	try:
		export_kwargs["export_anim_slide_to_zero"] = True
	except Exception:
		pass
	bpy.ops.export_scene.gltf(**export_kwargs)

	report = {
		"out": str(out),
		"clips": added,
		"append": append,
		"mesh_dimensions": mesh_dimensions(),
		"bone_count": len(base_arm.data.bones),
		"coverage": coverage,
		"sanitized": sanitized,
		"rotations_only": rotations_only,
		"strip_root_motion": strip_root_motion,
		"pbr": pbr_report,
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
	parser.add_argument("--append", action="store_true", help="Keep existing clips on base GLB; add/replace mapped ones.")
	parser.add_argument(
		"--textures-dir",
		default="",
		help="Optional folder with albedo/normal/metallic/roughness|smoothness/AO/emissive maps (packed into GLB).",
	)
	parser.add_argument(
		"--extra-anims-dir",
		default="",
		help="Optional second anim folder (e.g. Mixamo deaths) merged in the same pass.",
	)
	parser.add_argument(
		"--extra-preset",
		default="",
		choices=[""] + sorted(CLIP_MAPS.keys()),
		help="Preset for --extra-anims-dir.",
	)
	parser.add_argument(
		"--extra",
		action="append",
		default=[],
		metavar="DIR::PRESET",
		help="Additional anim folder + preset (repeatable). Example: Mixamo/Player/Unarmed::unarmed",
	)
	parser.add_argument("--preset", default="mutant", choices=sorted(CLIP_MAPS.keys()))
	parser.add_argument(
		"--no-retarget",
		action="store_true",
		help="Skip world-space retarget (raw fcurve copy). Breaks great-sword arms on differently proportioned Mixamo rigs.",
	)
	args = parser.parse_args(argv)
	textures = Path(args.textures_dir) if args.textures_dir else None
	extra_dir = Path(args.extra_anims_dir) if args.extra_anims_dir else None
	extra_preset = args.extra_preset or None
	extra_passes: list[tuple[Path, str]] = []
	for spec in args.extra:
		if "::" not in spec:
			raise RuntimeError(f"--extra expects DIR::PRESET, got: {spec}")
		raw_dir, raw_preset = spec.rsplit("::", 1)
		extra_passes.append((Path(raw_dir), raw_preset))
	merge(
		Path(args.base),
		Path(args.anims_dir),
		Path(args.out),
		args.scale,
		args.rotations_only,
		args.strip_root_motion,
		args.preset,
		args.append,
		textures,
		extra_dir,
		extra_preset,
		retarget=not args.no_retarget,
		extra_passes=extra_passes,
	)
	return 0


if __name__ == "__main__":
	argv = sys.argv
	argv = argv[argv.index("--") + 1 :] if "--" in argv else []
	raise SystemExit(main(argv))
