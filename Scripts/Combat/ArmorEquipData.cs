using Godot;

namespace DirgeOfWithering;

[GlobalClass]
public partial class ArmorEquipData : Resource
{
	[Export]
	public string DisplayName { get; set; } = "";

	/// <summary>Absolute PhysicalResist 0…0.95 (replaces, does not stack).</summary>
	[Export(PropertyHint.Range, "0,0.95,0.01")]
	public float PhysicalResist { get; set; } = 0.2f;

	[Export]
	public float Weight { get; set; } = 10f;
}
