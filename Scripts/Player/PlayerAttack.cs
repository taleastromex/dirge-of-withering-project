using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Ближний удар к курсору:
/// ЛКМ — обычный; RMB — тяжёлый (больше урон/knockback, наполняет Скверну).
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
	public BlightController? BlightController { get; set; }

	[Export]
	public float WindupTime { get; set; } = 0.1f;

	[Export]
	public float ActiveTime { get; set; } = 0.12f;

	[Export]
	public float CooldownTime { get; set; } = 0.42f;

	[Export]
	public int Damage { get; set; } = 28;

	[Export]
	public float KnockbackForce { get; set; } = 9f;

	[Export]
	public float HitStopSeconds { get; set; } = 0.055f;

	[Export]
	public float HeavyWindupTime { get; set; } = 0.16f;

	[Export]
	public float HeavyActiveTime { get; set; } = 0.16f;

	[Export]
	public float HeavyCooldownTime { get; set; } = 0.55f;

	[Export]
	public int HeavyDamage { get; set; } = 42;

	[Export]
	public float HeavyKnockbackForce { get; set; } = 12f;

	[Export]
	public float HeavyHitStopSeconds { get; set; } = 0.07f;

	private AttackPhase _phase = AttackPhase.Idle;
	private float _phaseTimer;
	private bool _heavySwing;

	public bool IsAttacking => _phase is AttackPhase.Windup or AttackPhase.Active;

	public bool LocksMovement => _phase is AttackPhase.Windup or AttackPhase.Active;

	public override void _Ready()
	{
		Hitbox ??= GetNodeOrNull<Hitbox3D>("../Visual/Hitbox");
		Health ??= GetNodeOrNull<Health>("../Health");
		BlightController ??= GetNodeOrNull<BlightController>("../BlightController");
		ApplyHitboxTuning(heavy: false);
		Hitbox?.SetActive(false);
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
				if (Input.IsActionJustPressed("heavy_attack"))
				{
					BeginWindup(heavy: true);
				}
				else if (Input.IsActionJustPressed("attack"))
				{
					BeginWindup(heavy: false);
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
					_heavySwing = false;
				}
				break;
		}
	}

	private void BeginWindup(bool heavy)
	{
		_heavySwing = heavy;
		_phase = AttackPhase.Windup;
		_phaseTimer = heavy ? HeavyWindupTime : WindupTime;
		Hitbox?.SetActive(false);

		if (heavy)
		{
			BlightController?.NotifyHeavyAttackUsed();
		}
	}

	private void BeginActive()
	{
		_phase = AttackPhase.Active;
		_phaseTimer = _heavySwing ? HeavyActiveTime : ActiveTime;
		ApplyHitboxTuning(_heavySwing);
		Hitbox?.SetActive(true);
	}

	private void BeginCooldown()
	{
		_phase = AttackPhase.Cooldown;
		_phaseTimer = _heavySwing ? HeavyCooldownTime : CooldownTime;
		Hitbox?.SetActive(false);
	}

	private void EndAttackImmediate()
	{
		_phase = AttackPhase.Idle;
		_phaseTimer = 0f;
		_heavySwing = false;
		Hitbox?.SetActive(false);
	}

	private void ApplyHitboxTuning(bool heavy)
	{
		if (Hitbox == null)
		{
			return;
		}

		float blightMul = BlightController?.GetDamageMultiplier() ?? 1f;
		int baseDamage = heavy ? HeavyDamage : Damage;
		Hitbox.Damage = Mathf.RoundToInt(baseDamage * blightMul);
		Hitbox.KnockbackForce = heavy ? HeavyKnockbackForce : KnockbackForce;
		Hitbox.HitStopSeconds = heavy ? HeavyHitStopSeconds : HitStopSeconds;
	}
}
