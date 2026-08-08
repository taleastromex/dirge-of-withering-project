using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Примитивный враг Core Loop: детект → подход по прямой → телеграф → удар.
/// Без NavMesh.
/// </summary>
public partial class BasicEnemy : CharacterBody3D
{
	private enum AiState
	{
		Idle,
		Chase,
		Telegraph,
		Attack,
		Recovery,
		Dead
	}

	[Export]
	public float MoveSpeed { get; set; } = 3.5f;

	[Export]
	public float DetectRange { get; set; } = 12f;

	[Export]
	public float AttackRange { get; set; } = 1.6f;

	[Export]
	public float TelegraphTime { get; set; } = 0.4f;

	[Export]
	public float AttackActiveTime { get; set; } = 0.15f;

	[Export]
	public float RecoveryTime { get; set; } = 0.55f;

	[Export]
	public int AttackDamage { get; set; } = 20;

	[Export]
	public Health? Health { get; set; }

	[Export]
	public Hitbox3D? Hitbox { get; set; }

	[Export]
	public Node3D? Visual { get; set; }

	[Export]
	public MeshInstance3D? BodyMesh { get; set; }

	private AiState _state = AiState.Idle;
	private float _stateTimer;
	private Node3D? _player;
	private StandardMaterial3D? _bodyMaterial;
	private Color _baseColor = new(0.35f, 0.4f, 0.28f, 1f);
	private Color _telegraphColor = new(0.85f, 0.25f, 0.15f, 1f);

	public override void _Ready()
	{
		AddToGroup("enemies");

		Health ??= GetNodeOrNull<Health>("Health");
		Hitbox ??= GetNodeOrNull<Hitbox3D>("Visual/Hitbox");
		Visual ??= GetNodeOrNull<Node3D>("Visual");
		BodyMesh ??= GetNodeOrNull<MeshInstance3D>("Visual/Body");

		if (Hitbox != null)
		{
			Hitbox.OwnerRoot = this;
			Hitbox.Damage = AttackDamage;
			Hitbox.SetActive(false);
		}

		if (BodyMesh?.GetActiveMaterial(0) is StandardMaterial3D mat)
		{
			_bodyMaterial = mat;
			_baseColor = mat.AlbedoColor;
		}
		else if (BodyMesh != null)
		{
			_bodyMaterial = new StandardMaterial3D { AlbedoColor = _baseColor, Roughness = 0.9f };
			BodyMesh.MaterialOverride = _bodyMaterial;
		}

		if (Health != null)
		{
			Health.Died += OnDied;
		}

		_player = GetTree().GetFirstNodeInGroup("player") as Node3D;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_state == AiState.Dead)
		{
			return;
		}

		_player ??= GetTree().GetFirstNodeInGroup("player") as Node3D;
		float dt = (float)delta;

		switch (_state)
		{
			case AiState.Idle:
				Velocity = new Vector3(0f, Velocity.Y, 0f);
				if (IsPlayerInRange(DetectRange))
				{
					_state = AiState.Chase;
				}
				break;

			case AiState.Chase:
				if (_player == null || !IsPlayerAlive())
				{
					_state = AiState.Idle;
					break;
				}

				if (!IsPlayerInRange(DetectRange * 1.25f))
				{
					_state = AiState.Idle;
					Velocity = new Vector3(0f, Velocity.Y, 0f);
					break;
				}

				if (DistanceToPlayer() <= AttackRange)
				{
					BeginTelegraph();
					break;
				}

				ChasePlayer();
				break;

			case AiState.Telegraph:
				Velocity = new Vector3(0f, Velocity.Y, 0f);
				FacePlayer();
				_stateTimer -= dt;
				if (_stateTimer <= 0f)
				{
					BeginAttack();
				}
				break;

			case AiState.Attack:
				Velocity = new Vector3(0f, Velocity.Y, 0f);
				_stateTimer -= dt;
				if (_stateTimer <= 0f)
				{
					BeginRecovery();
				}
				break;

			case AiState.Recovery:
				Velocity = new Vector3(0f, Velocity.Y, 0f);
				_stateTimer -= dt;
				if (_stateTimer <= 0f)
				{
					SetBodyColor(_baseColor);
					_state = IsPlayerInRange(DetectRange) ? AiState.Chase : AiState.Idle;
				}
				break;
		}

		MoveAndSlide();
	}

	private void BeginTelegraph()
	{
		_state = AiState.Telegraph;
		_stateTimer = TelegraphTime;
		Velocity = new Vector3(0f, Velocity.Y, 0f);
		Hitbox?.SetActive(false);
		SetBodyColor(_telegraphColor);
		FacePlayer();
	}

	private void BeginAttack()
	{
		_state = AiState.Attack;
		_stateTimer = AttackActiveTime;
		FacePlayer();

		if (Hitbox != null)
		{
			Hitbox.Damage = AttackDamage;
			Hitbox.SetActive(true);
		}
	}

	private void BeginRecovery()
	{
		_state = AiState.Recovery;
		_stateTimer = RecoveryTime;
		Hitbox?.SetActive(false);
	}

	private void ChasePlayer()
	{
		if (_player == null)
		{
			return;
		}

		Vector3 toPlayer = _player.GlobalPosition - GlobalPosition;
		toPlayer.Y = 0f;
		if (toPlayer.LengthSquared() < 0.0001f)
		{
			Velocity = new Vector3(0f, Velocity.Y, 0f);
			return;
		}

		Vector3 dir = toPlayer.Normalized();
		Velocity = new Vector3(dir.X * MoveSpeed, Velocity.Y, dir.Z * MoveSpeed);
		FaceDirection(dir);
	}

	private void FacePlayer()
	{
		if (_player == null)
		{
			return;
		}

		Vector3 toPlayer = _player.GlobalPosition - GlobalPosition;
		toPlayer.Y = 0f;
		if (toPlayer.LengthSquared() < 0.0001f)
		{
			return;
		}

		FaceDirection(toPlayer.Normalized());
	}

	private void FaceDirection(Vector3 direction)
	{
		if (Visual == null)
		{
			return;
		}

		Vector3 lookAt = Visual.GlobalPosition + direction;
		lookAt.Y = Visual.GlobalPosition.Y;
		Visual.LookAt(lookAt, Vector3.Up);
	}

	private bool IsPlayerInRange(float range)
	{
		return _player != null && IsPlayerAlive() && DistanceToPlayer() <= range;
	}

	private bool IsPlayerAlive()
	{
		if (_player == null)
		{
			return false;
		}

		Health? playerHealth = _player.GetNodeOrNull<Health>("Health");
		return playerHealth == null || !playerHealth.IsDead;
	}

	private float DistanceToPlayer()
	{
		if (_player == null)
		{
			return float.MaxValue;
		}

		Vector3 a = GlobalPosition;
		Vector3 b = _player.GlobalPosition;
		a.Y = 0f;
		b.Y = 0f;
		return a.DistanceTo(b);
	}

	private void SetBodyColor(Color color)
	{
		if (_bodyMaterial != null)
		{
			_bodyMaterial.AlbedoColor = color;
		}
	}

	private void OnDied()
	{
		_state = AiState.Dead;
		Velocity = Vector3.Zero;
		Hitbox?.SetActive(false);
		SetBodyColor(new Color(0.15f, 0.12f, 0.12f, 1f));

		CollisionLayer = 0;
		CollisionMask = 0;

		if (Visual != null)
		{
			Visual.Visible = false;
		}

		// Короткая пауза, затем удаление — удобно для Core Loop.
		GetTree().CreateTimer(0.8f).Timeout += QueueFree;
	}
}
