using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Камера «сверху-сбоку» (Sims / Project Zomboid).
/// Следует за целью по позиции, но не наследует её поворот.
/// </summary>
public partial class FollowCamera : Node3D
{
	[Export]
	public Node3D? Target { get; set; }

	/// <summary>
	/// Смещение камеры относительно цели.
	/// (10, 12, 10) — диагональный вид сверху-сбоку.
	/// </summary>
	[Export]
	public Vector3 Offset { get; set; } = new(10f, 12f, 10f);

	/// <summary>Скорость сглаживания следования. 0 = без сглаживания.</summary>
	[Export]
	public float SmoothSpeed { get; set; } = 8f;

	/// <summary>Точка, на которую смотрит камера (смещение от ног цели вверх).</summary>
	[Export]
	public Vector3 LookAtOffset { get; set; } = new(0f, 1f, 0f);

	private Camera3D? _camera;

	public override void _Ready()
	{
		_camera = GetNodeOrNull<Camera3D>("Camera3D");
		if (Target == null)
		{
			Target = GetTree().CurrentScene?.GetNodeOrNull<Node3D>("Player");
		}

		SnapToTarget();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Target == null)
		{
			return;
		}

		Vector3 desired = Target.GlobalPosition + Offset;

		if (SmoothSpeed <= 0f)
		{
			GlobalPosition = desired;
		}
		else
		{
			float t = 1f - Mathf.Exp(-SmoothSpeed * (float)delta);
			GlobalPosition = GlobalPosition.Lerp(desired, t);
		}

		_camera?.LookAt(Target.GlobalPosition + LookAtOffset, Vector3.Up);
	}

	private void SnapToTarget()
	{
		if (Target == null)
		{
			return;
		}

		GlobalPosition = Target.GlobalPosition + Offset;
		_camera?.LookAt(Target.GlobalPosition + LookAtOffset, Vector3.Up);
	}
}
