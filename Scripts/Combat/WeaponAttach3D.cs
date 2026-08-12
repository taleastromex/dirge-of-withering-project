using System.Collections.Generic;
using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Equips a weapon on a Mixamo hand bone via <see cref="BoneAttachment3D"/>.
/// Preferred: <see cref="WeaponEquipData.SocketAuthored"/> meshes (identity local xform).
/// Legacy per-export grip/slide/pre-rot remain for unbaked props (e.g. zweihander).
/// </summary>
public partial class WeaponAttach3D : Node
{
	[Export]
	public NodePath ModelPath { get; set; } = new("../Visual/Model");

	/// <summary>Inventory-ready equip definition. When set, applied on ready (and via <see cref="Equip"/>).</summary>
	[Export]
	public WeaponEquipData? EquipData { get; set; }

	[Export]
	public string BoneName { get; set; } = "mixamorig:RightHand";

	[Export]
	public PackedScene? WeaponScene { get; set; }

	[Export]
	public string WeaponPath { get; set; } = "";

	[Export]
	public string WeaponNodeName { get; set; } = "Weapon";

	[Export]
	public float TargetLengthMeters { get; set; } = 1.55f;

	[Export]
	public Vector3 GripLocalPosition { get; set; } = new(0.0f, 0.06f, -0.03f);

	[Export]
	public Vector3 GripLocalRotationDegrees { get; set; } = new(225f, 0f, -90f);

	[Export]
	public Vector3 StrikeGripLocalRotationDegrees { get; set; } = new(160f, 0f, -90f);

	[Export]
	public float BladeSlideMeters { get; set; } = 0.32f;

	[Export]
	public float StrikeBladeSlideMeters { get; set; } = 0.22f;

	[Export]
	public Vector3 SlideLocalAxis { get; set; } = new(1f, 0f, 0f);

	[Export]
	public Vector3 MeshPreRotationDegrees { get; set; }

	/// <summary>
	/// Roll the mesh around the handle so the blade edge faces the owner's forward.
	/// Off by default — heuristic was unreliable for asymmetric axe heads.
	/// </summary>
	[Export]
	public bool AutoAlignBladeEdge { get; set; }

	/// <summary>If auto-align picks the spine instead of the edge, flip 180°.</summary>
	[Export]
	public bool AutoAlignBladeFlip { get; set; }

	/// <summary>
	/// Q/E roll blade around handle; freezes AI and forces T-pose. Print angle to Output.
	/// </summary>
	[Export]
	public bool GripTuneMode { get; set; }

	[Export]
	public float GripTuneStepDegrees { get; set; } = 15f;

	[Export]
	public bool AlignLongestAxisToX { get; set; }

	[Export]
	public bool RecenterGripToPommel { get; set; } = true;

	[Export(PropertyHint.Range, "0.02,0.4,0.01")]
	public float StrikeBlendInNorm { get; set; } = 0.1f;

	[Export(PropertyHint.Range, "0.02,0.4,0.01")]
	public float StrikeBlendOutNorm { get; set; } = 0.14f;

	[Export]
	public Texture2D? AlbedoTexture { get; set; }

	[Export]
	public Texture2D? NormalTexture { get; set; }

	[Export]
	public PlayerAnimDriver? PlayerAnim { get; set; }

	[Export]
	public EnemyAnimDriver? EnemyAnim { get; set; }

	private Skeleton3D? _skeleton;
	private BoneAttachment3D? _boneAttach;
	private Node3D? _weapon;
	private float _fitScale = 1f;
	private Basis _gripBasis = Basis.Identity;
	private Vector3 _gripOrigin;
	private Quaternion _idleGripQuat = Quaternion.Identity;
	private Quaternion _strikeGripQuat = Quaternion.Identity;
	private bool _socketAuthored;
	private bool _bound;

	public WeaponEquipData? CurrentEquip => EquipData;

	public override void _Ready()
	{
		PlayerAnim ??= GetNodeOrNull<PlayerAnimDriver>("../AnimDriver");
		EnemyAnim ??= GetNodeOrNull<EnemyAnimDriver>("../AnimDriver");
		// EquipData overrides exports when set; otherwise scene exports are used as-is.
		if (EquipData != null)
		{
			ApplyEquipData(EquipData);
		}

		CallDeferred(MethodName.AttachWeapon);
		if (GripTuneMode)
		{
			SetProcessInput(true);
			CallDeferred(nameof(BeginGripTuneMode));
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (!GripTuneMode || _weapon == null)
		{
			return;
		}

		if (@event is not InputEventKey key || !key.Pressed || key.Echo)
		{
			return;
		}

		// PhysicalKeycode = QWERTY seat (works on ЙЦУКЕН).
		Key code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;

		float step = GripTuneStepDegrees;
		bool shift = key.ShiftPressed;
		if (shift)
		{
			step *= 0.2f; // fine adjust
		}

		Vector3 grip = GripLocalRotationDegrees;
		Vector3 pre = MeshPreRotationDegrees;
		Vector3 pos = GripLocalPosition;
		bool changed = true;

		switch (code)
		{
			// Mesh roll around handle (blade edge)
			case Key.Q:
				pre.X -= step;
				break;
			case Key.E:
				pre.X += step;
				break;

			// Grip euler (Godot YXZ): pitch / yaw / roll of the whole prop in the hand
			case Key.R:
				grip.X -= step;
				break;
			case Key.F:
				grip.X += step;
				break;
			case Key.T:
				grip.Y -= step;
				break;
			case Key.G:
				grip.Y += step;
				break;
			case Key.Y:
				grip.Z -= step;
				break;
			case Key.H:
				grip.Z += step;
				break;

			// Nudge position in hand (meters)
			case Key.U:
				pos.X -= 0.01f * (shift ? 0.25f : 1f);
				break;
			case Key.I:
				pos.X += 0.01f * (shift ? 0.25f : 1f);
				break;
			case Key.J:
				pos.Y -= 0.01f * (shift ? 0.25f : 1f);
				break;
			case Key.K:
				pos.Y += 0.01f * (shift ? 0.25f : 1f);
				break;
			case Key.N:
				pos.Z -= 0.01f * (shift ? 0.25f : 1f);
				break;
			case Key.M:
				pos.Z += 0.01f * (shift ? 0.25f : 1f);
				break;

			case Key.P:
				// Print only — handy snapshot
				break;

			default:
				changed = false;
				break;
		}

		if (!changed && code != Key.P)
		{
			return;
		}

		pre.X = WrapDegrees(pre.X);
		grip.X = WrapDegrees(grip.X);
		grip.Y = WrapDegrees(grip.Y);
		grip.Z = WrapDegrees(grip.Z);

		MeshPreRotationDegrees = pre;
		GripLocalRotationDegrees = grip;
		StrikeGripLocalRotationDegrees = grip; // keep strike = idle while tuning
		GripLocalPosition = pos;

		_idleGripQuat = Quaternion.FromEuler(grip * (Mathf.Pi / 180f));
		_strikeGripQuat = _idleGripQuat;
		ApplyMeshPreRotationToChildren();
		ApplyGripPose(0f);
		if (_weapon != null)
		{
			Basis localBasis = _gripBasis.Scaled(Vector3.One * _fitScale);
			_weapon.Transform = new Transform3D(localBasis, _gripOrigin);
		}

		GD.Print(
			"[GripTune] " +
			$"preX={pre.X:0.#}  gripRot=({grip.X:0.#}, {grip.Y:0.#}, {grip.Z:0.#})  " +
			$"pos=({pos.X:0.###}, {pos.Y:0.###}, {pos.Z:0.###})  " +
			"| Q/E preX | R/F gripX | T/G gripY | Y/H gripZ | U/I J/K N/M pos | Shift=fine | P=print");
		GetViewport().SetInputAsHandled();
	}

	private static float WrapDegrees(float deg)
	{
		deg %= 360f;
		if (deg > 180f)
		{
			deg -= 360f;
		}

		if (deg < -180f)
		{
			deg += 360f;
		}

		return deg;
	}

	private void BeginGripTuneMode()
	{
		if (GetParent() is BasicEnemy enemy)
		{
			enemy.AiEnabled = false;
		}

		EnemyAnim ??= GetNodeOrNull<EnemyAnimDriver>("../AnimDriver");
		EnemyAnim?.ForceRestPose();
		SetProcessInput(true);
		GD.Print(
			"[GripTune] T-pose + AI off.\n" +
			"  Q/E = blade roll (preX)\n" +
			"  R/F = grip pitch (X)   T/G = grip yaw (Y)   Y/H = grip roll (Z)\n" +
			"  U/I J/K N/M = position XYZ (±1cm)\n" +
			"  Shift = fine step   P = print current");
	}

	public override void _Process(double delta)
	{
		if (GripTuneMode)
		{
			EnemyAnim?.ForceRestPose();
		}

		if (_weapon == null || _boneAttach == null)
		{
			return;
		}

		if (_socketAuthored)
		{
			_weapon.Transform = Transform3D.Identity;
			return;
		}

		float strike = GripTuneMode ? 0f : ResolveStrikeWeight();
		ApplyGripPose(strike);
		Basis localBasis = _gripBasis.Scaled(Vector3.One * _fitScale);
		_weapon.Transform = new Transform3D(localBasis, _gripOrigin);
	}

	private void ApplyMeshPreRotationToChildren()
	{
		if (_weapon == null)
		{
			return;
		}

		Basis align = Basis.FromEuler(MeshPreRotationDegrees * (Mathf.Pi / 180f));
		foreach (Node child in _weapon.GetChildren())
		{
			if (child is MeshInstance3D mi)
			{
				mi.Basis = align;
			}
		}
	}

	/// <summary>Swap held weapon (inventory). Pass null to unequip.</summary>
	public void Equip(WeaponEquipData? data)
	{
		EquipData = data;
		ClearAttached();
		if (data != null)
		{
			ApplyEquipData(data);
		}

		if (_bound || IsInsideTree())
		{
			AttachWeapon();
		}
	}

	public void Unequip()
	{
		Equip(null);
	}

	private void ApplyEquipData(WeaponEquipData data)
	{
		BoneName = string.IsNullOrWhiteSpace(data.BoneName) ? BoneName : data.BoneName;
		WeaponScene = data.WeaponScene;
		WeaponPath = data.WeaponPath ?? "";
		WeaponNodeName = string.IsNullOrWhiteSpace(data.WeaponNodeName) ? WeaponNodeName : data.WeaponNodeName;
		_socketAuthored = data.SocketAuthored;

		if (data.SocketAuthored)
		{
			GripLocalPosition = data.LocalPosition;
			GripLocalRotationDegrees = data.LocalRotationDegrees;
			StrikeGripLocalRotationDegrees = data.ResolveStrikeRotationDegrees();
			BladeSlideMeters = 0f;
			StrikeBladeSlideMeters = 0f;
			MeshPreRotationDegrees = Vector3.Zero;
			AlignLongestAxisToX = false;
			RecenterGripToPommel = false;
			TargetLengthMeters = data.TargetLengthMeters;
			return;
		}

		GripLocalPosition = data.LocalPosition;
		GripLocalRotationDegrees = data.LocalRotationDegrees;
		StrikeGripLocalRotationDegrees = data.ResolveStrikeRotationDegrees();
		BladeSlideMeters = data.BladeSlideMeters;
		StrikeBladeSlideMeters = data.StrikeBladeSlideMeters;
		MeshPreRotationDegrees = data.MeshPreRotationDegrees;
		AlignLongestAxisToX = data.AlignLongestAxisToX;
		RecenterGripToPommel = data.RecenterGripToPommel;
		if (data.TargetLengthMeters > 0.01f)
		{
			TargetLengthMeters = data.TargetLengthMeters;
		}
	}

	private float ResolveStrikeWeight()
	{
		if (PlayerAnim != null)
		{
			return PlayerAnim.GetWeaponStrikeWeight(StrikeBlendInNorm, StrikeBlendOutNorm);
		}

		if (EnemyAnim != null)
		{
			return EnemyAnim.GetWeaponStrikeWeight(StrikeBlendInNorm, StrikeBlendOutNorm);
		}

		return 0f;
	}

	private void ApplyGripPose(float strikeWeight)
	{
		float w = Mathf.Clamp(strikeWeight, 0f, 1f);
		Quaternion q = _idleGripQuat.Slerp(_strikeGripQuat, w).Normalized();
		_gripBasis = new Basis(q);
		float slide = Mathf.Lerp(BladeSlideMeters, StrikeBladeSlideMeters, w);
		Vector3 axis = SlideLocalAxis.LengthSquared() < 0.0001f ? Vector3.Right : SlideLocalAxis.Normalized();
		Vector3 alongHandle = _gripBasis * axis;
		_gripOrigin = GripLocalPosition - alongHandle * slide;
	}

	private void AttachWeapon()
	{
		_bound = true;
		if (EquipData != null)
		{
			ApplyEquipData(EquipData);
		}

		if (string.IsNullOrWhiteSpace(WeaponPath) && WeaponScene == null)
		{
			return;
		}

		Node? modelRoot = GetNodeOrNull(ModelPath)
			?? GetParent()?.GetNodeOrNull("Visual/Model");
		if (modelRoot == null && GetParent()?.GetNodeOrNull("Visual") is Node visualNode)
		{
			foreach (Node child in visualNode.GetChildren())
			{
				modelRoot = child;
				break;
			}
		}

		if (modelRoot == null)
		{
			GD.PushWarning($"WeaponAttach3D: model missing at '{ModelPath}' (from {GetPath()}).");
			return;
		}

		_skeleton = FindSkeleton(modelRoot);
		if (_skeleton == null)
		{
			GD.PushWarning("WeaponAttach3D: Skeleton3D not found — weapon bind impossible.");
			return;
		}

		int boneIdx = ResolveBoneIndex(_skeleton);
		if (boneIdx < 0)
		{
			GD.PushWarning($"WeaponAttach3D: hand bone not found. Sample bones: {DumpBoneNames(_skeleton)}");
			return;
		}

		ClearAttached();

		_weapon = BuildNormalizedWeapon();
		if (_weapon == null)
		{
			return;
		}

		_boneAttach = new BoneAttachment3D
		{
			Name = WeaponNodeName + "Attach",
			BoneIdx = boneIdx,
		};
		_skeleton.AddChild(_boneAttach);
		_boneAttach.AddChild(_weapon);
		_weapon.Name = WeaponNodeName;

		_idleGripQuat = Quaternion.FromEuler(GripLocalRotationDegrees * (Mathf.Pi / 180f));
		_strikeGripQuat = Quaternion.FromEuler(StrikeGripLocalRotationDegrees * (Mathf.Pi / 180f));
		PlayerAnim ??= GetNodeOrNull<PlayerAnimDriver>("../AnimDriver");
		EnemyAnim ??= GetNodeOrNull<EnemyAnimDriver>("../AnimDriver");

		if (_socketAuthored)
		{
			_fitScale = 1f;
			_weapon.Transform = Transform3D.Identity;
		}
		else
		{
			ApplyGripPose(0f);
			_weapon.Transform = new Transform3D(_gripBasis.Scaled(Vector3.One * _fitScale), _gripOrigin);
			if (AutoAlignBladeEdge)
			{
				CallDeferred(MethodName.AutoAlignBladeEdgeRoll);
			}
		}

		GD.Print(
			$"WeaponAttach3D: '{WeaponNodeName}' on '{_skeleton.GetBoneName(boneIdx)}' " +
			$"socketAuthored={_socketAuthored} fitScale={_fitScale:0.####}.");
	}

	private void ClearAttached()
	{
		if (_weapon != null && GodotObject.IsInstanceValid(_weapon))
		{
			_weapon.QueueFree();
		}

		if (_boneAttach != null && GodotObject.IsInstanceValid(_boneAttach))
		{
			_boneAttach.QueueFree();
		}

		_weapon = null;
		_boneAttach = null;
	}

	/// <summary>
	/// Pick MeshPreRotation roll around +X (handle) so blade points along owner forward,
	/// not up/down or flat-to-target.
	/// </summary>
	private void AutoAlignBladeEdgeRoll()
	{
		if (_weapon == null || GetParent() is not Node3D owner)
		{
			return;
		}

		Aabb? bounds = null;
		foreach (Node child in _weapon.GetChildren())
		{
			if (child is MeshInstance3D { Mesh: not null } mi)
			{
				Aabb bb = mi.Mesh.GetAabb();
				bounds = bounds == null ? bb : bounds.Value.Merge(bb);
			}
		}

		if (bounds == null)
		{
			return;
		}

		Vector3 bladeLocal = EstimateBladeDirectionLocal(bounds.Value);
		if (AutoAlignBladeFlip)
		{
			bladeLocal = -bladeLocal;
		}

		// Godot character forward is -Z; Bandit hitbox also sits on Visual -Z.
		Vector3 facing = -owner.GlobalTransform.Basis.Z;
		facing.Y = 0f;
		if (facing.LengthSquared() < 0.0001f)
		{
			facing = Vector3.Forward;
		}

		facing = facing.Normalized();

		float bestRoll = 0f;
		float bestScore = float.NegativeInfinity;
		const int steps = 360;
		for (int i = 0; i < steps; i++)
		{
			float rollDeg = i;
			Basis pre = Basis.FromEuler(new Vector3(Mathf.DegToRad(rollDeg), 0f, 0f));
			// Weapon node already has grip; blade in world:
			Vector3 bladeWorld = (_weapon.GlobalTransform.Basis * (pre * bladeLocal)).Normalized();
			Vector3 handleWorld = (_weapon.GlobalTransform.Basis * Vector3.Right).Normalized();
			Vector3 desired = facing - handleWorld * facing.Dot(handleWorld);
			if (desired.LengthSquared() < 0.0001f)
			{
				continue;
			}

			desired = desired.Normalized();
			// Strongly prefer edge toward target; penalize vertical edge (up/down).
			float score = bladeWorld.Dot(desired) - 1.25f * Mathf.Abs(bladeWorld.Y);
			if (score > bestScore)
			{
				bestScore = score;
				bestRoll = rollDeg;
			}
		}

		MeshPreRotationDegrees = new Vector3(bestRoll, 0f, 0f);
		Basis align = Basis.FromEuler(MeshPreRotationDegrees * (Mathf.Pi / 180f));
		foreach (Node child in _weapon.GetChildren())
		{
			if (child is MeshInstance3D mi)
			{
				mi.Basis = align;
			}
		}

		GD.Print($"WeaponAttach3D: AutoAlignBladeEdge roll={bestRoll:0.#}° score={bestScore:0.###} bladeLocal={bladeLocal}");
	}

	private static Vector3 EstimateBladeDirectionLocal(Aabb bb)
	{
		// Handle is authored along +X; blade mass is the larger of Y/Z extents, biased by center.
		Vector3 size = bb.Size;
		Vector3 center = bb.GetCenter();
		if (size.Y >= size.Z)
		{
			return new Vector3(0f, center.Y >= 0f ? 1f : -1f, 0f);
		}

		return new Vector3(0f, 0f, center.Z >= 0f ? 1f : -1f);
	}

	private Node3D? BuildNormalizedWeapon()
	{
		var meshes = new List<MeshInstance3D>();
		Node3D? sourceScene = InstantiateWeaponScene();
		if (sourceScene != null)
		{
			CollectMeshes(sourceScene, meshes);
		}
		else
		{
			MeshInstance3D? fromMesh = InstantiateWeaponMesh();
			if (fromMesh != null)
			{
				meshes.Add(fromMesh);
			}
		}

		if (meshes.Count == 0)
		{
			GD.PushWarning($"WeaponAttach3D: failed to load weapon '{WeaponPath}'.");
			sourceScene?.QueueFree();
			return null;
		}

		var holder = new Node3D { Name = WeaponNodeName + "Holder" };
		Aabb? localBounds = null;
		foreach (MeshInstance3D mi in meshes)
		{
			if (mi.Mesh == null)
			{
				continue;
			}

			localBounds = localBounds == null ? mi.Mesh.GetAabb() : localBounds.Value.Merge(mi.Mesh.GetAabb());
		}

		Basis alignBasis = Basis.Identity;
		if (!_socketAuthored)
		{
			if (localBounds != null && AlignLongestAxisToX)
			{
				alignBasis = BasisToAlignLongestAxisToX(localBounds.Value);
			}

			if (MeshPreRotationDegrees.LengthSquared() > 0.0001f)
			{
				alignBasis *= Basis.FromEuler(MeshPreRotationDegrees * (Mathf.Pi / 180f));
			}
		}

		Aabb? alignedBounds = null;
		foreach (MeshInstance3D mi in meshes)
		{
			if (mi.Mesh == null)
			{
				continue;
			}

			var copy = new MeshInstance3D
			{
				Name = mi.Name,
				Mesh = mi.Mesh,
				MaterialOverride = mi.MaterialOverride,
				CastShadow = mi.CastShadow,
			};
			copy.Basis = alignBasis;
			int surfaces = mi.Mesh.GetSurfaceCount();
			for (int i = 0; i < surfaces; i++)
			{
				Material? mat = mi.GetActiveMaterial(i);
				if (mat != null)
				{
					copy.SetSurfaceOverrideMaterial(i, mat);
				}
			}

			ApplyFallbackMaterial(copy);
			holder.AddChild(copy);

			Aabb rotated = TransformAabb(new Transform3D(alignBasis, Vector3.Zero), mi.Mesh.GetAabb());
			alignedBounds = alignedBounds == null ? rotated : alignedBounds.Value.Merge(rotated);
		}

		sourceScene?.QueueFree();

		if (_socketAuthored)
		{
			_fitScale = 1f;
			return holder;
		}

		if (alignedBounds != null)
		{
			Aabb bb = alignedBounds.Value;
			float longest = Mathf.Max(bb.Size.X, Mathf.Max(bb.Size.Y, bb.Size.Z));
			_fitScale = TargetLengthMeters > 0.01f && longest > 0.001f
				? TargetLengthMeters / longest
				: 1f;

			if (RecenterGripToPommel)
			{
				Vector3 center = bb.GetCenter();
				float pommelX = Mathf.Abs(bb.Position.X) <= Mathf.Abs(bb.End.X) ? bb.Position.X : bb.End.X;
				Vector3 gripShift = new(-pommelX, -center.Y, -center.Z);
				foreach (Node child in holder.GetChildren())
				{
					if (child is Node3D n3)
					{
						n3.Position = gripShift;
					}
				}
			}
		}
		else
		{
			_fitScale = 1f;
		}

		return holder;
	}

	private static Basis BasisToAlignLongestAxisToX(Aabb bb)
	{
		if (bb.Size.X >= bb.Size.Y && bb.Size.X >= bb.Size.Z)
		{
			return Basis.Identity;
		}

		if (bb.Size.Y >= bb.Size.X && bb.Size.Y >= bb.Size.Z)
		{
			return Basis.FromEuler(new Vector3(0f, 0f, -Mathf.Pi * 0.5f));
		}

		return Basis.FromEuler(new Vector3(0f, Mathf.Pi * 0.5f, 0f));
	}

	private static Aabb TransformAabb(Transform3D xf, Aabb aabb)
	{
		Vector3 min = aabb.Position;
		Vector3 max = aabb.End;
		Vector3[] corners =
		{
			new(min.X, min.Y, min.Z),
			new(max.X, min.Y, min.Z),
			new(min.X, max.Y, min.Z),
			new(max.X, max.Y, min.Z),
			new(min.X, min.Y, max.Z),
			new(max.X, min.Y, max.Z),
			new(min.X, max.Y, max.Z),
			new(max.X, max.Y, max.Z),
		};

		Vector3 p0 = xf * corners[0];
		Vector3 bMin = p0;
		Vector3 bMax = p0;
		for (int i = 1; i < corners.Length; i++)
		{
			Vector3 p = xf * corners[i];
			bMin = bMin.Min(p);
			bMax = bMax.Max(p);
		}

		return new Aabb(bMin, bMax - bMin);
	}

	private void ApplyFallbackMaterial(MeshInstance3D mesh)
	{
		if (AlbedoTexture == null)
		{
			return;
		}

		var mat = new StandardMaterial3D
		{
			AlbedoTexture = AlbedoTexture,
			Roughness = 0.55f,
			Metallic = 0.15f,
		};
		if (NormalTexture != null)
		{
			mat.NormalEnabled = true;
			mat.NormalTexture = NormalTexture;
		}

		mesh.MaterialOverride = mat;
	}

	private Node3D? InstantiateWeaponScene()
	{
		if (WeaponScene != null)
		{
			return WeaponScene.Instantiate<Node3D>();
		}

		if (string.IsNullOrWhiteSpace(WeaponPath))
		{
			return null;
		}

		string path = WeaponPath;
		if (path.EndsWith(".obj", System.StringComparison.OrdinalIgnoreCase)
			|| path.EndsWith(".mesh", System.StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		return GD.Load<PackedScene>(path)?.Instantiate<Node3D>();
	}

	private MeshInstance3D? InstantiateWeaponMesh()
	{
		if (string.IsNullOrWhiteSpace(WeaponPath))
		{
			return null;
		}

		Mesh? mesh = GD.Load<Mesh>(WeaponPath);
		if (mesh == null)
		{
			return null;
		}

		return new MeshInstance3D
		{
			Name = WeaponNodeName,
			Mesh = mesh,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
		};
	}

	private static void CollectMeshes(Node node, List<MeshInstance3D> into)
	{
		if (node is MeshInstance3D mi)
		{
			into.Add(mi);
		}

		foreach (Node child in node.GetChildren())
		{
			CollectMeshes(child, into);
		}
	}

	private int ResolveBoneIndex(Skeleton3D skeleton)
	{
		int bone = skeleton.FindBone(BoneName);
		if (bone >= 0)
		{
			return bone;
		}

		bone = skeleton.FindBone(BoneName.Replace("mixamorig:", string.Empty));
		if (bone >= 0)
		{
			return bone;
		}

		bool preferRight = BoneName.Contains("Right", System.StringComparison.OrdinalIgnoreCase)
			|| !BoneName.Contains("Left", System.StringComparison.OrdinalIgnoreCase);
		string handKey = preferRight ? "RightHand" : "LeftHand";

		for (int i = 0; i < skeleton.GetBoneCount(); i++)
		{
			string name = skeleton.GetBoneName(i);
			if (!name.Contains(handKey, System.StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			if (name.Contains("Index", System.StringComparison.OrdinalIgnoreCase)
				|| name.Contains("Thumb", System.StringComparison.OrdinalIgnoreCase)
				|| name.Contains("Middle", System.StringComparison.OrdinalIgnoreCase)
				|| name.Contains("Ring", System.StringComparison.OrdinalIgnoreCase)
				|| name.Contains("Pinky", System.StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			return i;
		}

		return -1;
	}

	private static Skeleton3D? FindSkeleton(Node root)
	{
		if (root is Skeleton3D sk)
		{
			return sk;
		}

		foreach (Node child in root.GetChildren())
		{
			Skeleton3D? found = FindSkeleton(child);
			if (found != null)
			{
				return found;
			}
		}

		return null;
	}

	private static string DumpBoneNames(Skeleton3D skeleton)
	{
		var names = new List<string>();
		int count = Mathf.Min(skeleton.GetBoneCount(), 16);
		for (int i = 0; i < count; i++)
		{
			names.Add(skeleton.GetBoneName(i));
		}

		return string.Join(", ", names);
	}
}
