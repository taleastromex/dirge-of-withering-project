using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Ближний удар к курсору (ЛКМ): wind-up → active frames → cooldown.
/// </summary>
public partial class PlayerAttack : Node
{
	private enum AttackPhase
	{
		Idle,
		Windup,
		Active,
		Cooldown
	}

	[Export]
	public Hitbox3D? Hitbox { get; set; }

	[Export]
	public Health? Health { get; set; }

	[Export]
	public float WindupTime { get; set; } = 0.08f;

	[Export]
	public float ActiveTime { get; set; } = 0.14f;

	[Export]
	public float CooldownTime { get; set; } = 0.35f;

	[Export]
	public int Damage { get; set; } = 25;

	private AttackPhase _phase = AttackPhase.Idle;
	private float _phaseTimer;

	public bool IsAttacking => _phase is AttackPhase.Windup or AttackPhase.Active;

	public override void _Ready()
	{
		Hitbox ??= GetNodeOrNull<Hitbox3D>("../Visual/Hitbox");
		Health ??= GetNodeOrNull<Health>("../Health");

		if (Hitbox != null)
		{
			Hitbox.Damage = Damage;
			Hitbox.SetActive(false);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Health != null && Health.IsDead)
		{
			EndAttackImmediate();
			return;
		}

		float dt = (float)delta;

		switch (_phase)
		{
			case AttackPhase.Idle:
				if (Input.IsActionJustPressed("attack"))
				{
					BeginWindup();
				}
				break;

			case AttackPhase.Windup:
				_phaseTimer -= dt;
				if (_phaseTimer <= 0f)
				{
					BeginActive();
				}
				break;

			case AttackPhase.Active:
				_phaseTimer -= dt;
				if (_phaseTimer <= 0f)
				{
					BeginCooldown();
				}
				break;

			case AttackPhase.Cooldown:
				_phaseTimer -= dt;
				if (_phaseTimer <= 0f)
				{
					_phase = AttackPhase.Idle;
				}
				break;
		}
	}

	private void BeginWindup()
	{
		_phase = AttackPhase.Windup;
		_phaseTimer = WindupTime;
		Hitbox?.SetActive(false);
	}

	private void BeginActive()
	{
		_phase = AttackPhase.Active;
		_phaseTimer = ActiveTime;

		if (Hitbox != null)
		{
			Hitbox.Damage = Damage;
			Hitbox.SetActive(true);
		}
	}

	private void BeginCooldown()
	{
		_phase = AttackPhase.Cooldown;
		_phaseTimer = CooldownTime;
		Hitbox?.SetActive(false);
	}

	private void EndAttackImmediate()
	{
		_phase = AttackPhase.Idle;
		_phaseTimer = 0f;
		Hitbox?.SetActive(false);
	}
}
