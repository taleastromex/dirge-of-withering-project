using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Ближний удар к курсору:
/// ЛКМ — обычный; RMB — тяжёлый (телеграф, больше урон/knockback, +Скверна).
/// Хитбокс синхронизирован с окном клипа в PlayerAnimDriver.
/// </summary>
public partial class PlayerAttack : Node
{
	private enum AttackPhase
	{
		Idle,
		Swing,
		Recovery
	}

	[Export]
	public Hitbox3D? Hitbox { get; set; }

	[Export]
	public Health? Health { get; set; }

	[Export]
	public BlightController? BlightController { get; set; }

	[Export]
	public PlayerAnimDriver? AnimDriver { get; set; }

	/// <summary>Запас, если клип ещё не готов (сек).</summary>
	[Export]
	public float AttackFallbackDuration { get; set; } = 1.2f;

	[Export]
	public float HeavyAttackFallbackDuration { get; set; } = 2.0f;

	[Export]
	public float RecoveryTime { get; set; } = 0.12f;

	[Export]
	public float HeavyRecoveryTime { get; set; } = 0.18f;

	/// <summary>Когда показать телеграф heavy (доля клипа), до окна урона.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")]
	public float HeavyTelegraphNormStart { get; set; } = 0.45f;

	/// <summary>База физ. урона light (N). Heavy = 2N.</summary>
	[Export]
	public int Damage { get; set; } = 40;

	/// <summary>Доля ихор-урона от базы N (канон: 0.5).</summary>
	[Export(PropertyHint.Range, "0,2,0.05")]
	public float IchorRatio { get; set; } = 0.5f;

	[Export]
	public float KnockbackForce { get; set; } = 9f;

	[Export]
	public float HitStopSeconds { get; set; } = 0.055f;

	[Export]
	public float HeavyKnockbackForce { get; set; } = 12f;

	[Export]
	public float HeavyHitStopSeconds { get; set; } = 0.07f;

	[Export]
	public float HeavyTelegraphScale { get; set; } = 1.35f;

	private AttackPhase _phase = AttackPhase.Idle;
	private float _phaseTimer;
	private bool _heavySwing;
	private bool _hitboxWasActive;
	private Vector3 _hitboxBaseScale = Vector3.One;
	private StandardMaterial3D? _telegraphMaterial;
	private Color _defaultDebugColor = new(0.9f, 0.2f, 0.25f, 0.35f);
	private Color _heavyTelegraphColor = new(0.95f, 0.55f, 0.12f, 0.5f);

	public bool IsAttacking => _phase is AttackPhase.Swing;

	public bool LocksMovement =>
		_phase is AttackPhase.Swing || (AnimDriver != null && AnimDriver.IsHurt);

	/// <summary>Cancel swing/recovery immediately (hit interrupt).</summary>
	public void Interrupt()
	{
		EndAttackImmediate();
	}

	public override void _Ready()
	{
		Hitbox ??= GetNodeOrNull<Hitbox3D>("../Visual/Hitbox");
		Health ??= GetNodeOrNull<Health>("../Health");
		BlightController ??= GetNodeOrNull<BlightController>("../BlightController");
		AnimDriver ??= GetNodeOrNull<PlayerAnimDriver>("../AnimDriver");

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
				if (AnimDriver != null && AnimDriver.IsHurt)
				{
					break;
				}

				if (Input.IsActionJustPressed("heavy_attack"))
				{
					BeginSwing(heavy: true);
				}
				else if (Input.IsActionJustPressed("attack"))
				{
					BeginSwing(heavy: false);
				}
				break;

			case AttackPhase.Swing:
				UpdateHeavyTelegraph();
				UpdateAttackHitbox();
				_phaseTimer -= dt;
				if (_phaseTimer <= 0f || (AnimDriver?.IsAttackFinished() ?? false))
				{
					BeginRecovery();
				}
				break;

			case AttackPhase.Recovery:
				_phaseTimer -= dt;
				if (_phaseTimer <= 0f)
				{
					_phase = AttackPhase.Idle;
					_heavySwing = false;
					AnimDriver?.NotifyAttackFinished();
					ResetHitboxVisual();
				}
				break;
		}
	}

	private void BeginSwing(bool heavy)
	{
		_heavySwing = heavy;
		_phase = AttackPhase.Swing;
		_hitboxWasActive = false;
		Hitbox?.SetActive(false);
		ApplyHitboxTuning(heavy);
		AnimDriver?.PlayAttack(heavy);

		float len = AnimDriver?.GetCurrentLength() ?? 0f;
		float fallback = heavy ? HeavyAttackFallbackDuration : AttackFallbackDuration;
		_phaseTimer = len > 0.05f ? len : fallback;

		if (heavy)
		{
			BlightController?.NotifyHeavyAttackUsed(Damage);
			GameAudio.Instance?.PlaySfxOneShot(
				SliceAudioIds.Pick(SliceAudioIds.SwingWhooshes),
				volumeDbOffset: -1f);
			GameAudio.Instance?.PlaySfxOneShot(
				SliceAudioIds.Pick(SliceAudioIds.HeavyAttackVoices),
				volumeDbOffset: -2f,
				pitchScale: 0.96f + GD.Randf() * 0.08f);
		}
		else
		{
			GameAudio.Instance?.PlaySfxOneShot(
				SliceAudioIds.Pick(SliceAudioIds.SwingWhooshes),
				volumeDbOffset: -3f,
				pitchScale: 1.02f + GD.Randf() * 0.08f);
		}

		SetTelegraphVisible(false);
		ResetHitboxVisual();
	}

	private void UpdateHeavyTelegraph()
	{
		if (!_heavySwing || Hitbox == null || AnimDriver == null || _hitboxWasActive)
		{
			return;
		}

		float len = AnimDriver.GetCurrentLength();
		if (len <= 0.01f)
		{
			return;
		}

		float t = AnimDriver.GetCurrentPosition() / len;
		bool show = t >= HeavyTelegraphNormStart && t < AnimDriver.HeavyAttackHitNormStart;
		if (show)
		{
			ShowHeavyTelegraph();
		}
		else if (t < HeavyTelegraphNormStart)
		{
			SetTelegraphVisible(false);
			ResetHitboxVisual();
		}
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

		if (shouldHit)
		{
			ApplyHitboxTuning(_heavySwing);
			if (_heavySwing)
			{
				SetTelegraphColor(_defaultDebugColor);
			}
		}
		else if (_heavySwing && _hitboxWasActive)
		{
			SetTelegraphVisible(false);
			ResetHitboxVisual();
		}

		Hitbox.SetActive(shouldHit);
		_hitboxWasActive = shouldHit;
	}

	private void BeginRecovery()
	{
		_phase = AttackPhase.Recovery;
		_phaseTimer = _heavySwing ? HeavyRecoveryTime : RecoveryTime;
		_hitboxWasActive = false;
		Hitbox?.SetActive(false);
		SetTelegraphVisible(false);
		ResetHitboxVisual();
	}

	private void EndAttackImmediate()
	{
		_phase = AttackPhase.Idle;
		_phaseTimer = 0f;
		_heavySwing = false;
		_hitboxWasActive = false;
		Hitbox?.SetActive(false);
		SetTelegraphVisible(false);
		ResetHitboxVisual();
		AnimDriver?.NotifyAttackFinished();
	}

	/// <summary>Итоговый урон в Hitbox: phys×filthMul + ichor (ichor без mul).</summary>
	public int ComputeOutgoingDamage(bool heavy)
	{
		int n = heavy ? Damage * 2 : Damage;
		float filthMul = BlightController?.GetDamageMultiplier() ?? 1f;
		float phys = n * filthMul;
		float ichor = IchorRatio * n;
		return Mathf.Max(1, Mathf.RoundToInt(phys + ichor));
	}

	private void ApplyHitboxTuning(bool heavy)
	{
		if (Hitbox == null)
		{
			return;
		}

		Hitbox.Damage = ComputeOutgoingDamage(heavy);
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
