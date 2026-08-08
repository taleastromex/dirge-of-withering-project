using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Алтарь-заглушка Vertical Slice: рядом со стоящим игроком снижает Скверну.
/// Работает и в OVERLOAD (единственный надёжный сброс).
/// </summary>
public partial class BlightAltar : Area3D
{
	[Export]
	public float CleansePerSecond { get; set; } = 38f;

	[Export]
	public float EnterBurstCleanse { get; set; } = 22f;

	[Export]
	public MeshInstance3D? VisualMesh { get; set; }

	private Blight? _playerBlight;
	private StandardMaterial3D? _glowMaterial;
	private float _pulse;

	public override void _Ready()
	{
		Monitoring = true;
		Monitorable = false;
		CollisionLayer = 0;
		CollisionMask = CombatLayers.Player;

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;

		VisualMesh ??= GetParent()?.GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
		if (VisualMesh?.GetActiveMaterial(0) is StandardMaterial3D shared)
		{
			_glowMaterial = (StandardMaterial3D)shared.Duplicate();
			VisualMesh.MaterialOverride = _glowMaterial;
			_glowMaterial.EmissionEnabled = true;
			_glowMaterial.Emission = ErrengardPalette.BoneYellow;
			_glowMaterial.EmissionEnergyMultiplier = 0.35f;
		}
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_pulse += dt * 3f;

		if (_glowMaterial != null)
		{
			float baseEnergy = _playerBlight != null ? 0.85f : 0.35f;
			_glowMaterial.EmissionEnergyMultiplier = baseEnergy + Mathf.Sin(_pulse) * 0.15f;
		}

		if (_playerBlight == null || !GodotObject.IsInstanceValid(_playerBlight))
		{
			return;
		}

		if (CleansePerSecond > 0f)
		{
			_playerBlight.Remove(CleansePerSecond * dt);
		}
	}

	private void OnBodyEntered(Node3D body)
	{
		if (body is not Player player)
		{
			return;
		}

		_playerBlight = player.GetNodeOrNull<Blight>("Blight");
		if (_playerBlight != null && EnterBurstCleanse > 0f)
		{
			_playerBlight.Remove(EnterBurstCleanse);
		}
	}

	private void OnBodyExited(Node3D body)
	{
		if (body is Player)
		{
			_playerBlight = null;
		}
	}
}
