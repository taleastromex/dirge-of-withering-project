namespace DirgeOfWithering;

/// <summary>
/// Физические слои 3D (битовые маски Godot, слой N = 1 &lt;&lt; (N-1)).
/// </summary>
public static class CombatLayers
{
	public const uint World = 1u << 0;
	public const uint Player = 1u << 1;
	public const uint Enemy = 1u << 2;
}
