using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Holds the zweihander on the Mixamo right-hand bone and keeps it synced every frame
/// so locomotion / attack / death animations carry the weapon correctly.
/// </summary>
public partial class PlayerWeaponAttach : Node
{
	[Export]
	public NodePath ModelPath { get; set; } = new("../Visual/Model");

	[Export]
	public string BoneName { get; set; } = "mixamorig:RightHand";

	[Export]
	public PackedScene? WeaponScene { get; set; }

	[Export]
	public string WeaponPath { get; set; } =
		"res://Assets/ThirdParty/Weapons/zweihander.glb";

	/// <summary>World-space blade length to fit the prop to (meters).</summary>
	[Export]
	public float TargetLengthMeters { get; set; } = 1.55f;

	[Export]
	public Vector3 GripLocalPosition { get; set; } = new(0.0f, 0.06f, -0.03f);

	/// <summary>
	/// Bone-local euler (Godot YXZ). Idle / locomotion pose (~45° tip-up).
	/// </summary>
	[Export]
	public Vector3 GripLocalRotationDegrees { get; set; } = new(225f, 0f, -90f);

	/// <summary>
	/// Pose during the attack hit window — flatter / more extended cut.
	/// </summary>
	[Export]
	public Vector3 StrikeGripLocalRotationDegrees { get; set; } = new(160f, 0f, -90f);

	/// <summary>
	/// Slide along the blade axis (mesh +X). Positive pulls the handle into the palms
	/// when the pommel sits ahead of the hands.
	/// </summary>
	[Export]
	public float BladeSlideMeters { get; set; } = 0.32f;

	/// <summary>Slightly less slide on strike = tip reaches farther into the cut.</summary>
	[Export]
	public float StrikeBladeSlideMeters { get; set; } = 0.22f;

	[Export(PropertyHint.Range, "0.02,0.4,0.01")]
	public float StrikeBlendInNorm { get; set; } = 0.1f;

	[Export(PropertyHint.Range, "0.02,0.4,0.01")]
	public float StrikeBlendOutNorm { get; set; } = 0.14f;

	[Export]
	public PlayerAnimDriver? AnimDriver { get; set; }

	private Skeleton3D? _skeleton;
	private int _boneIdx = -1;
	private Node3D? _weapon;
	private float _fitScale = 1f;
	private Basis _gripBasis = Basis.Identity;
	private Vector3 _gripOrigin;
	private Quaternion _idleGripQuat = Quaternion.Identity;
	private Quaternion _strikeGripQuat = Quaternion.Identity;

	public override void _Ready()
	{
		AnimDriver ??= GetNodeOrNull<PlayerAnimDriver>("../AnimDriver");
		CallDeferred(nameof(AttachWeapon));
	}

	public override void _Process(double delta)
	{
		if (_skeleton == null || _weapon == null || _boneIdx < 0)
		{
			return;
		}

		float strike = AnimDriver?.GetWeaponStrikeWeight(StrikeBlendInNorm, StrikeBlendOutNorm) ?? 0f;
		ApplyGripPose(strike);

		// Bone pose is relative to skeleton; compose to world so anims drive the sword.
		// IMPORTANT: bake fit-scale into this transform — assigning GlobalTransform would
		// otherwise wipe Node3D.Scale and revive Sketchfab's x100 hierarchy scale.
		Transform3D boneWorld = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_boneIdx);
		Basis worldBasis = boneWorld.Basis * _gripBasis;
		worldBasis = worldBasis.Scaled(Vector3.One * _fitScale);
		_weapon.GlobalTransform = new Transform3D(worldBasis, boneWorld * _gripOrigin);
	}

	private void ApplyGripPose(float strikeWeight)
	{
		float w = Mathf.Clamp(strikeWeight, 0f, 1f);
		Quaternion q = _idleGripQuat.Slerp(_strikeGripQuat, w).Normalized();
		_gripBasis = new Basis(q);
		float slide = Mathf.Lerp(BladeSlideMeters, StrikeBladeSlideMeters, w);
		Vector3 alongBlade = _gripBasis * Vector3.Right;
		_gripOrigin = GripLocalPosition - alongBlade * slide;
	}

	private void AttachWeapon()
	{
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
			GD.PushWarning($"PlayerWeaponAttach: model missing at '{ModelPath}' (from {GetPath()}).");
			return;
		}

		_skeleton = FindSkeleton(modelRoot);
		if (_skeleton == null)
		{
			GD.PushWarning("PlayerWeaponAttach: Skeleton3D not found — weapon bind impossible.");
			return;
		}

		_boneIdx = ResolveBoneIndex(_skeleton);
		if (_boneIdx < 0)
		{
			GD.PushWarning($"PlayerWeaponAttach: hand bone not found. Sample bones: {DumpBoneNames(_skeleton)}");
			return;
		}

		_weapon = BuildNormalizedWeapon();
		if (_weapon == null)
		{
			return;
		}

		Node3D? visual = GetParent()?.GetNodeOrNull<Node3D>("Visual");
		(visual ?? GetParent())?.AddChild(_weapon);
		_weapon.Name = "Zweihander";

		_idleGripQuat = Quaternion.FromEuler(GripLocalRotationDegrees * (Mathf.Pi / 180f));
		_strikeGripQuat = Quaternion.FromEuler(StrikeGripLocalRotationDegrees * (Mathf.Pi / 180f));
		AnimDriver ??= GetNodeOrNull<PlayerAnimDriver>("../AnimDriver");
		ApplyGripPose(0f);

		GD.Print(
			$"PlayerWeaponAttach: bound to '{_skeleton.GetBoneName(_boneIdx)}', fitScale={_fitScale:0.####}, bladeSlide={BladeSlideMeters:0.##}.");
	}

	/// <summary>
	/// Instantiates the prop and re-parents only MeshInstance3Ds under a clean root,
	/// stripping Sketchfab scale=100 nodes that otherwise make a ~100m sword.
	/// </summary>
	private Node3D? BuildNormalizedWeapon()
	{
		Node3D? source = InstantiateWeapon();
		if (source == null)
		{
			return null;
		}

		var holder = new Node3D { Name = "ZweihanderHolder" };
		var meshes = new System.Collections.Generic.List<MeshInstance3D>();
		CollectMeshes(source, meshes);
		if (meshes.Count == 0)
		{
			// Fallback: keep hierarchy, compensate scale empirically.
			_fitScale = 0.0022f;
			holder.AddChild(source);
			return holder;
		}

		Aabb? localBounds = null;
		foreach (MeshInstance3D mi in meshes)
		{
			if (mi.Mesh == null)
			{
				continue;
			}

			MeshInstance3D copy = new()
			{
				Name = mi.Name,
				Mesh = mi.Mesh,
				MaterialOverride = mi.MaterialOverride,
				CastShadow = mi.CastShadow,
			};
			int surfaces = mi.Mesh.GetSurfaceCount();
			for (int i = 0; i < surfaces; i++)
			{
				Material? mat = mi.GetActiveMaterial(i);
				if (mat != null)
				{
					copy.SetSurfaceOverrideMaterial(i, mat);
				}
			}

			holder.AddChild(copy);
			Aabb meshAabb = mi.Mesh.GetAabb();
			localBounds = localBounds == null ? meshAabb : localBounds.Value.Merge(meshAabb);
		}

		source.QueueFree();

		if (localBounds != null)
		{
			Aabb bb = localBounds.Value;
			float longest = Mathf.Max(bb.Size.X, Mathf.Max(bb.Size.Y, bb.Size.Z));
			_fitScale = longest > 0.001f ? TargetLengthMeters / longest : 0.25f;

			// Shift so the grip sits near the bone: use the AABB end closest to origin
			// along the longest axis as a rough pommel/guard anchor.
			Vector3 center = bb.GetCenter();
			Vector3 gripShift = -center;
			if (bb.Size.X >= bb.Size.Y && bb.Size.X >= bb.Size.Z)
			{
				// Blade along X — pull so min-X (or nearer end to 0) is at grip.
				float pommelX = Mathf.Abs(bb.Position.X) <= Mathf.Abs(bb.End.X) ? bb.Position.X : bb.End.X;
				gripShift = new Vector3(-pommelX, -center.Y, -center.Z);
			}

			foreach (Node child in holder.GetChildren())
			{
				if (child is Node3D n3)
				{
					n3.Position = gripShift;
				}
			}
		}
		else
		{
			_fitScale = 0.25f;
		}

		return holder;
	}

	private static void CollectMeshes(Node node, System.Collections.Generic.List<MeshInstance3D> into)
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

		for (int i = 0; i < skeleton.GetBoneCount(); i++)
		{
			string name = skeleton.GetBoneName(i);
			if (!name.Contains("RightHand", System.StringComparison.OrdinalIgnoreCase))
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

	private Node3D? InstantiateWeapon()
	{
		if (WeaponScene != null)
		{
			return WeaponScene.Instantiate<Node3D>();
		}

		PackedScene? packed = GD.Load<PackedScene>(WeaponPath);
		if (packed == null)
		{
			GD.PushWarning($"PlayerWeaponAttach: failed to load '{WeaponPath}'.");
			return null;
		}

		return packed.Instantiate<Node3D>();
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
		var names = new System.Collections.Generic.List<string>();
		int count = Mathf.Min(skeleton.GetBoneCount(), 16);
		for (int i = 0; i < count; i++)
		{
			names.Add(skeleton.GetBoneName(i));
		}

		return string.Join(", ", names);
	}
}
