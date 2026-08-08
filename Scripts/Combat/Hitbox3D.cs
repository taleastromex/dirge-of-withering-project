using System.Collections.Generic;
using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Активный хитбокс удара. Пока Monitoring включён — наносит урон Health на вошедших телах.
/// Один Health получает урон не чаще одного раза за активацию.
/// </summary>
public partial class Hitbox3D : Area3D
{
	[Export]
	public int Damage { get; set; } = 25;

	[Export]
	public float KnockbackForce { get; set; } = 8f;

	[Export]
	public float HitStopSeconds { get; set; } = 0.05f;

	/// <summary>Корень владельца — его Health не повреждается.</summary>
	[Export]
	public Node? OwnerRoot { get; set; }

	/// <summary>Опциональный меш для отладки окна удара.</summary>
	[Export]
	public MeshInstance3D? DebugMesh { get; set; }

	private readonly HashSet<ulong> _hitInstanceIds = new();

	public override void _Ready()
	{
		Monitoring = false;
		Monitorable = false;
		BodyEntered += OnBodyEntered;

		OwnerRoot ??= GetOwnerOrNull<Node>() ?? GetParent();
		DebugMesh ??= GetNodeOrNull<MeshInstance3D>("DebugMesh");
		SetDebugVisible(false);
	}

	public void SetActive(bool active)
	{
		if (active)
		{
			_hitInstanceIds.Clear();
		}

		Monitoring = active;
		SetDebugVisible(active);

		if (!active)
		{
			return;
		}

		foreach (Node3D body in GetOverlappingBodies())
		{
			TryHit(body);
		}
	}

	/// <summary>Показать/скрыть debug-меш без включения урона (телеграф игрока).</summary>
	public void SetDebugVisiblePublic(bool visible)
	{
		SetDebugVisible(visible);
	}

	private void OnBodyEntered(Node3D body)
	{
		if (!Monitoring)
		{
			return;
		}

		TryHit(body);
	}

	private void TryHit(Node3D body)
	{
		if (OwnerRoot != null && (body == OwnerRoot || OwnerRoot.IsAncestorOf(body) || body.IsAncestorOf(OwnerRoot)))
		{
			return;
		}

		Health? health = FindHealth(body);
		if (health == null || health.IsDead)
		{
			return;
		}

		ulong id = health.GetInstanceId();
		if (!_hitInstanceIds.Add(id))
		{
			return;
		}

		Vector3 source = OwnerRoot is Node3D owner3D ? owner3D.GlobalPosition : GlobalPosition;
		bool landed = health.TakeDamage(Damage, source);
		if (!landed)
		{
			return;
		}

		ApplyKnockback(body, source);

		if (HitStopSeconds > 0f)
		{
			CombatHitStop.Pulse(GetTree(), HitStopSeconds);
		}
	}

	private void ApplyKnockback(Node3D body, Vector3 source)
	{
		if (KnockbackForce <= 0f)
		{
			return;
		}

		switch (body)
		{
			case Player player:
				player.ApplyKnockback(source, KnockbackForce);
				break;
			case BasicEnemy enemy:
				enemy.ApplyKnockback(source, KnockbackForce);
				break;
		}
	}

	private static Health? FindHealth(Node node)
	{
		Health? direct = node.GetNodeOrNull<Health>("Health");
		if (direct != null)
		{
			return direct;
		}

		foreach (Node child in node.GetChildren())
		{
			if (child is Health health)
			{
				return health;
			}
		}

		return node.GetParent()?.GetNodeOrNull<Health>("Health");
	}

	private void SetDebugVisible(bool visible)
	{
		if (DebugMesh != null)
		{
			DebugMesh.Visible = visible;
		}
	}
}
