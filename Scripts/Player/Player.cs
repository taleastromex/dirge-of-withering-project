using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Игрок в 3D: «перед» = направление на курсор.
/// W — к курсору, S — от курсора, A/D — стрейф относительно прицела.
/// </summary>
public partial class Player : CharacterBody3D
{
	/// <summary>Скорость передвижения (единиц/сек).</summary>
	[Export]
	public float MoveSpeed { get; set; } = 6f;

	/// <summary>
	/// Ускорение до целевой скорости.
	/// 0 = мгновенный отклик.
	/// </summary>
	[Export]
	public float Acceleration { get; set; } = 0f;

	/// <summary>Узел, который поворачивается к курсору (тело / пивот оружия).</summary>
	[Export]
	public Node3D? AimPivot { get; set; }

	/// <summary>Длина луча прицеливания от камеры.</summary>
	[Export]
	public float AimRayLength { get; set; } = 1000f;

	/// <summary>Высота плоскости прицеливания, если луч не попал в коллизию.</summary>
	[Export]
	public float AimPlaneY { get; set; } = 0f;

	[Export]
	public Health? Health { get; set; }

	[Export]
	public float DeathRestartDelay { get; set; } = 0.85f;

	private Camera3D? _camera;

	/// <summary>Горизонтальный «вперёд» к курсору (нормализованный, Y = 0).</summary>
	private Vector3 _aimForward = Vector3.Forward;

	private bool _isDead;

	public override void _Ready()
	{
		AddToGroup("player");

		AimPivot ??= GetNodeOrNull<Node3D>("Visual") ?? this;
		Health ??= GetNodeOrNull<Health>("Health");
		_camera = GetViewport().GetCamera3D();

		if (Health != null)
		{
			Health.Died += OnDied;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_isDead)
		{
			Velocity = Vector3.Zero;
			return;
		}

		_camera ??= GetViewport().GetCamera3D();

		// Сначала прицел — от него зависит, куда смотрит «W».
		HandleAiming();
		HandleMovement((float)delta);
	}

	private void HandleMovement(float delta)
	{
		Vector2 input = Input.GetVector(
			"move_left",
			"move_right",
			"move_up",
			"move_down"
		);

		Vector3 direction = GetAimRelativeDirection(input);
		Vector3 targetVelocity = direction * MoveSpeed;
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

	/// <summary>
	/// W/S — вперёд/назад к курсору, A/D — стрейф перпендикулярно прицелу.
	/// </summary>
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
		// Луч прицела цепляется за мир (и врагов как поверхность), не за хитбоксы.
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

		GetTree().CreateTimer(DeathRestartDelay).Timeout += () =>
		{
			GetTree().ReloadCurrentScene();
		};
	}
}
