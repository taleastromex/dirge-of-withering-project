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
	public Vector3 GripLocalPosition { get; set; } = new(-0.01f, 0.1f, -0.01f);

	/// <summary>
	/// Bone-local euler (Godot YXZ). Idle / locomotion pose (~45° tip-up).
	/// </summary>
	[Export]
	public Vector3 GripLocalRotationDegrees { get; set; } = new(-120f, 90f, -75f);

	/// <summary>
	/// Pose during the attack hit window — flatter / more extended cut.
	/// </summary>
	[Export]
	public Vector3 StrikeGripLocalRotationDegrees { get; set; } = new(-120f, 90f, -75f);

	/// <summary>
	/// Roll mesh children around the handle (blade edge). Same role as enemy MeshPreRotation.
	/// </summary>
	[Export]
	public Vector3 MeshPreRotationDegrees { get; set; } = new(15f, 0f, 0f);

	/// <summary>
	/// Slide along the blade axis (mesh +X). Positive pulls the handle into the palms
	/// when the pommel sits ahead of the hands.
	/// </summary>
	[Export]
	public float BladeSlideMeters { get; set; } = 0.33f;

	/// <summary>Slightly less slide on strike = tip reaches farther into the cut.</summary>
	[Export]
	public float StrikeBladeSlideMeters { get; set; } = 0.23f;

	[Export(PropertyHint.Range, "0.02,0.4,0.01")]
	public float StrikeBlendInNorm { get; set; } = 0.1f;

	[Export(PropertyHint.Range, "0.02,0.4,0.01")]
	public float StrikeBlendOutNorm { get; set; } = 0.14f;

	[Export]
	public PlayerAnimDriver? AnimDriver { get; set; }

	/// <summary>
	/// Freezes player + T-pose; on-screen buttons + keys nudge grip. Print to Output.
	/// </summary>
	[Export]
	public bool GripTuneMode { get; set; }

	[Export]
	public float GripTuneStepDegrees { get; set; } = 15f;

	private Skeleton3D? _skeleton;
	private int _boneIdx = -1;
	private Node3D? _weapon;
	private float _fitScale = 1f;
	private Basis _gripBasis = Basis.Identity;
	private Vector3 _gripOrigin;
	private Quaternion _idleGripQuat = Quaternion.Identity;
	private Quaternion _strikeGripQuat = Quaternion.Identity;
	private Label? _gripTuneHud;
	private CanvasLayer? _gripTuneLayer;
	private string _lastInputDebug = "click buttons below (keys optional)";

	public override void _Ready()
	{
		AnimDriver ??= GetNodeOrNull<PlayerAnimDriver>("../AnimDriver");
		SetProcess(true);
		SetProcessInput(true);
		CallDeferred(nameof(AttachWeapon));
		if (GripTuneMode)
		{
			CallDeferred(nameof(BeginGripTuneMode));
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (!GripTuneMode)
		{
			return;
		}

		if (@event is InputEventKey key && key.Pressed && !key.Echo)
		{
			Key phys = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
			_lastInputDebug = $"KEY phys={phys} code={key.Keycode}";
			NudgeFromKey(phys, key.ShiftPressed);
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event is InputEventMouseButton mouse && mouse.Pressed)
		{
			float step = mouse.ShiftPressed ? GripTuneStepDegrees * 0.2f : GripTuneStepDegrees;
			if (mouse.ButtonIndex == MouseButton.WheelUp)
			{
				_lastInputDebug = "WHEEL+";
				NudgeGrip(new Vector3(step, 0f, 0f));
				GetViewport().SetInputAsHandled();
			}
			else if (mouse.ButtonIndex == MouseButton.WheelDown)
			{
				_lastInputDebug = "WHEEL-";
				NudgeGrip(new Vector3(-step, 0f, 0f));
				GetViewport().SetInputAsHandled();
			}
		}
	}

	private void NudgeFromKey(Key code, bool shift)
	{
		float step = GripTuneStepDegrees * (shift ? 0.2f : 1f);
		float posStep = 0.01f * (shift ? 0.25f : 1f);

		switch (code)
		{
			case Key.Q: NudgePre(-step); break;
			case Key.E: NudgePre(step); break;
			case Key.R:
			case Key.Left: NudgeGrip(new Vector3(-step, 0f, 0f)); break;
			case Key.F:
			case Key.Right: NudgeGrip(new Vector3(step, 0f, 0f)); break;
			case Key.T:
			case Key.Up: NudgeGrip(new Vector3(0f, -step, 0f)); break;
			case Key.G:
			case Key.Down: NudgeGrip(new Vector3(0f, step, 0f)); break;
			case Key.Y:
			case Key.Pageup: NudgeGrip(new Vector3(0f, 0f, -step)); break;
			case Key.H:
			case Key.Pagedown: NudgeGrip(new Vector3(0f, 0f, step)); break;
			case Key.U: NudgePos(new Vector3(-posStep, 0f, 0f)); break;
			case Key.I: NudgePos(new Vector3(posStep, 0f, 0f)); break;
			case Key.J: NudgePos(new Vector3(0f, -posStep, 0f)); break;
			case Key.K: NudgePos(new Vector3(0f, posStep, 0f)); break;
			case Key.N: NudgePos(new Vector3(0f, 0f, -posStep)); break;
			case Key.M: NudgePos(new Vector3(0f, 0f, posStep)); break;
			case Key.Comma: NudgeSlide(-posStep); break;
			case Key.Period: NudgeSlide(posStep); break;
			case Key.P: CommitGripTune(printOnly: true); break;
			default:
				UpdateGripTuneHud();
				break;
		}
	}

	private void NudgePre(float dx)
	{
		Vector3 pre = MeshPreRotationDegrees;
		pre.X += dx;
		MeshPreRotationDegrees = pre;
		CommitGripTune();
	}

	private void NudgeGrip(Vector3 d)
	{
		GripLocalRotationDegrees += d;
		CommitGripTune();
	}

	private void NudgePos(Vector3 d)
	{
		GripLocalPosition += d;
		CommitGripTune();
	}

	private void NudgeSlide(float d)
	{
		BladeSlideMeters += d;
		CommitGripTune();
	}

	private void CommitGripTune(bool printOnly = false)
	{
		if (!printOnly)
		{
			Vector3 pre = MeshPreRotationDegrees;
			Vector3 grip = GripLocalRotationDegrees;
			pre.X = WrapDegrees(pre.X);
			grip.X = WrapDegrees(grip.X);
			grip.Y = WrapDegrees(grip.Y);
			grip.Z = WrapDegrees(grip.Z);
			MeshPreRotationDegrees = pre;
			GripLocalRotationDegrees = grip;
			StrikeGripLocalRotationDegrees = grip;
			StrikeBladeSlideMeters = BladeSlideMeters;
		}

		RebuildGripQuats();
		ApplyMeshPreRotationToChildren();
		ApplyGripPose(0f);
		UpdateGripTuneHud();

		Vector3 g = GripLocalRotationDegrees;
		Vector3 p = GripLocalPosition;
		GD.Print(
			"[GripTune] " +
			$"preX={MeshPreRotationDegrees.X:0.#}  gripRot=({g.X:0.#}, {g.Y:0.#}, {g.Z:0.#})  " +
			$"pos=({p.X:0.###}, {p.Y:0.###}, {p.Z:0.###})  slide={BladeSlideMeters:0.###}  " +
			$"weapon={(_weapon != null ? "OK" : "NULL")}");
	}

	private void RebuildGripQuats()
	{
		_idleGripQuat = Quaternion.FromEuler(GripLocalRotationDegrees * (Mathf.Pi / 180f));
		_strikeGripQuat = Quaternion.FromEuler(StrikeGripLocalRotationDegrees * (Mathf.Pi / 180f));
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
		if (GetParent() is Player player)
		{
			player.ControlEnabled = false;
		}

		if (GetParent()?.GetNodeOrNull<PlayerAttack>("PlayerAttack") is PlayerAttack attack)
		{
			attack.AttackEnabled = false;
		}

		AnimDriver ??= GetNodeOrNull<PlayerAnimDriver>("../AnimDriver");
		if (AnimDriver != null)
		{
			AnimDriver.HoldRestPose = true;
			AnimDriver.ForceRestPose();
		}

		EnsureGripTuneHud();
		UpdateGripTuneHud();
		GD.Print(
			"[GripTune] Player T-pose. Use ON-SCREEN BUTTONS (keyboard often blocked by editor focus).\n" +
			$"  weapon={(_weapon != null ? _weapon.Name : "NULL")} bone={_boneIdx}");
	}

	private void EnsureGripTuneHud()
	{
		if (_gripTuneLayer != null && GodotObject.IsInstanceValid(_gripTuneLayer))
		{
			return;
		}

		_gripTuneLayer = new CanvasLayer { Name = "GripTuneHud", Layer = 100 };
		var root = new VBoxContainer
		{
			Position = new Vector2(12, 12),
			CustomMinimumSize = new Vector2(420, 0),
		};

		_gripTuneHud = new Label { Name = "GripTuneLabel", Text = "GRIPTUNE ON" };
		_gripTuneHud.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));
		_gripTuneHud.AddThemeFontSizeOverride("font_size", 18);
		root.AddChild(_gripTuneHud);

		root.AddChild(MakeTuneRow("preX", () => NudgePre(-GripTuneStepDegrees), () => NudgePre(GripTuneStepDegrees)));
		root.AddChild(MakeTuneRow("gripX", () => NudgeGrip(new Vector3(-GripTuneStepDegrees, 0f, 0f)), () => NudgeGrip(new Vector3(GripTuneStepDegrees, 0f, 0f))));
		root.AddChild(MakeTuneRow("gripY", () => NudgeGrip(new Vector3(0f, -GripTuneStepDegrees, 0f)), () => NudgeGrip(new Vector3(0f, GripTuneStepDegrees, 0f))));
		root.AddChild(MakeTuneRow("gripZ", () => NudgeGrip(new Vector3(0f, 0f, -GripTuneStepDegrees)), () => NudgeGrip(new Vector3(0f, 0f, GripTuneStepDegrees))));
		root.AddChild(MakeTuneRow("posX", () => NudgePos(new Vector3(-0.01f, 0f, 0f)), () => NudgePos(new Vector3(0.01f, 0f, 0f))));
		root.AddChild(MakeTuneRow("posY", () => NudgePos(new Vector3(0f, -0.01f, 0f)), () => NudgePos(new Vector3(0f, 0.01f, 0f))));
		root.AddChild(MakeTuneRow("posZ", () => NudgePos(new Vector3(0f, 0f, -0.01f)), () => NudgePos(new Vector3(0f, 0f, 0.01f))));
		root.AddChild(MakeTuneRow("slide", () => NudgeSlide(-0.01f), () => NudgeSlide(0.01f)));

		var printBtn = new Button { Text = "PRINT [GripTune] line" };
		printBtn.Pressed += () => CommitGripTune(printOnly: true);
		root.AddChild(printBtn);

		_gripTuneLayer.AddChild(root);
		GetTree()?.Root.AddChild(_gripTuneLayer);
	}

	private static HBoxContainer MakeTuneRow(string label, System.Action onMinus, System.Action onPlus)
	{
		var row = new HBoxContainer();
		var name = new Label { Text = label, CustomMinimumSize = new Vector2(70, 0) };
		var minus = new Button { Text = "−", CustomMinimumSize = new Vector2(44, 32) };
		var plus = new Button { Text = "+", CustomMinimumSize = new Vector2(44, 32) };
		minus.Pressed += onMinus;
		plus.Pressed += onPlus;
		row.AddChild(name);
		row.AddChild(minus);
		row.AddChild(plus);
		return row;
	}

	private void UpdateGripTuneHud()
	{
		if (_gripTuneHud == null || !GodotObject.IsInstanceValid(_gripTuneHud))
		{
			return;
		}

		Vector3 g = GripLocalRotationDegrees;
		Vector3 p = GripLocalPosition;
		bool focused = GetWindow()?.HasFocus() ?? false;
		string weapon = _weapon != null ? "OK" : "NULL";
		_gripTuneHud.AddThemeColorOverride(
			"font_color",
			weapon == "OK" ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.25f, 0.2f));
		_gripTuneHud.Text =
			$"GRIPTUNE  weapon={weapon}  focus={(focused ? "YES" : "NO")}\n" +
			$"preX={MeshPreRotationDegrees.X:0.#}  grip=({g.X:0.#},{g.Y:0.#},{g.Z:0.#})\n" +
			$"pos=({p.X:0.###},{p.Y:0.###},{p.Z:0.###})  slide={BladeSlideMeters:0.###}\n" +
			$"last: {_lastInputDebug}";
	}

	public override void _Process(double delta)
	{
		if (GripTuneMode)
		{
			AnimDriver?.ForceRestPose();
			UpdateGripTuneHud();
		}

		if (_skeleton == null || _weapon == null || _boneIdx < 0)
		{
			return;
		}

		float strike = GripTuneMode
			? 0f
			: AnimDriver?.GetWeaponStrikeWeight(StrikeBlendInNorm, StrikeBlendOutNorm) ?? 0f;

		if (GripTuneMode)
		{
			RebuildGripQuats();
		}

		ApplyGripPose(strike);

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
				mi.Transform = new Transform3D(align, mi.Position);
			}
		}
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

		RebuildGripQuats();
		AnimDriver ??= GetNodeOrNull<PlayerAnimDriver>("../AnimDriver");
		ApplyMeshPreRotationToChildren();
		ApplyGripPose(0f);

		GD.Print(
			$"PlayerWeaponAttach: bound to '{_skeleton.GetBoneName(_boneIdx)}', fitScale={_fitScale:0.####}, bladeSlide={BladeSlideMeters:0.##}, GripTune={GripTuneMode}.");

		if (GripTuneMode)
		{
			EnsureGripTuneHud();
			UpdateGripTuneHud();
		}
	}

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

			Vector3 center = bb.GetCenter();
			Vector3 gripShift = -center;
			if (bb.Size.X >= bb.Size.Y && bb.Size.X >= bb.Size.Z)
			{
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
