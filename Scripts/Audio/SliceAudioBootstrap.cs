using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Starts cathedral ambient when the slice scene loads.
/// </summary>
public partial class SliceAudioBootstrap : Node
{
	[Export]
	public string AmbientPath { get; set; } = SliceAudioIds.Ambient;

	public override void _Ready()
	{
		GameAudio.Instance?.PlayAmbient(AmbientPath, loop: true);
	}
}
