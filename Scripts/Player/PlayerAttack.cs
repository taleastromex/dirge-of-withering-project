using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Ближний удар к курсору:
/// ЛКМ — обычный; RMB — тяжёлый (телеграф, больше урон/knockback, +Скверна).
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
	public float HeavyWindupTime { get; set; } = 0.22f;

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

	[Export]
	public float HeavyTelegraphScale { get; set; } = 1.35f;

	private AttackPhase _phase = AttackPhase.Idle;
	private float _phaseTimer;
	private bool _heavySwing;
	private Vector3 _hitboxBaseScale = Vector3.One;
	private StandardMaterial3D? _telegraphMaterial;
	private Color _defaultDebugColor = new(0.9f, 0.2f, 0.25f, 0.35f);
	private Color _heavyTelegraphColor = new(0.95f, 0.55f, 0.12f, 0.5f);

	public bool IsAttacking => _phase is AttackPhase.Windup or AttackPhase.Active;

	public bool LocksMovement => _phase is AttackPhase.Windup or AttackPhase.Active;

	public override void _Ready()
	{
		Hitbox ??= GetNodeOrNull<Hitbox3D>("../Visual/Hitbox");
		Health ??= GetNodeOrNull<Health>("../Health");
		BlightController ??= GetNodeOrNull<BlightController>("../BlightController");

		if (Hitbox != null)
		{
			_hitboxBaseScale = Hitbox.Scale;
			EnsureTelegraphMaterial();
		}

		ApplyHitboxTuning(heavy: false);
		Hitbox?.SetActive(false);
		SetTelegraphVisible(false);
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
					ResetHitboxVisual();
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
			ShowHeavyTelegraph();
		}
		else
		{
			SetTelegraphVisible(false);
			ResetHitboxVisual();
		}
	}

	private void BeginActive()
	{
		_phase = AttackPhase.Active;
		_phaseTimer = _heavySwing ? HeavyActiveTime : ActiveTime;
		ApplyHitboxTuning(_heavySwing);
		Hitbox?.SetActive(true);

		// Во время active оставляем увеличенный хитбокс для heavy, цвет — ударный.
		if (_heavySwing)
		{
			SetTelegraphColor(_defaultDebugColor);
		}
	}

	private void BeginCooldown()
	{
		_phase = AttackPhase.Cooldown;
		_phaseTimer = _heavySwing ? HeavyCooldownTime : CooldownTime;
		Hitbox?.SetActive(false);
		SetTelegraphVisible(false);
		ResetHitboxVisual();
	}

	private void EndAttackImmediate()
	{
		_phase = AttackPhase.Idle;
		_phaseTimer = 0f;
		_heavySwing = false;
		Hitbox?.SetActive(false);
		SetTelegraphVisible(false);
		ResetHitboxVisual();
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

	private void ShowHeavyTelegraph()
	{
		if (Hitbox == null)
		{
			return;
		}

		Hitbox.Scale = _hitboxBaseScale * HeavyTelegraphScale;
		SetTelegraphColor(_heavyTelegraphColor);
		SetTelegraphVisible(true);
	}

	private void ResetHitboxVisual()
	{
		if (Hitbox == null)
		{
			return;
		}

		Hitbox.Scale = _hitboxBaseScale;
		SetTelegraphColor(_defaultDebugColor);
	}

	private void EnsureTelegraphMaterial()
	{
		MeshInstance3D? mesh = Hitbox?.DebugMesh;
		if (mesh == null)
		{
			return;
		}

		if (mesh.GetActiveMaterial(0) is StandardMaterial3D shared)
		{
			_defaultDebugColor = shared.AlbedoColor;
			_telegraphMaterial = (StandardMaterial3D)shared.Duplicate();
		}
		else
		{
			_telegraphMaterial = new StandardMaterial3D
			{
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				AlbedoColor = _defaultDebugColor,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
			};
		}

		mesh.MaterialOverride = _telegraphMaterial;
	}

	private void SetTelegraphColor(Color color)
	{
		if (_telegraphMaterial != null)
		{
			_telegraphMaterial.AlbedoColor = color;
		}
	}

	private void SetTelegraphVisible(bool visible)
	{
		Hitbox?.SetDebugVisiblePublic(visible);
	}
}
