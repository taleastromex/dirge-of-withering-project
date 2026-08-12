using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Игрок в 3D: «перед» = направление на курсор.
/// W — к курсору, S — от курсора, A/D — стрейф относительно прицела.
/// </summary>
public partial class Player : CharacterBody3D
{
	[Export]
	public float MoveSpeed { get; set; } = 4.0f;

	[Export]
	public float Acceleration { get; set; } = 0f;

	[Export]
	public Node3D? AimPivot { get; set; }

	/// <summary>Мёртвая зона курсора вокруг персонажа на экране (px).</summary>
	[Export]
	public float AimScreenDeadzonePixels { get; set; } = 8f;

	/// <summary>Высота точки персонажа для Unproject (экранный «центр» тела).</summary>
	[Export]
	public float AimScreenPivotHeight { get; set; } = 0.9f;

	[Export]
	public Health? Health { get; set; }

	[Export]
	public NpcCategory Category { get; set; } = NpcCategory.Human;

	[Export]
	public PlayerAttack? Attack { get; set; }

	/// <summary>When false, skip move / aim-driven locomotion (grip tune).</summary>
	[Export]
	public bool ControlEnabled { get; set; } = true;

	[Export]
	public BlightController? BlightController { get; set; }

	[Export]
	public MeshInstance3D? BodyMesh { get; set; }

	[Export]
	public PlayerAnimDriver? AnimDriver { get; set; }

	[Export]
	public float DeathRestartDelay { get; set; } = 3.0f;

	/// <summary>Множитель скорости во время замаха/удара.</summary>
	[Export]
	public float AttackMoveMultiplier { get; set; } = 0.2f;

	[Export]
	public float KnockbackDuration { get; set; } = 0.12f;

	/// <summary>Фиксирует Y (top-down без гравитации / наклонов).</summary>
	[Export]
	public bool LockVerticalPosition { get; set; } = true;

	[Export]
	public float LockedY { get; set; } = 0f;

	[Export]
	public float FootstepInterval { get; set; } = 0.38f;

	[Export]
	public float FootstepMinSpeed { get; set; } = 0.6f;

	/// <summary>Длительность hurt-лока движения (клип ускоряется отдельно в AnimDriver).</summary>
	[Export]
	public float HurtLockSeconds { get; set; } = 0.18f;

	private Camera3D? _camera;
	private Vector3 _aimForward = Vector3.Forward;
	private bool _isDead;

	private Vector3 _knockbackVelocity;
	private float _knockbackTimer;
	private float _footstepTimer;
	private float _hurtLockTimer;

	private readonly System.Collections.Generic.List<StandardMaterial3D> _bodyMaterials = new();
	private Color _baseColor = new(0.55f, 0.18f, 0.22f, 1f);
	private float _flashTimer;

	public override void _Ready()
	{
		AddToGroup("player");

		AimPivot ??= GetNodeOrNull<Node3D>("Visual") ?? this;
		Health ??= GetNodeOrNull<Health>("Health");
		Attack ??= GetNodeOrNull<PlayerAttack>("PlayerAttack");
		BlightController ??= GetNodeOrNull<BlightController>("BlightController");
		AnimDriver ??= GetNodeOrNull<PlayerAnimDriver>("AnimDriver");
		BodyMesh ??= GetNodeOrNull<MeshInstance3D>("Visual/Body");
		_camera = GetViewport().GetCamera3D();

		CaptureBodyMaterials();

		if (Health != null)
		{
			Health.Died += OnDied;
			Health.Damaged += OnDamaged;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		if (_flashTimer > 0f)
		{
			_flashTimer -= dt;
			if (_flashTimer <= 0f)
			{
				SetBodyColor(_baseColor);
			}
		}

		if (_isDead)
		{
			Velocity = Vector3.Zero;
			return;
		}

		_camera ??= GetViewport().GetCamera3D();

		if (!ControlEnabled)
		{
			Velocity = Vector3.Zero;
			MoveAndSlide();
			ApplyVerticalLock();
			AnimDriver?.UpdateLocomotion(0f);
			return;
		}

		HandleAiming();

		if (_hurtLockTimer > 0f)
		{
			_hurtLockTimer -= dt;
		}

		if (_knockbackTimer > 0f)
		{
			_knockbackTimer -= dt;
			Velocity = new Vector3(_knockbackVelocity.X, Velocity.Y, _knockbackVelocity.Z);
			_knockbackVelocity = _knockbackVelocity.MoveToward(Vector3.Zero, 40f * dt);
			MoveAndSlide();
			ApplyVerticalLock();
			return;
		}

		if (_hurtLockTimer > 0f || (AnimDriver != null && AnimDriver.IsHurt))
		{
			Velocity = new Vector3(0f, Velocity.Y, 0f);
			MoveAndSlide();
			ApplyVerticalLock();
			return;
		}

		HandleMovement(dt);
		UpdateFootsteps(dt);
		ApplyVerticalLock();

		Vector3 planar = new(Velocity.X, 0f, Velocity.Z);
		AnimDriver?.UpdateLocomotion(planar.Length());
	}

	private void UpdateFootsteps(float dt)
	{
		Vector3 planar = new(Velocity.X, 0f, Velocity.Z);
		float speed = planar.Length();
		if (speed < FootstepMinSpeed || (Attack != null && Attack.LocksMovement))
		{
			_footstepTimer = 0f;
			return;
		}

		float interval = FootstepInterval * Mathf.Clamp(MoveSpeed / Mathf.Max(speed, 0.01f), 0.55f, 1.25f);
		_footstepTimer -= dt;
		if (_footstepTimer > 0f)
		{
			return;
		}

		_footstepTimer = interval;
		string[] steps = SliceAudioIds.FootstepsConcrete;
		string clip = steps[GD.RandRange(0, steps.Length - 1)];
		GameAudio.Instance?.PlaySfxOneShot(clip, volumeDbOffset: -10f, pitchScale: 0.92f + GD.Randf() * 0.16f);
	}

	private void ApplyVerticalLock()
	{
		if (!LockVerticalPosition)
		{
			return;
		}

		Vector3 pos = GlobalPosition;
		if (!Mathf.IsEqualApprox(pos.Y, LockedY))
		{
			GlobalPosition = new Vector3(pos.X, LockedY, pos.Z);
		}

		Velocity = new Vector3(Velocity.X, 0f, Velocity.Z);
	}

	public void ApplyKnockback(Vector3 sourcePosition, float force)
	{
		if (_isDead || force <= 0f)
		{
			return;
		}

		Vector3 dir = GlobalPosition - sourcePosition;
		dir.Y = 0f;
		if (dir.LengthSquared() < 0.0001f)
		{
			dir = -_aimForward;
		}

		_knockbackVelocity = dir.Normalized() * force;
		_knockbackTimer = KnockbackDuration;
	}

	private void HandleMovement(float delta)
	{
		Vector2 input = Input.GetVector(
			"move_left",
			"move_right",
			"move_up",
			"move_down"
		);

		float speed = MoveSpeed * (BlightController?.GetSpeedMultiplier() ?? 1f);
		if (Attack != null && Attack.LocksMovement)
		{
			speed *= AttackMoveMultiplier;
		}

		Vector3 direction = GetAimRelativeDirection(input);
		Vector3 targetVelocity = direction * speed;
		targetVelocity.Y = Velocity.Y;

		if (Acceleration > 0f)
		{
			Velocity = Velocity.MoveToward(targetVelocity, Acceleration * delta);
		}
		else
		{
			Velocity = targetVelocity;
		}

		MoveAndSlide();
	}

	private Vector3 GetAimRelativeDirection(Vector2 input)
	{
		if (input == Vector2.Zero)
		{
			return Vector3.Zero;
		}

		Vector3 forward = _aimForward;
		forward.Y = 0f;
		if (forward.LengthSquared() < 0.0001f)
		{
			return Vector3.Zero;
		}

		forward = forward.Normalized();
		Vector3 right = forward.Cross(Vector3.Up).Normalized();
		Vector3 direction = (forward * -input.Y) + (right * input.X);
		return direction.LengthSquared() > 0.0001f ? direction.Normalized() : Vector3.Zero;
	}

	/// <summary>
	/// Прицел в экранном пространстве относительно персонажа (не raycast в пол).
	/// Курсор «выше» тела на экране = вглубь сцены; «ниже» = к камере.
	/// Так угол камеры больше не ломает ощущение вперёд/назад.
	/// </summary>
	private void HandleAiming()
	{
		if (_camera == null)
		{
			return;
		}

		Vector3 pivotWorld = GlobalPosition + Vector3.Up * AimScreenPivotHeight;
		Vector2 playerScreen = _camera.UnprojectPosition(pivotWorld);
		Vector2 mousePos = GetViewport().GetMousePosition();
		Vector2 screenDelta = mousePos - playerScreen;

		if (screenDelta.LengthSquared() < AimScreenDeadzonePixels * AimScreenDeadzonePixels)
		{
			return;
		}

		// Оси пола, выровненные с камерой.
		Vector3 camRight = _camera.GlobalTransform.Basis.X;
		Vector3 camLook = -_camera.GlobalTransform.Basis.Z;
		camRight.Y = 0f;
		camLook.Y = 0f;

		if (camRight.LengthSquared() < 0.0001f || camLook.LengthSquared() < 0.0001f)
		{
			return;
		}

		camRight = camRight.Normalized();
		camLook = camLook.Normalized();

		// Screen +X → вправо; Screen +Y (вниз) → к камере (−look).
		Vector3 aimDir = (camRight * screenDelta.X) + (-camLook * screenDelta.Y);
		if (aimDir.LengthSquared() < 0.0001f)
		{
			return;
		}

		_aimForward = aimDir.Normalized();

		Vector3 aimOrigin = AimPivot?.GlobalPosition ?? GlobalPosition;
		Vector3 lookAt = aimOrigin + _aimForward;
		AimPivot?.LookAt(lookAt, Vector3.Up);
	}

	private void OnDamaged(int amount, Vector3 sourcePosition)
	{
		if (_isDead)
		{
			return;
		}

		GameAudio.Instance?.PlaySfxOneShot(SliceAudioIds.PlayerHurt, volumeDbOffset: -6f, pitchScale: 1.05f);

		// Fatal hit: skip hurt anim — OnDied plays death immediately after.
		if (Health != null && Health.Current <= 0)
		{
			Attack?.Interrupt();
			return;
		}

		Attack?.Interrupt();
		AnimDriver?.PlayHurt();
		_hurtLockTimer = HurtLockSeconds;

		SetBodyColor(new Color(0.95f, 0.85f, 0.85f, 1f));
		_flashTimer = 0.12f;
	}

	/// <summary>Визуал Скверны: emission нарастает с шкалой.</summary>
	public void SetBlightVisual(float normalized, bool overloaded)
	{
		if (_bodyMaterials.Count == 0 || _flashTimer > 0f)
		{
			return;
		}

		Color albedo = _baseColor.Lerp(new Color(0.35f, 0.06f, 0.08f), normalized * 0.55f);
		float emission = Mathf.Lerp(0f, overloaded ? 1.6f : 0.9f, normalized);
		foreach (StandardMaterial3D mat in _bodyMaterials)
		{
			mat.AlbedoColor = albedo;
			mat.EmissionEnabled = normalized > 0.05f;
			mat.Emission = new Color(0.55f, 0.08f, 0.1f);
			mat.EmissionEnergyMultiplier = emission;
		}
	}

	public void SetFilthAuraActive(bool active)
	{
		if (_isDead)
		{
			active = false;
		}

		PlayerFilthAura? aura = GetNodeOrNull<PlayerFilthAura>("Visual/FilthAura");
		aura?.SetHighFilthActive(active);
	}

	private void SetBodyColor(Color color)
	{
		foreach (StandardMaterial3D mat in _bodyMaterials)
		{
			mat.AlbedoColor = color;
		}
	}

	private void CaptureBodyMaterials()
	{
		_bodyMaterials.Clear();
		System.Collections.Generic.List<MeshInstance3D> meshes = new();
		if (BodyMesh != null && BodyMesh.Visible)
		{
			meshes.Add(BodyMesh);
		}
		else
		{
			CollectMeshes(GetNodeOrNull<Node>("Visual/Model") ?? GetNodeOrNull<Node>("Visual"), meshes);
		}

		foreach (MeshInstance3D mesh in meshes)
		{
			int surfaces = mesh.Mesh?.GetSurfaceCount() ?? 0;
			if (surfaces <= 0 && mesh.MaterialOverride is StandardMaterial3D single)
			{
				StandardMaterial3D dup = (StandardMaterial3D)single.Duplicate();
				if (_bodyMaterials.Count == 0)
				{
					_baseColor = dup.AlbedoColor;
				}

				mesh.MaterialOverride = dup;
				_bodyMaterials.Add(dup);
				continue;
			}

			for (int i = 0; i < surfaces; i++)
			{
				Material? source = mesh.GetActiveMaterial(i);
				if (source is not StandardMaterial3D shared)
				{
					continue;
				}

				StandardMaterial3D dup = (StandardMaterial3D)shared.Duplicate();
				if (_bodyMaterials.Count == 0)
				{
					_baseColor = dup.AlbedoColor;
				}

				mesh.SetSurfaceOverrideMaterial(i, dup);
				_bodyMaterials.Add(dup);
			}
		}
	}

	private static void CollectMeshes(Node? root, System.Collections.Generic.List<MeshInstance3D> into)
	{
		if (root == null)
		{
			return;
		}

		if (root is MeshInstance3D mi)
		{
			into.Add(mi);
		}

		foreach (Node child in root.GetChildren())
		{
			CollectMeshes(child, into);
		}
	}

	private void OnDied()
	{
		if (_isDead)
		{
			return;
		}

		_isDead = true;
		Velocity = Vector3.Zero;
		CollisionLayer = 0;
		CollisionMask = 0;
		AnimDriver?.PlayDeath();
		SetFilthAuraActive(false);
		SetBodyColor(new Color(0.2f, 0.12f, 0.14f, 1f));
		GameAudio.Instance?.PlaySfxOneShot(SliceAudioIds.PlayerDeath, volumeDbOffset: -1f);
		GameAudio.Instance?.PlaySfxOneShot(
			SliceAudioIds.Pick(SliceAudioIds.PlayerDeathVoices),
			volumeDbOffset: -2f);

		GetTree().CreateTimer(DeathRestartDelay, processAlways: true).Timeout += () =>
		{
			foreach (Node node in GetTree().GetNodesInGroup("death_screen"))
			{
				if (node is DeathScreen screen)
				{
					screen.ShowDeathScreen();
					return;
				}
			}

			// Fallback if HUD overlay missing.
			GetTree().Paused = false;
			GetTree().ReloadCurrentScene();
		};
	}
}
