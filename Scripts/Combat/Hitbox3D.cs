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

	/// <summary>
	/// FILTH на цель за 1 ед. фактически снятого HP (0 = не заражает).
	/// Искажённые Пеплом: 0.5.
	/// </summary>
	[Export]
	public float FilthPerDamage { get; set; }

	/// <summary>Доля PhysicalResist цели, которую удар игнорирует (0…1).</summary>
	[Export(PropertyHint.Range, "0,1,0.01")]
	public float PhysicalResistIgnore { get; set; }

	/// <summary>Доля нанесённого урона, возвращаемая владельцу как heal (0.2 для Undead).</summary>
	[Export(PropertyHint.Range, "0,1,0.01")]
	public float LifestealFraction { get; set; }

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

	/// <summary>When false (default), active frames and telegraphs stay invisible in play.</summary>
	[Export]
	public bool ShowDebugVisual { get; set; }

	private readonly HashSet<ulong> _hitInstanceIds = new();

	public override void _Ready()
	{
		Monitoring = false;
		Monitorable = false;
		BodyEntered += OnBodyEntered;

		OwnerRoot ??= GetOwnerOrNull<Node>() ?? GetParent();
		DebugMesh ??= GetNodeOrNull<MeshInstance3D>("DebugMesh");
		if (DebugMesh != null)
		{
			DebugMesh.Visible = false;
		}
	}

	public void SetActive(bool active)
	{
		if (active)
		{
			_hitInstanceIds.Clear();
		}

		Monitoring = active;
		SetDebugVisible(active && ShowDebugVisual);

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
		SetDebugVisible(visible && ShowDebugVisual);
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

		if (OwnerRoot != null && !FactionRules.CanHarm(OwnerRoot, body))
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
		int applied = health.TakeDamage(Damage, source, PhysicalResistIgnore);
		if (applied <= 0)
		{
			return;
		}

		ApplyLifesteal(applied);
		ApplyFilthGain(body, applied);
		ApplyKnockback(body, source);

		// Weapon impact only when the player (or ally) lands a hit — player-hurt SFX is handled on Player.
		if (body is not Player)
		{
			GameAudio.Instance?.PlaySfxOneShot(
				SliceAudioIds.Pick(SliceAudioIds.SwordHits),
				volumeDbOffset: -2f,
				pitchScale: 0.95f + GD.Randf() * 0.1f);
		}

		if (HitStopSeconds > 0f)
		{
			CombatHitStop.Pulse(GetTree(), HitStopSeconds);
		}
	}

	private void ApplyLifesteal(int appliedDamage)
	{
		if (LifestealFraction <= 0f || appliedDamage <= 0 || OwnerRoot == null)
		{
			return;
		}

		Health? ownerHealth = FindHealth(OwnerRoot);
		if (ownerHealth == null || ownerHealth.IsDead)
		{
			return;
		}

		int heal = Mathf.Max(1, Mathf.RoundToInt(appliedDamage * LifestealFraction));
		ownerHealth.Heal(heal);
	}

	private void ApplyFilthGain(Node3D body, int appliedDamage)
	{
		if (FilthPerDamage <= 0f || appliedDamage <= 0)
		{
			return;
		}

		float gain = appliedDamage * FilthPerDamage;
		BlightController? ctrl = body.GetNodeOrNull<BlightController>("BlightController");
		if (ctrl != null)
		{
			ctrl.NotifyFilthFromHit(gain);
			return;
		}

		Blight? blight = body.GetNodeOrNull<Blight>("Blight");
		blight?.Add(gain);
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
