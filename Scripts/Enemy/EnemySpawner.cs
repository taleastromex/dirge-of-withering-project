using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Респавн одного врага на точке — для прогона баланса Core Loop.
/// </summary>
public partial class EnemySpawner : Node3D
{
	[Export]
	public PackedScene? EnemyScene { get; set; }

	[Export]
	public float RespawnDelay { get; set; } = 2.5f;

	[Export]
	public bool SpawnOnReady { get; set; } = true;

	private bool _respawnScheduled;
	private Node? _aliveEnemy;

	public override void _Ready()
	{
		if (SpawnOnReady)
		{
			// Нельзя AddChild во время расстановки детей родителя.
			CallDeferred(MethodName.Spawn);
		}
	}

	public void Spawn()
	{
		_respawnScheduled = false;

		if (EnemyScene == null)
		{
			GD.PushWarning("EnemySpawner: EnemyScene не назначен.");
			return;
		}

		if (_aliveEnemy != null && GodotObject.IsInstanceValid(_aliveEnemy))
		{
			return;
		}

		Node instance = EnemyScene.Instantiate();
		Node parent = GetParent() ?? GetTree().CurrentScene ?? this;
		parent.AddChild(instance);

		if (instance is Node3D node3D)
		{
			node3D.GlobalTransform = GlobalTransform;
		}

		_aliveEnemy = instance;
		instance.TreeExiting += OnEnemyTreeExiting;
	}

	private void OnEnemyTreeExiting()
	{
		_aliveEnemy = null;
		ScheduleRespawn();
	}

	private void ScheduleRespawn()
	{
		if (_respawnScheduled)
		{
			return;
		}

		_respawnScheduled = true;
		GetTree().CreateTimer(RespawnDelay).Timeout += Spawn;
	}
}
