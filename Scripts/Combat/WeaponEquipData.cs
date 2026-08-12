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

	[Export]
	public bool AlignLongestAxisToX { get; set; }

	[Export]
	public bool RecenterGripToPommel { get; set; }

	/// <summary>Fit longest AABB axis to this length; &lt;=0 keeps authored scale when socket-authored.</summary>
	[Export]
	public float TargetLengthMeters { get; set; }

	public Vector3 ResolveStrikeRotationDegrees()
	{
		if (StrikeLocalRotationDegrees.LengthSquared() < 0.0001f)
		{
			return LocalRotationDegrees;
		}

		return StrikeLocalRotationDegrees;
	}
}
