using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Примитивный враг Core Loop: детект → подход → телеграф → удар.
/// Есть stagger от урона и knockback.
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
		Stagger,
		Dead
	}

	[Export]
	public float MoveSpeed { get; set; } = 3.35f;

	[Export]
	public float DetectRange { get; set; } = 12f;

	[Export]
	public float AttackRange { get; set; } = 1.55f;

	[Export]
	public float TelegraphTime { get; set; } = 0.45f;

	[Export]
	public float AttackActiveTime { get; set; } = 0.14f;

	[Export]
	public float RecoveryTime { get; set; } = 0.6f;

	[Export]
	public float StaggerTime { get; set; } = 0.28f;

	[Export]
	public int AttackDamage { get; set; } = 20;

	[Export]
	public float AttackKnockback { get; set; } = 7f;

	[Export]
	public float AttackHitStop { get; set; } = 0.045f;

	[Export]
	public float KnockbackDuration { get; set; } = 0.14f;

	[Export]
	public float DeathDespawnDelay { get; set; } = 0.55f;

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
	private Color _staggerColor = new(0.75f, 0.7f, 0.45f, 1f);

	private Vector3 _knockbackVelocity;
	private float _knockbackTimer;

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
			ApplyHitboxTuning();
			Hitbox.SetActive(false);
		}

		// Материал из .tscn общий на все инстансы — дублируем, иначе stagger/телеграф красит всех.
		EnsureUniqueBodyMaterial();

		if (Health != null)
		{
			Health.Died += OnDied;
			Health.Damaged += OnDamaged;
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

		if (_knockbackTimer > 0f)
		{
			_knockbackTimer -= dt;
			Velocity = new Vector3(_knockbackVelocity.X, Velocity.Y, _knockbackVelocity.Z);
			_knockbackVelocity = _knockbackVelocity.MoveToward(Vector3.Zero, 35f * dt);
			MoveAndSlide();

			if (_state == AiState.Stagger)
			{
				_stateTimer -= dt;
				if (_stateTimer <= 0f && _knockbackTimer <= 0f)
				{
					SetBodyColor(_baseColor);
					_state = IsPlayerInRange(DetectRange) ? AiState.Chase : AiState.Idle;
				}
			}

			return;
		}

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

			case AiState.Stagger:
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

	public void ApplyKnockback(Vector3 sourcePosition, float force)
	{
		if (_state == AiState.Dead || force <= 0f)
		{
			return;
		}

		Vector3 dir = GlobalPosition - sourcePosition;
		dir.Y = 0f;
		if (dir.LengthSquared() < 0.0001f)
		{
			dir = Vector3.Forward;
		}

		_knockbackVelocity = dir.Normalized() * force;
		_knockbackTimer = KnockbackDuration;
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
		ApplyHitboxTuning();
		Hitbox?.SetActive(true);
	}

	private void BeginRecovery()
	{
		_state = AiState.Recovery;
		_stateTimer = RecoveryTime;
		Hitbox?.SetActive(false);
	}

	private void BeginStagger()
	{
		_state = AiState.Stagger;
		_stateTimer = StaggerTime;
		Hitbox?.SetActive(false);
		SetBodyColor(_staggerColor);
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

	private void EnsureUniqueBodyMaterial()
	{
		if (BodyMesh == null)
		{
			return;
		}

		Material? source = BodyMesh.GetActiveMaterial(0) ?? BodyMesh.MaterialOverride;
		if (source is StandardMaterial3D shared)
		{
			_bodyMaterial = (StandardMaterial3D)shared.Duplicate();
			_baseColor = _bodyMaterial.AlbedoColor;
		}
		else
		{
			_bodyMaterial = new StandardMaterial3D { AlbedoColor = _baseColor, Roughness = 0.9f };
		}

		BodyMesh.MaterialOverride = _bodyMaterial;
	}

	private void SetBodyColor(Color color)
	{
		if (_bodyMaterial != null)
		{
			_bodyMaterial.AlbedoColor = color;
		}
	}

	private void ApplyHitboxTuning()
	{
		if (Hitbox == null)
		{
			return;
		}

		Hitbox.Damage = AttackDamage;
		Hitbox.KnockbackForce = AttackKnockback;
		Hitbox.HitStopSeconds = AttackHitStop;
	}

	private void OnDamaged(int amount, Vector3 sourcePosition)
	{
		if (_state == AiState.Dead)
		{
			return;
		}

		// Прерываем телеграф/удар — читаемый ответ на попадание.
		BeginStagger();
	}

	private void OnDied()
	{
		_state = AiState.Dead;
		Velocity = Vector3.Zero;
		_knockbackTimer = 0f;
		Hitbox?.SetActive(false);
		SetBodyColor(new Color(0.15f, 0.12f, 0.12f, 1f));

		CollisionLayer = 0;
		CollisionMask = 0;

		if (Visual != null)
		{
			Visual.Visible = false;
		}

		GetTree().CreateTimer(DeathDespawnDelay).Timeout += QueueFree;
	}
}
