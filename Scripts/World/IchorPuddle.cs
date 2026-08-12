using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Лужа Ихора: стоя в зоне игрок набирает FILTH (Concepts/DAMAGE.md).
/// </summary>
public partial class IchorPuddle : Area3D
{
	[Export]
	public float FilthPerSecond { get; set; } = 4f;

	private BlightController? _blightController;

	public override void _Ready()
	{
		Monitoring = true;
		Monitorable = false;
		CollisionLayer = 0;
		CollisionMask = CombatLayers.Player;

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	public override void _Process(double delta)
	{
		if (_blightController == null || !GodotObject.IsInstanceValid(_blightController))
		{
			return;
		}

		if (FilthPerSecond <= 0f)
		{
			return;
		}

		_blightController.NotifyFilthFromHit(FilthPerSecond * (float)delta);
	}

	private void OnBodyEntered(Node3D body)
	{
		if (body is not Player player)
		{
			return;
		}

		_blightController = player.GetNodeOrNull<BlightController>("BlightController");
	}

	private void OnBodyExited(Node3D body)
	{
		if (body is Player)
		{
			_blightController = null;
		}
	}
}
