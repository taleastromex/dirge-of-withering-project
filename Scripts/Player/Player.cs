using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Игрок в 3D: «перед» = направление на курсор.
/// W — к курсору, S — от курсора, A/D — стрейф относительно прицела.
/// </summary>
public partial class Player : CharacterBody3D
{
	[Export]
	public float MoveSpeed { get; set; } = 6f;

	[Export]
	public float Acceleration { get; set; } = 0f;

	[Export]
	public Node3D? AimPivot { get; set; }

	[Export]
	public float AimRayLength { get; set; } = 1000f;

	[Export]
	public float AimPlaneY { get; set; } = 0f;

	[Export]
	public Health? Health { get; set; }

	[Export]
	public PlayerAttack? Attack { get; set; }

	[Export]
	public MeshInstance3D? BodyMesh { get; set; }

	[Export]
	public float DeathRestartDelay { get; set; } = 0.85f;

	/// <summary>Множитель скорости во время замаха/удара.</summary>
	[Export]
	public float AttackMoveMultiplier { get; set; } = 0.2f;

	[Export]
	public float KnockbackDuration { get; set; } = 0.12f;

	private Camera3D? _camera;
	private Vector3 _aimForward = Vector3.Forward;
	private bool _isDead;

	private Vector3 _knockbackVelocity;
	private float _knockbackTimer;

	private StandardMaterial3D? _bodyMaterial;
	private Color _baseColor = new(0.55f, 0.18f, 0.22f, 1f);
	private float _flashTimer;

	public override void _Ready()
	{
		AddToGroup("player");

		AimPivot ??= GetNodeOrNull<Node3D>("Visual") ?? this;
		Health ??= GetNodeOrNull<Health>("Health");
		Attack ??= GetNodeOrNull<PlayerAttack>("PlayerAttack");
		BodyMesh ??= GetNodeOrNull<MeshInstance3D>("Visual/Body");
		_camera = GetViewport().GetCamera3D();

		if (BodyMesh?.GetActiveMaterial(0) is StandardMaterial3D mat)
		{
			_bodyMaterial = mat;
			_baseColor = mat.AlbedoColor;
		}

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
		HandleAiming();

		if (_knockbackTimer > 0f)
		{
			_knockbackTimer -= dt;
			Velocity = new Vector3(_knockbackVelocity.X, Velocity.Y, _knockbackVelocity.Z);
			_knockbackVelocity = _knockbackVelocity.MoveToward(Vector3.Zero, 40f * dt);
			MoveAndSlide();
			return;
		}

		HandleMovement(dt);
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

		float speed = MoveSpeed;
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

	private void HandleAiming()
	{
		if (_camera == null)
		{
			return;
		}

		Vector2 mousePos = GetViewport().GetMousePosition();
		Vector3 rayOrigin = _camera.ProjectRayOrigin(mousePos);
		Vector3 rayDir = _camera.ProjectRayNormal(mousePos);

		Vector3? hitPoint = RaycastAimPoint(rayOrigin, rayDir)
			?? IntersectAimPlane(rayOrigin, rayDir);

		if (hitPoint == null)
		{
			return;
		}

		Vector3 aimOrigin = AimPivot?.GlobalPosition ?? GlobalPosition;
		Vector3 lookAt = hitPoint.Value;
		lookAt.Y = aimOrigin.Y;

		Vector3 toTarget = lookAt - aimOrigin;
		toTarget.Y = 0f;
		if (toTarget.LengthSquared() < 0.0001f)
		{
			return;
		}

		_aimForward = toTarget.Normalized();
		AimPivot?.LookAt(lookAt, Vector3.Up);
	}

	private Vector3? RaycastAimPoint(Vector3 origin, Vector3 direction)
	{
		PhysicsDirectSpaceState3D space = GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(
			origin,
			origin + direction * AimRayLength
		);
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		query.CollisionMask = CombatLayers.World | CombatLayers.Enemy;

		var result = space.IntersectRay(query);
		if (result.Count == 0)
		{
			return null;
		}

		return (Vector3)result["position"];
	}

	private Vector3? IntersectAimPlane(Vector3 origin, Vector3 direction)
	{
		var plane = new Plane(Vector3.Up, AimPlaneY);
		return plane.IntersectsRay(origin, direction);
	}

	private void OnDamaged(int amount, Vector3 sourcePosition)
	{
		SetBodyColor(new Color(0.95f, 0.85f, 0.85f, 1f));
		_flashTimer = 0.1f;
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
		if (_isDead)
		{
			return;
		}

		_isDead = true;
		Velocity = Vector3.Zero;
		CollisionLayer = 0;
		CollisionMask = 0;
		SetBodyColor(new Color(0.2f, 0.12f, 0.14f, 1f));

		GetTree().CreateTimer(DeathRestartDelay).Timeout += () =>
		{
			GetTree().ReloadCurrentScene();
		};
	}
}
