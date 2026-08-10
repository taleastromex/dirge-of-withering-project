#!/usr/bin/env python3
"""
Assemble Flooded Cathedral slice art from Sketchfab Processed packs → one glTF.
Run: blender -b --factory-startup --python Tools/build_cathedral_slice_art.py

Coordinate note: build on Blender XY ground (Z up). Godot glTF import maps to Y-up.
Corridor length runs along Blender +Y (= Godot forward after import — verify & fix in scene).
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[1]
PROC = ROOT / "Assets/ThirdParty/CathedralSlice/Processed"
OUT = PROC / "CathedralSliceArt.glb"

DUNGEON_FBX = PROC / "ModularDungeon/ModularDungeon.fbx"
DUNGEON_TEX = PROC / "ModularDungeon/textures"
ALTAR_GLTF = PROC / "AltarRuins/AltarRuins.gltf"
ANGELS_FBX = PROC / "AngelStatues/Angels.fbx"
ANGELS_TEX = PROC / "AngelStatues/textures"
GREEK_PILLAR_GLB = PROC / "GreekPillar/GreekPillar.glb"
GREEK_PILLAR_TEX = PROC / "GreekPillar/textures"
CEMETERY_FBX = PROC / "CemeteryFigure/CemeteryFigure.fbx"
CEMETERY_TEX = PROC / "CemeteryFigure/textures"
LANTERN_FBX = PROC / "Lantern/Lantern.fbx"
LANTERN_TEX = PROC / "Lantern/textures"
ALTAR_DIANA_OBJ = PROC / "AltarDiana/AltarDiana.obj"
ALTAR_DIANA_TEX = PROC / "AltarDiana/altar01.jpg"
# Keep only these templates from the modular dungeon pack.
KEEP_DUNGEON = {
	"Flagstone  floor  4x4",
	"Stone wall 4x4",
	"Large brickwall 4x4",
	"Stone cube.001",
	"Stone slab 3x3",
	"Cliff 1",
	"Cliff 2",
	"Piller ornate",
	"Piller stone",
}

# Legacy StaticBody rubble box in scene: Godot (2.8, 1.2, 1.8) → Blender (X, Y depth, Z height).
RUBBLE_SIZE_BLENDER = (2.8, 1.8, 1.2)


def clear_scene() -> None:
	bpy.ops.wm.read_factory_settings(use_empty=True)


def mesh_objects():
	return [o for o in bpy.data.objects if o.type == "MESH"]


def delete_objects(objs) -> None:
	bpy.ops.object.select_all(action="DESELECT")
	for o in objs:
		o.select_set(True)
	if bpy.context.selected_objects:
		bpy.ops.object.delete()


def duplicate(obj: bpy.types.Object, name: str, loc, rot=(0.0, 0.0, 0.0), scale=None):
	dup = obj.copy()
	dup.data = obj.data  # share mesh data
	dup.name = name
	bpy.context.scene.collection.objects.link(dup)
	dup.location = loc
	dup.rotation_euler = rot
	if scale is not None:
		dup.scale = scale
	return dup


def load_image(path: Path) -> bpy.types.Image | None:
	if not path.exists():
		print("WARN missing texture", path, file=sys.stderr)
		return None
	img = bpy.data.images.load(str(path), check_existing=True)
	img.pack()
	return img


def make_pbr_material(
	name: str,
	albedo: Path,
	normal: Path | None = None,
	roughness: Path | None = None,
	metallic: Path | None = None,
	tint=(0.74, 0.7, 0.72, 1.0),
) -> bpy.types.Material:
	mat = bpy.data.materials.new(name)
	mat.use_nodes = True
	nt = mat.node_tree
	nt.nodes.clear()
	out = nt.nodes.new("ShaderNodeOutputMaterial")
	bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
	nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])

	alb = load_image(albedo)
	if alb:
		tex = nt.nodes.new("ShaderNodeTexImage")
		tex.image = alb
		mix = nt.nodes.new("ShaderNodeMix")
		mix.data_type = "RGBA"
		mix.inputs["Factor"].default_value = 1.0
		mix.inputs[7].default_value = tint  # B color
		nt.links.new(tex.outputs["Color"], mix.inputs[6])  # A
		nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])

	if normal and normal.exists():
		nimg = load_image(normal)
		if nimg:
			nimg.colorspace_settings.name = "Non-Color"
			ntex = nt.nodes.new("ShaderNodeTexImage")
			ntex.image = nimg
			nmap = nt.nodes.new("ShaderNodeNormalMap")
			nt.links.new(ntex.outputs["Color"], nmap.inputs["Color"])
			nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])

	if roughness and roughness.exists():
		rimg = load_image(roughness)
		if rimg:
			rimg.colorspace_settings.name = "Non-Color"
			rtex = nt.nodes.new("ShaderNodeTexImage")
			rtex.image = rimg
			nt.links.new(rtex.outputs["Color"], bsdf.inputs["Roughness"])

	if metallic and metallic.exists():
		mimg = load_image(metallic)
		if mimg:
			mimg.colorspace_settings.name = "Non-Color"
			mtex = nt.nodes.new("ShaderNodeTexImage")
			mtex.image = mimg
			nt.links.new(mtex.outputs["Color"], bsdf.inputs["Metallic"])

	return mat


def assign_mat(obj: bpy.types.Object, mat: bpy.types.Material) -> None:
	if obj.data.materials:
		obj.data.materials[0] = mat
	else:
		obj.data.materials.append(mat)


def apply_ash_tint(obj: bpy.types.Object, tint=(0.72, 0.68, 0.7, 1.0)) -> None:
	# Kept for altar/angel materials that already have textures linked.
	for slot in obj.material_slots:
		mat = slot.material
		if mat is None or not mat.use_nodes:
			continue
		for node in mat.node_tree.nodes:
			if node.type != "BSDF_PRINCIPLED":
				continue
			base = node.inputs.get("Base Color")
			if base is None or not base.is_linked:
				continue
			tree = mat.node_tree
			mix = tree.nodes.new("ShaderNodeMix")
			mix.data_type = "RGBA"
			mix.inputs["Factor"].default_value = 1.0
			mix.inputs[7].default_value = tint
			from_socket = None
			for ln in list(tree.links):
				if ln.to_socket == base:
					from_socket = ln.from_socket
					tree.links.remove(ln)
					break
			if from_socket is not None:
				tree.links.new(from_socket, mix.inputs[6])
				tree.links.new(mix.outputs[2], base)


def import_dungeon_kit():
	before = set(bpy.data.objects)
	bpy.ops.import_scene.fbx(filepath=str(DUNGEON_FBX), use_anim=False)
	imported = [o for o in bpy.data.objects if o not in before]
	templates = {}
	for o in imported:
		if o.type != "MESH":
			continue
		if o.name not in KEEP_DUNGEON:
			continue
		o.location = (80.0, 0.0, 0.0)
		templates[o.name] = o
	junk = [o for o in imported if o not in templates.values()]
	delete_objects(junk)
	missing = KEEP_DUNGEON - set(templates)
	if missing:
		print("WARN missing dungeon pieces:", missing, file=sys.stderr)

	# Rebuild materials with packed textures (FBX paths were broken).
	mat_floor = make_pbr_material(
		"Mat_Flagstone",
		DUNGEON_TEX / "Flag_Stone_Floor_BaseColor_With_AO.png",
		DUNGEON_TEX / "Flag_Stone_Floor_Normal.png",
		DUNGEON_TEX / "Flag_Stone_Floor_Roughness.png",
		tint=(0.7, 0.68, 0.7, 1.0),
	)
	mat_wall = make_pbr_material(
		"Mat_StoneWall",
		DUNGEON_TEX / "Stone_Wall_BaseColor_With_AO.png",
		DUNGEON_TEX / "Stone_Wall_Normal.png",
		DUNGEON_TEX / "Stone_Wall_Roughness.png",
		tint=(0.68, 0.64, 0.66, 1.0),
	)
	mat_brick = make_pbr_material(
		"Mat_StoneDark",
		DUNGEON_TEX / "Stone_Dark_BaseColor_With_AO.png",
		DUNGEON_TEX / "Stone_Dark_Normal.png",
		DUNGEON_TEX / "Stone_Dark_Roughness.png",
		tint=(0.65, 0.62, 0.64, 1.0),
	)
	mat_ash = make_pbr_material(
		"Mat_AshStone",
		DUNGEON_TEX / "Stone_Light_BaseColor_With_AO.png",
		DUNGEON_TEX / "Stone_Light_Normal.png",
		DUNGEON_TEX / "Stone_Light_Roughness.png",
		tint=(0.72, 0.68, 0.62, 1.0),
	)
	assign_mat(templates["Flagstone  floor  4x4"], mat_floor)
	assign_mat(templates["Stone wall 4x4"], mat_wall)
	assign_mat(templates["Large brickwall 4x4"], mat_brick)
	for key in ("Stone cube.001", "Stone slab 3x3", "Cliff 1", "Cliff 2", "Piller ornate", "Piller stone"):
		if key in templates:
			assign_mat(templates[key], mat_ash if "slab" in key.lower() or "cube" in key.lower() else mat_brick)
	return templates


def decimate(obj: bpy.types.Object, ratio: float = 0.08) -> None:
	bpy.ops.object.select_all(action="DESELECT")
	obj.select_set(True)
	bpy.context.view_layer.objects.active = obj
	mod = obj.modifiers.new(name="Decimate", type="DECIMATE")
	mod.ratio = ratio
	bpy.ops.object.modifier_apply(modifier=mod.name)


def fit_dimensions(obj: bpy.types.Object, size_xyz: tuple[float, float, float]) -> None:
	"""Non-uniform scale so object AABB matches target Blender dimensions."""
	unparent_keep_world(obj)
	bpy.context.view_layer.update()
	d = obj.dimensions
	obj.scale = (
		obj.scale.x * (size_xyz[0] / max(d.x, 1e-6)),
		obj.scale.y * (size_xyz[1] / max(d.y, 1e-6)),
		obj.scale.z * (size_xyz[2] / max(d.z, 1e-6)),
	)
	apply_scale_only(obj)
	bake_base_into_mesh(obj)


def unparent_keep_world(obj: bpy.types.Object) -> None:
	mw = obj.matrix_world.copy()
	obj.parent = None
	obj.matrix_world = mw
	bpy.context.view_layer.update()


def apply_scale_only(obj: bpy.types.Object) -> None:
	bpy.ops.object.select_all(action="DESELECT")
	obj.select_set(True)
	bpy.context.view_layer.objects.active = obj
	bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)


def normalize_height(obj: bpy.types.Object, target_h: float) -> None:
	unparent_keep_world(obj)
	h = max(obj.dimensions.z, 0.001)
	obj.scale = obj.scale * (target_h / h)
	apply_scale_only(obj)


def place_prop(obj: bpy.types.Object, name: str, loc, yaw_deg: float = 0.0) -> None:
	obj.name = name
	obj.location = loc
	obj.rotation_euler = (0.0, 0.0, math.radians(yaw_deg))
	obj.scale = (1.0, 1.0, 1.0)
	snap_base_to_floor(obj)


def snap_base_to_floor(obj: bpy.types.Object) -> None:
	"""Lift/drop so the lowest world AABB point sits on Z=0 (floor)."""
	bpy.context.view_layer.update()
	from mathutils import Vector

	mat = obj.matrix_world
	zs = [(mat @ Vector(corner)).z for corner in obj.bound_box]
	min_z = min(zs)
	obj.location.z -= min_z
	bpy.context.view_layer.update()
	zs2 = [(obj.matrix_world @ Vector(corner)).z for corner in obj.bound_box]
	print(
		f"  snap {obj.name}: min_z_before={min_z:.3f} -> min_z_after={min(zs2):.3f} "
		f"max_z={max(zs2):.3f}"
	)


def bake_base_into_mesh(obj: bpy.types.Object) -> None:
	"""
	Apply transforms and shift vertices so local min Z = 0 and object origin
	sits at the base. Shared-mesh duplicates then sit correctly at location.z=0.
	"""
	unparent_keep_world(obj)
	bpy.ops.object.select_all(action="DESELECT")
	obj.select_set(True)
	bpy.context.view_layer.objects.active = obj
	bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
	mesh = obj.data
	if not mesh.vertices:
		return
	xs = [v.co.x for v in mesh.vertices]
	ys = [v.co.y for v in mesh.vertices]
	zs = [v.co.z for v in mesh.vertices]
	cx = 0.5 * (min(xs) + max(xs))
	cy = 0.5 * (min(ys) + max(ys))
	min_z = min(zs)
	for v in mesh.vertices:
		v.co.x -= cx
		v.co.y -= cy
		v.co.z -= min_z
	mesh.update()
	obj.location = (0.0, 0.0, 0.0)
	bpy.context.view_layer.update()
	print(
		f"  bake_base {obj.name}: height={obj.dimensions.z:.3f} "
		f"min_z={min(v.co.z for v in mesh.vertices):.4f} "
		f"xy_centered"
	)


def ensure_upright_tall_axis(obj: bpy.types.Object) -> None:
	"""Rotate so the longest local axis becomes +Z (standing)."""
	unparent_keep_world(obj)
	dims = obj.dimensions
	# Pick tallest axis: 0=X, 1=Y, 2=Z
	axes = (dims.x, dims.y, dims.z)
	tall = max(range(3), key=lambda i: axes[i])
	if tall == 2:
		return
	bpy.ops.object.select_all(action="DESELECT")
	obj.select_set(True)
	bpy.context.view_layer.objects.active = obj
	if tall == 0:
		obj.rotation_euler = (0.0, math.radians(-90.0), 0.0)
	else:  # Y tallest
		obj.rotation_euler = (math.radians(90.0), 0.0, 0.0)
	bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
	bpy.context.view_layer.update()


def stand_local_y_as_up(obj: bpy.types.Object) -> None:
	"""
	Wide multi-figure FBX kits: longest AABB axis is width (X), figure height is Y.
	Do NOT use ensure_upright_tall_axis on those — it lays them on their side.
	"""
	unparent_keep_world(obj)
	apply_scale_only(obj)
	bpy.ops.object.select_all(action="DESELECT")
	obj.select_set(True)
	bpy.context.view_layer.objects.active = obj
	obj.rotation_euler = (math.radians(90.0), 0.0, 0.0)
	bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
	bpy.context.view_layer.update()
	print(f"  stand_y_up {obj.name}: dim={tuple(round(v, 3) for v in obj.dimensions)}")


def ensure_feet_down(obj: bpy.types.Object) -> None:
	"""
	After tall-axis is +Z: if the top slice is wider than the bottom, the mesh
	is upside-down (feet/base should be the wider end for pillars & statues).
	"""
	unparent_keep_world(obj)
	bpy.context.view_layer.update()
	from mathutils import Vector

	coords = [obj.matrix_world @ v.co for v in obj.data.vertices]
	if len(coords) < 8:
		return
	zs = [c.z for c in coords]
	z0, z1 = min(zs), max(zs)
	h = z1 - z0
	if h < 1e-4:
		return
	bot = [c for c in coords if c.z <= z0 + 0.22 * h]
	top = [c for c in coords if c.z >= z1 - 0.22 * h]
	if not bot or not top:
		return

	def avg_radius(pts: list) -> float:
		cx = sum(p.x for p in pts) / len(pts)
		cy = sum(p.y for p in pts) / len(pts)
		return sum(math.hypot(p.x - cx, p.y - cy) for p in pts) / len(pts)

	r_bot, r_top = avg_radius(bot), avg_radius(top)
	print(f"  feet_check {obj.name}: r_bot={r_bot:.3f} r_top={r_top:.3f}")
	if r_top > r_bot * 1.12:
		bpy.ops.object.select_all(action="DESELECT")
		obj.select_set(True)
		bpy.context.view_layer.objects.active = obj
		obj.rotation_euler[0] = math.radians(180.0)
		bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
		bpy.context.view_layer.update()
		print(f"  flipped {obj.name} 180° X (was upside-down)")


def build_nave(templates: dict) -> None:
	floor_t = templates["Flagstone  floor  4x4"]
	wall_t = templates["Stone wall 4x4"]
	brick_t = templates["Large brickwall 4x4"]

	# Floor: cover ~12 x 36 (x in [-6,6], y in [-18,18])
	for xi in range(-1, 2):
		for yi in range(-4, 5):
			duplicate(floor_t, f"Floor_{xi}_{yi}", loc=(xi * 4.0, yi * 4.0, 0.0))

	# Side walls — two courses; rotate so thin axis faces the nave
	for side, x in (("L", -6.2), ("R", 6.2)):
		yaw = math.radians(90.0 if side == "L" else -90.0)
		for yi in range(-4, 5):
			for course, z in enumerate((1.4, 4.2)):
				src = wall_t if course == 0 else brick_t
				duplicate(src, f"Wall{side}_{yi}_{course}", loc=(x, yi * 4.0, z), rot=(0.0, 0.0, yaw))

	for name, y, yaw in (("EntranceWall", 17.8, math.radians(180.0)), ("ApseWall", -17.8, 0.0)):
		for xi in (-4.0, 0.0, 4.0):
			for course, z in enumerate((1.4, 4.2)):
				duplicate(wall_t, f"{name}_{xi}_{course}", loc=(xi, y, z), rot=(0.0, 0.0, yaw))

	# Choke props for legacy RubbleEntrance / RubbleMid — dungeon stone, not broken Sketchfab piles.
	place_rubble_blockers(templates)

	for t in templates.values():
		t.hide_render = True
		t.hide_viewport = True
		t.hide_set(True)


def place_rubble_blockers(templates: dict) -> None:
	"""
	Fill RubbleEntrance / RubbleMid footprints with ModularDungeon cliffs/cubes.
	Godot box (2.8, 1.2, 1.8) → Blender (2.8, 1.8, 1.2); spots via 180°Y.
	"""
	src_a = templates.get("Cliff 1") or templates.get("Stone cube.001")
	src_b = templates.get("Cliff 2") or templates.get("Stone cube.001") or src_a
	if src_a is None:
		print("WARN: no dungeon mesh for rubble blockers", file=sys.stderr)
		return

	_spawn_grounded(
		src_a,
		"RubbleArt_Entrance",
		loc=(-3.2, 9.5, 0.0),
		yaw=25.0,
		size_xyz=RUBBLE_SIZE_BLENDER,
	)
	_spawn_grounded(
		src_b,
		"RubbleArt_Mid",
		loc=(3.0, -0.5, 0.0),
		yaw=-40.0,
		size_xyz=RUBBLE_SIZE_BLENDER,
	)


def import_perimeter_columns_and_statues() -> None:
	"""
	Replace the 6 big box props (PillarA–D + GateL/R) with Greek columns + statues.
	Blender XY: after glTF Y-up + scene 180°Y → Godot positions of those StaticBodies.
	"""
	# Godot (x,z) → Blender ( -x mapped via 180, z as Y ) = Blender (± mirrored)
	# Targets in Blender space:
	pillar_spots = [
		("PerimeterColumn_A", (3.6, 5.0, 0.0), 0.0),  # → Godot PillarA (-3.6, 5)
		("PerimeterColumn_B", (-3.6, 5.0, 0.0), 15.0),  # → PillarB
		("PerimeterColumn_C", (3.6, -3.0, 0.0), -10.0),  # → PillarC
		("PerimeterColumn_D", (-3.6, -3.0, 0.0), 20.0),  # → PillarD
	]
	gate_spots = [
		("PerimeterStatue_GateL", (2.4, 13.5, 0.0), 180.0),  # → GateLeft
		("PerimeterStatue_GateR", (-2.4, 13.5, 0.0), 180.0),  # → GateRight
	]

	# --- Greek pillars (4) ---
	if not GREEK_PILLAR_GLB.exists():
		print("WARN: GreekPillar.glb missing (run prepare + assimp)", file=sys.stderr)
	else:
		before = set(bpy.data.objects)
		bpy.ops.import_scene.gltf(filepath=str(GREEK_PILLAR_GLB))
		imported = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
		if not imported:
			print("WARN: GreekPillar import produced no meshes", file=sys.stderr)
		else:
			# Assimp splits the column — join into one mesh.
			bpy.ops.object.select_all(action="DESELECT")
			for o in imported:
				o.select_set(True)
			bpy.context.view_layer.objects.active = imported[0]
			if len(imported) > 1:
				bpy.ops.object.join()
			src = bpy.context.view_layer.objects.active
			ensure_upright_tall_axis(src)
			# Do not feet-flip pillars: capital can be wider than the plinth.
			# Walls are ~5.5 m tall — columns to the “ceiling” lip.
			normalize_height(src, target_h=5.45)
			# Origin often sits at the capital — bake base into mesh so Z=0 = floor.
			bake_base_into_mesh(src)
			mat_greek = make_pbr_material(
				"Mat_GreekPillar",
				GREEK_PILLAR_TEX / "1001_albedo.jpg",
				GREEK_PILLAR_TEX / "1001_normal.png",
				GREEK_PILLAR_TEX / "1001_roughness.jpg",
				tint=(0.7, 0.68, 0.7, 1.0),
			)
			assign_mat(src, mat_greek)
			src.location = (90.0, 0.0, 0.0)
			for name, loc, yaw in pillar_spots:
				dup = duplicate(src, name, loc=loc, rot=(0.0, 0.0, math.radians(yaw)))
				# Shared mesh already grounded; keep instance on floor.
				dup.location = (loc[0], loc[1], 0.0)
				snap_base_to_floor(dup)
				print(name, "dims", tuple(round(v, 2) for v in dup.dimensions))
			src.hide_set(True)
			src.hide_render = True
			src.hide_viewport = True

	# --- Gate statues: cemetery figure on both piers (angels kit squashes badly). ---
	if CEMETERY_FBX.exists():
		before = set(bpy.data.objects)
		bpy.ops.import_scene.fbx(filepath=str(CEMETERY_FBX), use_anim=False)
		imported = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
		if imported:
			fig = imported[0]
			delete_objects([o for o in imported[1:]])
			ensure_upright_tall_axis(fig)
			ensure_feet_down(fig)
			normalize_height(fig, target_h=3.2)
			if fig.dimensions.x > 2.2:
				fig.scale *= 2.2 / max(fig.dimensions.x, 0.001)
				apply_scale_only(fig)
			bake_base_into_mesh(fig)
			diff = next(CEMETERY_TEX.glob("*diffuse*"), None) or next(
				CEMETERY_TEX.glob("*Diffuse*"), None
			)
			norm = next(CEMETERY_TEX.glob("*normal*"), None) or next(
				CEMETERY_TEX.glob("*Normal*"), None
			)
			if diff:
				assign_mat(
					fig,
					make_pbr_material(
						"Mat_Cemetery",
						diff,
						norm,
						tint=(0.68, 0.66, 0.68, 1.0),
					),
				)
			name_l, loc_l, yaw_l = gate_spots[0]
			place_prop(fig, name_l, loc_l, yaw_l)
			print(name_l, "dims", tuple(round(v, 2) for v in fig.dimensions))

			name_r, loc_r, yaw_r = gate_spots[1]
			fig_r = duplicate(fig, name_r, loc=loc_r, rot=(0.0, 0.0, math.radians(yaw_r + 18.0)))
			snap_base_to_floor(fig_r)
			print(name_r, "dims", tuple(round(v, 2) for v in fig_r.dimensions))
	else:
		print("WARN: CemeteryFigure missing", file=sys.stderr)


def _spawn_grounded(
	src,
	name: str,
	loc,
	yaw: float,
	size_xyz=None,
	height=None,
	*,
	mode: str = "box",
) -> bpy.types.Object:
	"""Duplicate a dungeon template, optional fit, base on Z=0. mode: box|heap|column."""
	from mathutils import Vector

	dup = duplicate(src, name, loc=(0.0, 0.0, 0.0))
	dup.data = dup.data.copy()
	# Templates are hidden — copies inherit that and would be skipped on export.
	dup.hide_set(False)
	dup.hide_viewport = False
	dup.hide_render = False
	unparent_keep_world(dup)
	dup.location = (0.0, 0.0, 0.0)
	dup.rotation_euler = (0.0, 0.0, 0.0)
	dup.scale = (1.0, 1.0, 1.0)
	bpy.ops.object.select_all(action="DESELECT")
	dup.select_set(True)
	bpy.context.view_layer.objects.active = dup
	bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
	bpy.context.view_layer.update()

	if size_xyz is not None:
		d = dup.dimensions
		dup.scale = (
			size_xyz[0] / max(d.x, 1e-6),
			size_xyz[1] / max(d.y, 1e-6),
			size_xyz[2] / max(d.z, 1e-6),
		)
		apply_scale_only(dup)
	elif mode == "column" and height is not None:
		ensure_upright_tall_axis(dup)
		normalize_height(dup, target_h=height)
	elif height is not None:
		# Cliffs/heaps: shortest axis on +Z, then set height + cap footprint.
		dims = dup.dimensions
		short = min(range(3), key=lambda i: (dims.x, dims.y, dims.z)[i])
		if short == 0:
			dup.rotation_euler = (0.0, math.radians(90.0), 0.0)
		elif short == 1:
			dup.rotation_euler = (math.radians(-90.0), 0.0, 0.0)
		bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
		normalize_height(dup, target_h=height)
		max_foot = 3.2
		foot = max(dup.dimensions.x, dup.dimensions.y)
		if foot > max_foot:
			dup.scale *= max_foot / foot
			apply_scale_only(dup)
	else:
		normalize_height(dup, target_h=max(dup.dimensions.z, 0.5))

	bake_base_into_mesh(dup)
	dup.location = (loc[0], loc[1], 0.0)
	dup.rotation_euler = (0.0, 0.0, math.radians(yaw))
	bpy.context.view_layer.update()
	zs = [(dup.matrix_world @ Vector(c)).z for c in dup.bound_box]
	dup.location.z -= min(zs)
	bpy.context.view_layer.update()
	zs2 = [(dup.matrix_world @ Vector(c)).z for c in dup.bound_box]
	print(
		name,
		"dims",
		tuple(round(v, 2) for v in dup.dimensions),
		f"Z[{min(zs2):.3f},{max(zs2):.3f}]",
	)
	return dup


def import_diana_altar() -> None:
	"""altar-for-diana OBJ → grounded ApseAltar at gameplay stub spot."""
	if not ALTAR_DIANA_OBJ.exists():
		print("WARN: AltarDiana.obj missing", file=sys.stderr)
		return

	before = set(bpy.data.objects)
	bpy.ops.wm.obj_import(filepath=str(ALTAR_DIANA_OBJ))
	imported = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
	if not imported:
		print("WARN: AltarDiana import empty", file=sys.stderr)
		return

	altar = imported[0]
	delete_objects([o for o in imported[1:]])
	unparent_keep_world(altar)
	altar.hide_set(False)
	altar.hide_viewport = False
	altar.hide_render = False
	# OBJ already stands on Z (taller axis). -90°X made it lie flat — keep identity up.
	altar.location = (0.0, 0.0, 0.0)
	altar.rotation_euler = (0.0, 0.0, 0.0)
	altar.scale = (1.0, 1.0, 1.0)
	bpy.ops.object.select_all(action="DESELECT")
	altar.select_set(True)
	bpy.context.view_layer.objects.active = altar
	bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

	# Heavy scan — decimate for the slice budget.
	decimate(altar, ratio=0.08)
	# Wider end = base (raw import already has r_bot > r_top).
	normalize_height(altar, target_h=1.55)
	foot = max(altar.dimensions.x, altar.dimensions.y)
	if foot > 1.9:
		altar.scale *= 1.9 / foot
		apply_scale_only(altar)
	bake_base_into_mesh(altar)

	if ALTAR_DIANA_TEX.exists():
		assign_mat(
			altar,
			make_pbr_material(
				"Mat_ApseAltarDiana",
				ALTAR_DIANA_TEX,
				tint=(0.78, 0.72, 0.64, 1.0),
			),
		)

	altar.name = "ApseAltar"
	# Godot AltarStub (0, -15.2) → Blender (0, -15.2); 180° yaw faces the nave.
	altar.location = (0.0, -15.2, 0.0)
	altar.rotation_euler = (0.0, 0.0, math.radians(180.0))
	snap_base_to_floor(altar)
	print(
		"ApseAltar (diana)",
		"dims",
		tuple(round(v, 2) for v in altar.dimensions),
		"stand + yaw180",
	)


def import_apse_props(templates: dict) -> None:
	"""
	Apse: Diana altar + ModularDungeon flanking ruins/pillars.
	Godot AltarStub ≈ (0, -15.2) → Blender (0, -15.2) after scene 180°Y.
	"""
	cliff_a = templates.get("Cliff 1")
	cliff_b = templates.get("Cliff 2") or cliff_a
	pillar = templates.get("Piller ornate") or templates.get("Piller stone")

	import_diana_altar()

	# Broken stone / cliff ruins flanking the apse — fully grounded heaps.
	if cliff_a is not None:
		_spawn_grounded(
			cliff_a,
			"ApseRuin_L",
			loc=(-3.4, -14.2, 0.0),
			yaw=35.0,
			height=1.8,
		)
	if cliff_b is not None:
		_spawn_grounded(
			cliff_b,
			"ApseRuin_R",
			loc=(3.2, -13.8, 0.0),
			yaw=-50.0,
			height=1.6,
		)

	# Vertical accent pillars behind altar (reads as ruined colonnade).
	if pillar is not None:
		_spawn_grounded(
			pillar,
			"ApsePillar_L",
			loc=(-2.2, -16.8, 0.0),
			yaw=8.0,
			height=4.2,
			mode="column",
		)
		_spawn_grounded(
			pillar,
			"ApsePillar_R",
			loc=(2.2, -16.8, 0.0),
			yaw=-12.0,
			height=3.8,
			mode="column",
		)


def import_hanging_lanterns() -> None:
	"""
	Source/lantern is a flashlight kit — hang a few near Breach/Altar lights as practicals.
	Godot light (x,y,z) → Blender (-x, z, y) with CathedralArt 180°Y.
	  BreachMid  (-0.5, 7.5, 1)   → (0.5, 1.0, 7.2)
	  BreachApse (0.8, 7, -12)    → (-0.8, -12.0, 6.8)
	  AltarGlow  (0, 1.8, -14.5)  → elevated prop near apse (0.0, -14.2, 3.4)
	"""
	if not LANTERN_FBX.exists():
		print("WARN: Lantern.fbx missing", file=sys.stderr)
		return

	before = set(bpy.data.objects)
	bpy.ops.import_scene.fbx(filepath=str(LANTERN_FBX), use_anim=False)
	imported = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
	if not imported:
		print("WARN: lantern import empty", file=sys.stderr)
		return

	src = imported[0]
	delete_objects([o for o in imported[1:]])
	unparent_keep_world(src)
	src.location = (0.0, 0.0, 0.0)
	src.rotation_euler = (0.0, 0.0, 0.0)
	src.scale = (1.0, 1.0, 1.0)
	bpy.ops.object.select_all(action="DESELECT")
	src.select_set(True)
	bpy.context.view_layer.objects.active = src
	bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
	normalize_height(src, target_h=0.55)
	bake_base_into_mesh(src)

	metal = LANTERN_TEX / "flashlight.ver2FULL_Metal_BaseColor.png"
	metal_n = LANTERN_TEX / "flashlight.ver2FULL_Metal_Normal.png"
	metal_r = LANTERN_TEX / "flashlight.ver2FULL_Metal_Roughness.png"
	glass_e = LANTERN_TEX / "flashlight.ver2FULL_Glass_Emissive.png"
	mat = make_pbr_material(
		"Mat_HangingLantern",
		metal if metal.exists() else LANTERN_TEX / "flashlight.ver2FULL_MetalAlpha_BaseColor.png",
		metal_n if metal_n.exists() else None,
		metal_r if metal_r.exists() else None,
		tint=(0.75, 0.7, 0.55, 1.0),
	)
	# Warm glow so it reads as the breach practical.
	if mat.use_nodes and glass_e.exists():
		nt = mat.node_tree
		bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
		eimg = load_image(glass_e)
		if eimg is not None:
			etex = nt.nodes.new("ShaderNodeTexImage")
			etex.image = eimg
			bsdf.inputs["Emission Color"].default_value = (1.0, 0.85, 0.45, 1.0)
			bsdf.inputs["Emission Strength"].default_value = 2.4
			nt.links.new(etex.outputs["Color"], bsdf.inputs["Emission Color"])
	assign_mat(src, mat)

	# Tip slightly so the body hangs / aims down like a dropped work light.
	spots = [
		("HangingLantern_Mid", (0.5, 1.0, 7.2), 15.0, 35.0),
		("HangingLantern_Apse", (-0.8, -12.0, 6.8), -20.0, 40.0),
		("HangingLantern_Altar", (0.0, -14.2, 3.4), 180.0, 25.0),
	]
	src.location = (96.0, 0.0, 0.0)
	for name, loc, yaw, tip_deg in spots:
		dup = duplicate(src, name, loc=loc, rot=(math.radians(tip_deg), 0.0, math.radians(yaw)))
		dup.hide_set(False)
		dup.hide_viewport = False
		dup.hide_render = False
		print(name, "loc", loc, "dims", tuple(round(v, 2) for v in dup.dimensions))
	src.hide_set(True)
	src.hide_render = True
	src.hide_viewport = True


def export_glb() -> None:
	bpy.ops.object.select_all(action="DESELECT")
	for o in bpy.data.objects:
		if o.hide_get() or o.hide_viewport:
			continue
		o.select_set(True)
	OUT.parent.mkdir(parents=True, exist_ok=True)
	bpy.ops.export_scene.gltf(
		filepath=str(OUT),
		export_format="GLB",
		use_selection=True,
		export_apply=True,
		export_texcoords=True,
		export_normals=True,
		export_materials="EXPORT",
		export_image_format="JPEG",
		export_jpeg_quality=82,
		export_yup=True,
	)
	print("Wrote", OUT, "size_mb", round(OUT.stat().st_size / 1e6, 2))


def main() -> None:
	clear_scene()
	templates = import_dungeon_kit()
	build_nave(templates)
	import_perimeter_columns_and_statues()
	import_apse_props(templates)  # includes altar-for-diana as ApseAltar
	import_hanging_lanterns()
	export_glb()


if __name__ == "__main__":
	main()
