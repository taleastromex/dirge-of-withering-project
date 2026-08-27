using Godot;

namespace DirgeOfWithering;

/// <summary>Where a prop/armor piece mounts on a humanoid (inventory / equipment).</summary>
public enum EquipSlot
{
	None = 0,
	RightHand = 1,
	LeftHand = 2,
	Back = 3,
	Hip = 4,
}

public enum WeaponStance
{
	TwoHandGreat = 0,
	OneHand = 1,
}

/// <summary>
/// Authoring data for a held/worn prop. Preferred path: mesh already baked in socket space
/// (<see cref="SocketAuthored"/>) so Local* are identity — same pattern as TES/Gothic equip.
/// Runtime attach only parents to <see cref="BoneName"/>; no per-frame euler hacks.
/// </summary>
[GlobalClass]
public partial class WeaponEquipData : Resource
{
	[Export]
	public string DisplayName { get; set; } = "";

	[Export]
	public EquipSlot Slot { get; set; } = EquipSlot.RightHand;

	/// <summary>Skeleton bone (Mixamo: mixamorig:RightHand).</summary>
	[Export]
	public string BoneName { get; set; } = "mixamorig:RightHand";

	[Export]
	public PackedScene? WeaponScene { get; set; }

	/// <summary>PackedScene (.glb) path if <see cref="WeaponScene"/> is unset.</summary>
	[Export]
	public string WeaponPath { get; set; } = "";

	[Export]
	public string WeaponNodeName { get; set; } = "Weapon";

	/// <summary>
	/// When true, the GLB is already posed for this bone (equip with identity local xform).
	/// When false, use Local* offsets (migration / legacy props only).
	/// </summary>
	[Export]
	public bool SocketAuthored { get; set; } = true;

	[Export]
	public Vector3 LocalPosition { get; set; }

	/// <summary>Godot Euler YXZ, degrees.</summary>
	[Export]
	public Vector3 LocalRotationDegrees { get; set; }

	/// <summary>Optional strike pose; if zero-length delta from Local, unused.</summary>
	[Export]
	public Vector3 StrikeLocalRotationDegrees { get; set; }

	/// <summary>Legacy: slide handle into palm (ignored when SocketAuthored).</summary>
	[Export]
	public float BladeSlideMeters { get; set; }

	[Export]
	public float StrikeBladeSlideMeters { get; set; }

	[Export]
	public Vector3 MeshPreRotationDegrees { get; set; }

	/// <summary>Blade-edge roll on the sheathe bone. Independent from <see cref="MeshPreRotationDegrees"/>.</summary>
	[Export]
	public Vector3 SheatheMeshPreRotationDegrees { get; set; }

	[Export]
	public bool AlignLongestAxisToX { get; set; }

	[Export]
	public bool RecenterGripToPommel { get; set; }

	/// <summary>Fit longest AABB axis to this length; &lt;=0 keeps authored scale when socket-authored.</summary>
	[Export]
	public float TargetLengthMeters { get; set; }

	[Export]
	public int BaseDamageN { get; set; } = 40;

	[Export]
	public float Weight { get; set; } = 8f;

	[Export]
	public WeaponStance Stance { get; set; } = WeaponStance.TwoHandGreat;

	[Export]
	public string SheatheBoneName { get; set; } = "mixamorig:Spine2";

	[Export]
	public Vector3 SheatheLocalPosition { get; set; }

	[Export]
	public Vector3 SheatheLocalRotationDegrees { get; set; } = new(0f, 0f, 90f);

	/// <summary>Handle slide on the sheathe bone. 0 = use SheatheLocalPosition only.</summary>
	[Export]
	public float SheatheBladeSlideMeters { get; set; }

	[Export]
	public string IdleClip { get; set; } = "";

	[Export]
	public string WalkClip { get; set; } = "";

	[Export]
	public string RunClip { get; set; } = "";

	[Export]
	public string AttackClip { get; set; } = "";

	[Export]
	public string HeavyAttackClip { get; set; } = "";

	[Export]
	public string HurtClip { get; set; } = "";

	[Export]
	public string DrawClip { get; set; } = "";

	[Export]
	public string SheatheClip { get; set; } = "";

	/// <summary>
	/// Draw clip: keep the prop on the sheathe bone until this normalized time, then bind to the hand.
	/// </summary>
	[Export(PropertyHint.Range, "0,1,0.01")]
	public float DrawBindNorm { get; set; } = 0.62f;

	/// <summary>
	/// Sheathe clip: keep the prop on the hand until this normalized time, then bind to the sheathe bone.
	/// </summary>
	[Export(PropertyHint.Range, "0,1,0.01")]
	public float SheatheBindNorm { get; set; } = 0.84f;

	/// <summary>Normalized [start,end] pulses for the current heavy clip (e.g. 3-hit 2H slash).</summary>
	[Export]
	public Vector2[] HeavyHitWindows { get; set; } = System.Array.Empty<Vector2>();

	/// <summary>Normalized [start,end] pulses for the light clip. Empty = AnimDriver default window.</summary>
	[Export]
	public Vector2[] LightHitWindows { get; set; } = System.Array.Empty<Vector2>();

	[Export(PropertyHint.Range, "0.5,3,0.05")]
	public float AttackSpeedScale { get; set; } = 1f;

	[Export(PropertyHint.Range, "0.5,3,0.05")]
	public float HeavyAttackSpeedScale { get; set; } = 1f;

	public Vector3 ResolveStrikeRotationDegrees()
	{
		if (StrikeLocalRotationDegrees.LengthSquared() < 0.0001f)
		{
			return LocalRotationDegrees;
		}

		return StrikeLocalRotationDegrees;
	}
}
