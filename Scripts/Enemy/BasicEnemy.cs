using System.Collections.Generic;
using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Базовый враг: детект → подход → телеграф → удар → recovery.
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
	public string DisplayName { get; set; } = "Enemy";

	[Export]
	public float MoveSpeed { get; set; } = 3.35f;

	[Export]
	public float DetectRange { get; set; } = 12f;

	[Export]
	public float AttackRange { get; set; } = 1.55f;

	[Export]
	public float TelegraphTime { get; set; } = 0.4f;

	/// <summary>Пауза после анимации удара, пока хитбокс выключен.</summary>
	[Export]
	public float RecoveryTime { get; set; } = 0.45f;

	[Export]
	public float StaggerTime { get; set; } = 0.35f;

	[Export]
	public int AttackDamage { get; set; } = 20;

	[Export]
	public float AttackKnockback { get; set; } = 7f;

	[Export]
	public float AttackHitStop { get; set; } = 0.045f;

	/// <summary>Множитель урона для attack_heavy.</summary>
	[Export]
	public float HeavyAttackDamageMul { get; set; } = 1.35f;

	/// <summary>Множитель knockback для attack_heavy.</summary>
	[Export]
	public float HeavyAttackKnockbackMul { get; set; } = 1.25f;

	/// <summary>Множитель урона для attack_alt (быстрый удар).</summary>
	[Export]
	public float AltAttackDamageMul { get; set; } = 0.85f;

	/// <summary>Длительность выпада вперёд на attack_heavy (прыжок).</summary>
	[Export]
	public float HeavyLungeDuration { get; set; } = 1.75f;

	/// <summary>Базовая скорость выпада на attack_heavy (м/с); факт. скорость подгоняется под цель.</summary>
	[Export]
	public float HeavyLungeSpeed { get; set; } = 5.5f;

	/// <summary>Нижняя граница множителя скорости выпада при прицеливании в хитбокс.</summary>
	[Export(PropertyHint.Range, "0.1,1,0.05")]
	public float HeavyLungeSpeedMinMul { get; set; } = 0.45f;

	/// <summary>Верхняя граница множителя скорости выпада при прицеливании в хитбокс.</summary>
	[Export(PropertyHint.Range, "1,2,0.05")]
	public float HeavyLungeSpeedMaxMul { get; set; } = 1.2f;

	/// <summary>Шанс взять heavy, когда дистанция позволяет попасть хитбоксом.</summary>
	[Export(PropertyHint.Range, "0,1,0.05")]
	public float HeavyAttackPreferChance { get; set; } = 0.7f;

	[Export]
	public float KnockbackDuration { get; set; } = 0.14f;

	[Export]
	public float DeathDespawnDelay { get; set; } = 3.0f;

	/// <summary>Множитель скорости, если у игрока HIGH/OVERLOAD Скверна.</summary>
	[Export]
	public float HighBlightSpeedMul { get; set; } = 1.28f;

	/// <summary>Множитель длительности телеграфа при HIGH Скверне игрока (&lt; 1 = быстрее).</summary>
	[Export]
	public float HighBlightTelegraphMul { get; set; } = 0.75f;

	/// <summary>Запас, если AnimationPlayer ещё не отдал длину клипа.</summary>
	[Export]
	public float AttackFallbackDuration { get; set; } = 1.1f;

	/// <summary>Скорость поворота Visual к цели (выше = резче).</summary>
	[Export]
	public float FaceTurnSpeed { get; set; } = 7f;

	/// <summary>Пепельно-багровый тинт под палитру собора (умножается на albedo).</summary>
	[Export]
	public Color AshTint { get; set; } = new(0.82f, 0.74f, 0.7f, 1f);

	[Export]
	public Health? Health { get; set; }

	[Export]
	public Hitbox3D? Hitbox { get; set; }

	[Export]
	public Node3D? Visual { get; set; }

	[Export]
	public MeshInstance3D? BodyMesh { get; set; }

	[Export]
	public EnemyAnimDriver? AnimDriver { get; set; }

	[Export]
	public EnemyNameplate? Nameplate { get; set; }

	[Export]
	public float FootstepInterval { get; set; } = 0.48f;

	[Export]
	public float FootstepMinSpeed { get; set; } = 0.55f;

	[Export]
	public float SpatialSfxMaxDistance { get; set; } = 24f;

	private AiState _state = AiState.Idle;
	private float _stateTimer;
	private Node3D? _player;
	private readonly List<BaseMaterial3D> _tintedMaterials = new();
	private readonly List<Color> _baseAlbedos = new();

	private Vector3 _knockbackVelocity;
	private float _knockbackTimer;
	private bool _hitboxWasActive;
	private bool _deathSettling;
	private float _deathGroundOffsetApplied;
	private Skeleton3D? _deathSkeleton;
	private float _footstepTimer;
	private float _lungeTimer;
	private float _lungeSpeed;
	private Vector3 _lungeDir = Vector3.Forward;

	/// <summary>Скорость опускания трупа к полу (м/с), если death без root motion по Y.</summary>
	[Export]
	public float DeathSettleSpeed { get; set; } = 7.5f;

	/// <summary>Зазор кости/пола, при котором труп считаем приземлённым в этом кадре.</summary>
	[Export]
	public float DeathFloorClearance { get; set; } = 0.04f;

	public override void _Ready()
	{
		AddToGroup("enemies");

		Health ??= GetNodeOrNull<Health>("Health");
		Hitbox ??= GetNodeOrNull<Hitbox3D>("Visual/Hitbox");
		Visual ??= GetNodeOrNull<Node3D>("Visual");
		BodyMesh ??= GetNodeOrNull<MeshInstance3D>("Visual/Body");
		AnimDriver ??= GetNodeOrNull<EnemyAnimDriver>("AnimDriver");
		Nameplate ??= GetNodeOrNull<EnemyNameplate>("Nameplate");
		Nameplate?.SetTitle(DisplayName);
		UpdateNameplateVisibility();

		if (Hitbox != null)
		{
			Hitbox.OwnerRoot = this;
			ApplyHitboxTuning();
			Hitbox.SetActive(false);
		}

		ApplyAshTintToMeshes();

		if (Health != null)
		{
			Health.Died += OnDied;
			Health.Damaged += OnDamaged;
		}

		_player = GetTree().GetFirstNodeInGroup("player") as Node3D;
		CallDeferred(MethodName.EnterIdle);
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		if (_state == AiState.Dead)
		{
			if (_deathSettling)
			{
				SettleCorpseToGround(dt);
			}

			return;
		}

		_player ??= GetTree().GetFirstNodeInGroup("player") as Node3D;

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
					RestoreTint();
					ResumeAfterInterrupt();
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
					EnterChase();
				}
				break;

			case AiState.Chase:
				if (_player == null || !IsPlayerAlive())
				{
					EnterIdle();
					break;
				}

				if (!IsPlayerInRange(DetectRange * 1.25f))
				{
					EnterIdle();
					break;
				}

				if (ShouldBeginAttack())
				{
					BeginTelegraph();
					break;
				}

				ChasePlayer(dt);
				UpdateChaseFootsteps(dt);
				break;

			case AiState.Telegraph:
				Velocity = new Vector3(0f, Velocity.Y, 0f);
				FacePlayer(dt);
				_stateTimer -= dt;
				if (_stateTimer <= 0f)
				{
					BeginAttack();
				}
				break;

			case AiState.Attack:
				UpdateHeavyLunge(dt);
				// Heavy leap commits facing at start — no mid-air turn toward a dodging player.
				if (AnimDriver == null
					|| AnimDriver.CurrentAttackVariant != EnemyAnimDriver.AttackVariant.Heavy)
				{
					FacePlayer(dt * 0.2f);
				}

				UpdateAttackHitbox();
				_stateTimer -= dt;
				if (_stateTimer <= 0f || (AnimDriver?.IsAttackFinished() ?? false))
				{
					BeginRecovery();
				}
				break;

			case AiState.Recovery:
				Velocity = new Vector3(0f, Velocity.Y, 0f);
				_stateTimer -= dt;
				if (_stateTimer <= 0f)
				{
					RestoreTint();
					ResumeAfterInterrupt();
				}
				break;

			case AiState.Stagger:
				Velocity = new Vector3(0f, Velocity.Y, 0f);
				_stateTimer -= dt;
				if (_stateTimer <= 0f)
				{
					RestoreTint();
					ResumeAfterInterrupt();
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

	private void EnterIdle()
	{
		_state = AiState.Idle;
		_hitboxWasActive = false;
		Hitbox?.SetActive(false);
		Velocity = new Vector3(0f, Velocity.Y, 0f);
		AnimDriver?.ResetSpeed();
		AnimDriver?.PlayIdle();
		UpdateNameplateVisibility();
	}

	private void EnterChase()
	{
		_state = AiState.Chase;
		_hitboxWasActive = false;
		Hitbox?.SetActive(false);
		AnimDriver?.ResetSpeed();
		AnimDriver?.PlayChase();
		UpdateNameplateVisibility();
	}

	private void BeginTelegraph()
	{
		_state = AiState.Telegraph;
		float mul = PlayerHasHighBlight() ? HighBlightTelegraphMul : 1f;
		_stateTimer = TelegraphTime * mul;
		_hitboxWasActive = false;
		Hitbox?.SetActive(false);
		Velocity = new Vector3(0f, Velocity.Y, 0f);
		FacePlayer(999f);
		AnimDriver?.PlayTelegraph();
		UpdateNameplateVisibility();
		GameAudio.Instance?.PlaySfx3D(
			this,
			SliceAudioIds.Pick(SliceAudioIds.EnemyTelegraphGrowls),
			volumeDbOffset: -4f,
			pitchScale: 0.62f + GD.Randf() * 0.12f,
			maxDistance: SpatialSfxMaxDistance,
			unitSize: 5f);
	}

	private void BeginAttack()
	{
		float dist = DistanceToPlayer();
		bool canLandHeavy = TryComputeHeavyLungeSpeed(dist, out float aimedLungeSpeed);
		bool closeMelee = dist <= AttackRange * 1.12f;

		// Зашли в телеграф ради прыжка, но прицел уже не сходится — снова в chase.
		if (!closeMelee && !canLandHeavy)
		{
			EnterChase();
			return;
		}

		_state = AiState.Attack;
		_hitboxWasActive = false;
		Hitbox?.SetActive(false);
		Velocity = new Vector3(0f, Velocity.Y, 0f);
		FacePlayer(999f);

		// С дальней дистанции имеет смысл только heavy; в упор — наоборот без него.
		bool requireHeavy = canLandHeavy && !closeMelee;
		AnimDriver?.PlayAttack(
			allowHeavy: canLandHeavy,
			preferHeavy: canLandHeavy,
			preferChance: HeavyAttackPreferChance,
			requireHeavy: requireHeavy);

		if (requireHeavy
			&& AnimDriver != null
			&& AnimDriver.CurrentAttackVariant != EnemyAnimDriver.AttackVariant.Heavy)
		{
			// Клипа нет / не выбрался — не бьём «в воздух» обычным ударом.
			EnterChase();
			return;
		}

		ApplyHitboxTuning();
		BeginHeavyLungeIfNeeded(aimedLungeSpeed);
		UpdateNameplateVisibility();

		float len = AnimDriver?.GetCurrentLength() ?? 0f;
		_stateTimer = len > 0.05f ? len : AttackFallbackDuration;
	}

	private bool ShouldBeginAttack()
	{
		float dist = DistanceToPlayer();
		if (dist <= AttackRange)
		{
			return true;
		}

		// Прыжок — только если хитбокс реально можно положить на цель.
		return TryComputeHeavyLungeSpeed(dist, out _);
	}

	/// <summary>
	/// Считает скорость выпада так, чтобы к концу лунжа центр хитбокса оказался на текущей дистанции до цели.
	/// Путь лунжа: ∫ V·SmoothStep = 0.5·speed·duration.
	/// </summary>
	private bool TryComputeHeavyLungeSpeed(float distanceToTarget, out float lungeSpeed)
	{
		lungeSpeed = 0f;
		if (AnimDriver == null || !AnimDriver.HasHeavyAttackClip())
		{
			return false;
		}

		if (HeavyLungeDuration <= 0.05f || HeavyLungeSpeed <= 0.05f)
		{
			return false;
		}

		float hitboxForward = GetHitboxForwardOffset();
		// Нужный пробег тела: центр хитбокса → позиция цели.
		float desiredTravel = distanceToTarget - hitboxForward;
		float minTravel = GetHitboxHalfExtentForward() * 0.35f;
		if (desiredTravel < minTravel)
		{
			return false;
		}

		// travel = 0.5 * speed * duration  →  speed = 2 * travel / duration
		float speed = (desiredTravel * 2f) / HeavyLungeDuration;
		float minSpeed = HeavyLungeSpeed * HeavyLungeSpeedMinMul;
		float maxSpeed = HeavyLungeSpeed * HeavyLungeSpeedMaxMul;
		if (speed < minSpeed || speed > maxSpeed)
		{
			return false;
		}

		lungeSpeed = speed;
		return true;
	}

	private float GetHitboxForwardOffset()
	{
		if (Hitbox == null)
		{
			return 1.15f;
		}

		// Visual смотрит в -Z; хитбокс стоит впереди по локальному -Z.
		return Mathf.Abs(Hitbox.Position.Z);
	}

	private float GetHitboxHalfExtentForward()
	{
		CollisionShape3D? shapeNode = Hitbox?.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (shapeNode?.Shape is BoxShape3D box)
		{
			return box.Size.Z * 0.5f;
		}

		return 0.7f;
	}

	private void BeginHeavyLungeIfNeeded(float aimedLungeSpeed)
	{
		_lungeTimer = 0f;
		_lungeSpeed = 0f;
		if (AnimDriver == null || AnimDriver.CurrentAttackVariant != EnemyAnimDriver.AttackVariant.Heavy)
		{
			return;
		}

		if (HeavyLungeDuration <= 0f || aimedLungeSpeed <= 0f)
		{
			return;
		}

		// Пересчёт на момент старта удара (игрок мог сдвинуться на телеграфе).
		float dist = DistanceToPlayer();
		if (!TryComputeHeavyLungeSpeed(dist, out float speed))
		{
			// Чуть вне окна — всё равно прыгаем с клампом, чтобы не отменять уже начатый heavy.
			float hitboxForward = GetHitboxForwardOffset();
			float desiredTravel = Mathf.Max(0.35f, dist - hitboxForward);
			speed = Mathf.Clamp(
				(desiredTravel * 2f) / HeavyLungeDuration,
				HeavyLungeSpeed * HeavyLungeSpeedMinMul,
				HeavyLungeSpeed * HeavyLungeSpeedMaxMul);
		}

		_lungeDir = ResolveLungeDirection();
		_lungeSpeed = speed;
		_lungeTimer = HeavyLungeDuration;
	}

	private Vector3 ResolveLungeDirection()
	{
		if (_player != null && GodotObject.IsInstanceValid(_player))
		{
			Vector3 toPlayer = _player.GlobalPosition - GlobalPosition;
			toPlayer.Y = 0f;
			if (toPlayer.LengthSquared() > 0.0001f)
			{
				return toPlayer.Normalized();
			}
		}

		Node3D face = Visual ?? this;
		Vector3 forward = -face.GlobalTransform.Basis.Z;
		forward.Y = 0f;
		if (forward.LengthSquared() < 0.0001f)
		{
			return Vector3.Forward;
		}

		return forward.Normalized();
	}

	private void UpdateHeavyLunge(float dt)
	{
		if (_lungeTimer <= 0f)
		{
			Velocity = new Vector3(0f, Velocity.Y, 0f);
			return;
		}

		_lungeTimer -= dt;
		// Ease out: к концу выпада тело останавливается, хитбокс должен лежать на цели.
		float t = Mathf.Clamp(_lungeTimer / Mathf.Max(0.01f, HeavyLungeDuration), 0f, 1f);
		float speed = _lungeSpeed * Mathf.SmoothStep(0f, 1f, t);
		Velocity = new Vector3(_lungeDir.X * speed, Velocity.Y, _lungeDir.Z * speed);
	}

	private void UpdateAttackHitbox()
	{
		if (Hitbox == null || AnimDriver == null)
		{
			return;
		}

		bool shouldHit = AnimDriver.IsAttackHitWindow();
		if (shouldHit == _hitboxWasActive)
		{
			return;
		}

		Hitbox.SetActive(shouldHit);
		_hitboxWasActive = shouldHit;
	}

	private void BeginRecovery()
	{
		_state = AiState.Recovery;
		_stateTimer = RecoveryTime;
		_lungeTimer = 0f;
		_hitboxWasActive = false;
		Hitbox?.SetActive(false);
		Velocity = new Vector3(0f, Velocity.Y, 0f);
		RestoreTint();
		AnimDriver?.PlayIdle();
		UpdateNameplateVisibility();
	}

	private void BeginStagger()
	{
		_state = AiState.Stagger;
		_stateTimer = StaggerTime;
		_lungeTimer = 0f;
		_hitboxWasActive = false;
		Hitbox?.SetActive(false);
		Velocity = new Vector3(0f, Velocity.Y, 0f);
		ApplyStaggerTint();
		AnimDriver?.PlayStagger();
		UpdateNameplateVisibility();
	}

	private void UpdateNameplateVisibility()
	{
		bool aggro = _state is AiState.Chase or AiState.Telegraph or AiState.Attack
			or AiState.Recovery or AiState.Stagger;
		Nameplate?.SetAggroVisible(aggro);
	}

	private void ResumeAfterInterrupt()
	{
		if (IsPlayerInRange(DetectRange))
		{
			EnterChase();
		}
		else
		{
			EnterIdle();
		}
	}

	private void ChasePlayer(float dt)
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
		float speed = MoveSpeed * (PlayerHasHighBlight() ? HighBlightSpeedMul : 1f);
		Velocity = new Vector3(dir.X * speed, Velocity.Y, dir.Z * speed);
		FaceDirection(dir, dt);
	}

	private void UpdateChaseFootsteps(float dt)
	{
		Vector3 planar = new(Velocity.X, 0f, Velocity.Z);
		float speed = planar.Length();
		if (speed < FootstepMinSpeed)
		{
			_footstepTimer = 0f;
			return;
		}

		float interval = FootstepInterval * Mathf.Clamp(MoveSpeed / Mathf.Max(speed, 0.01f), 0.6f, 1.35f);
		_footstepTimer -= dt;
		if (_footstepTimer > 0f)
		{
			return;
		}

		_footstepTimer = interval;
		GameAudio.Instance?.PlaySfx3D(
			this,
			SliceAudioIds.Pick(SliceAudioIds.FootstepsConcrete),
			volumeDbOffset: -8f,
			pitchScale: 0.78f + GD.Randf() * 0.14f,
			maxDistance: SpatialSfxMaxDistance * 0.85f,
			unitSize: 3.5f);
	}

	private bool PlayerHasHighBlight()
	{
		if (_player == null)
		{
			return false;
		}

		Blight? blight = _player.GetNodeOrNull<Blight>("Blight");
		return blight != null && blight.IsHigh;
	}

	private void FacePlayer(float dt)
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

		FaceDirection(toPlayer.Normalized(), dt);
	}

	private void FaceDirection(Vector3 direction, float dt)
	{
		if (Visual == null || direction.LengthSquared() < 0.0001f)
		{
			return;
		}

		Transform3D current = Visual.GlobalTransform;
		Transform3D looking = current.LookingAt(current.Origin + direction, Vector3.Up);
		Quaternion from = current.Basis.GetRotationQuaternion();
		Quaternion to = looking.Basis.GetRotationQuaternion();
		float weight = dt >= 100f
			? 1f
			: 1f - Mathf.Exp(-FaceTurnSpeed * Mathf.Max(dt, 0f));
		Quaternion blended = from.Slerp(to, weight).Normalized();
		Visual.GlobalTransform = new Transform3D(new Basis(blended), current.Origin);
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

	private void ApplyAshTintToMeshes()
	{
		_tintedMaterials.Clear();
		_baseAlbedos.Clear();
		ApplyTintRecursive(Visual ?? this);
	}

	private void ApplyTintRecursive(Node node)
	{
		if (node is MeshInstance3D mesh)
		{
			int count = mesh.Mesh?.GetSurfaceCount() ?? 0;
			for (int i = 0; i < count; i++)
			{
				Material? source = mesh.GetActiveMaterial(i);
				if (source is not BaseMaterial3D)
				{
					continue;
				}

				var dup = (BaseMaterial3D)source.Duplicate();
				_baseAlbedos.Add(dup.AlbedoColor);
				dup.AlbedoColor = dup.AlbedoColor * AshTint;
				_tintedMaterials.Add(dup);
				mesh.SetSurfaceOverrideMaterial(i, dup);
			}
		}

		foreach (Node child in node.GetChildren())
		{
			ApplyTintRecursive(child);
		}
	}

	private void ApplyStaggerTint() => SetTintMul(new Color(1.05f, 0.95f, 0.55f, 1f));

	private void RestoreTint()
	{
		for (int i = 0; i < _tintedMaterials.Count; i++)
		{
			Color baseCol = i < _baseAlbedos.Count ? _baseAlbedos[i] : Colors.White;
			_tintedMaterials[i].AlbedoColor = baseCol * AshTint;
		}
	}

	private void SetTintMul(Color mul)
	{
		for (int i = 0; i < _tintedMaterials.Count; i++)
		{
			Color baseCol = i < _baseAlbedos.Count ? _baseAlbedos[i] : Colors.White;
			_tintedMaterials[i].AlbedoColor = baseCol * AshTint * mul;
		}
	}

	private void ApplyHitboxTuning()
	{
		if (Hitbox == null)
		{
			return;
		}

		float dmgMul = 1f;
		float kbMul = 1f;
		if (AnimDriver != null)
		{
			switch (AnimDriver.CurrentAttackVariant)
			{
				case EnemyAnimDriver.AttackVariant.Alt:
					dmgMul = AltAttackDamageMul;
					break;
				case EnemyAnimDriver.AttackVariant.Heavy:
					dmgMul = HeavyAttackDamageMul;
					kbMul = HeavyAttackKnockbackMul;
					break;
			}
		}

		Hitbox.Damage = Mathf.Max(1, Mathf.RoundToInt(AttackDamage * dmgMul));
		Hitbox.KnockbackForce = AttackKnockback * kbMul;
		Hitbox.HitStopSeconds = AttackHitStop;
	}

	private void OnDamaged(int amount, Vector3 sourcePosition)
	{
		if (_state == AiState.Dead)
		{
			return;
		}

		BeginStagger();
	}

	private void OnDied()
	{
		_state = AiState.Dead;
		Velocity = Vector3.Zero;
		_knockbackTimer = 0f;
		_lungeTimer = 0f;
		_hitboxWasActive = false;
		_deathSettling = true;
		_deathGroundOffsetApplied = 0f;
		Hitbox?.SetActive(false);
		UpdateNameplateVisibility();
		SetTintMul(new Color(0.45f, 0.4f, 0.4f, 1f));

		CollisionLayer = 0;
		CollisionMask = 0;

		if (AnimDriver != null)
		{
			AnimDriver.PlayDeath();
			// Только пауза после доигрывания — без Seek в конец (он рвал анимацию).
		}
		else if (Visual != null)
		{
			Visual.Visible = false;
			_deathSettling = false;
		}

		GameAudio.Instance?.PlaySfx3D(
			this,
			SliceAudioIds.EnemyDeath,
			volumeDbOffset: -2f,
			pitchScale: 0.85f + GD.Randf() * 0.2f,
			maxDistance: SpatialSfxMaxDistance,
			unitSize: 6f);

		GetTree().CreateTimer(DeathDespawnDelay).Timeout += QueueFree;
	}

	/// <summary>
	/// Death без hips-Y оставляет лежащую позу на высоте стоячих бёдер.
	/// AABB скин-меша врёт (rest pose) — меряем кости и опускаем Visual каждый кадр,
	/// пока анимация валит тело (не выключаем settle после первого «на полу»).
	/// </summary>
	private void SettleCorpseToGround(float dt)
	{
		if (Visual == null)
		{
			_deathSettling = false;
			return;
		}

		_deathSkeleton ??= FindSkeleton(Visual);
		float floorY = GlobalPosition.Y + DeathFloorClearance;
		float bottomY = SampleSkeletonBottomY(_deathSkeleton);
		if (bottomY >= float.MaxValue * 0.5f)
		{
			return;
		}

		float gap = bottomY - floorY;
		if (gap <= 0f)
		{
			return;
		}

		float step = Mathf.Min(gap, DeathSettleSpeed * dt);
		Visual.Position = new Vector3(
			Visual.Position.X,
			Visual.Position.Y - step,
			Visual.Position.Z);
		_deathGroundOffsetApplied += step;
	}

	private static Skeleton3D? FindSkeleton(Node node)
	{
		if (node is Skeleton3D skel)
		{
			return skel;
		}

		foreach (Node child in node.GetChildren())
		{
			Skeleton3D? found = FindSkeleton(child);
			if (found != null)
			{
				return found;
			}
		}

		return null;
	}

	private static float SampleSkeletonBottomY(Skeleton3D? skeleton)
	{
		if (skeleton == null || !GodotObject.IsInstanceValid(skeleton))
		{
			return float.MaxValue;
		}

		float bottomY = float.MaxValue;
		int count = skeleton.GetBoneCount();
		Transform3D skelGlobal = skeleton.GlobalTransform;
		for (int i = 0; i < count; i++)
		{
			Vector3 world = skelGlobal * skeleton.GetBoneGlobalPose(i).Origin;
			if (world.Y < bottomY)
			{
				bottomY = world.Y;
			}
		}

		return bottomY;
	}
}
