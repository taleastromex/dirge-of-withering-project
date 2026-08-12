using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Общий компонент здоровья для игрока и врагов.
/// </summary>
public partial class Health : Node
{
	[Signal]
	public delegate void HealthChangedEventHandler(int current, int max);

	[Signal]
	public delegate void DamagedEventHandler(int amount, Vector3 sourcePosition);

	[Signal]
	public delegate void DiedEventHandler();

	[Export]
	public int MaxHealth { get; set; } = 100;

	/// <summary>Доля входящего физ. урона, которая отсекается (0…0.95).</summary>
	[Export(PropertyHint.Range, "0,0.95,0.01")]
	public float PhysicalResist { get; set; }

	/// <summary>Неуязвимость после получения урона (i-frames).</summary>
	[Export]
	public float InvulnerabilityDuration { get; set; } = 0.45f;

	public int Current { get; private set; }

	public bool IsDead => Current <= 0;

	public bool IsInvulnerable => _iframeTimer > 0f;

	private float _iframeTimer;

	public override void _Ready()
	{
		Current = MaxHealth;
		EmitSignal(SignalName.HealthChanged, Current, MaxHealth);
	}

	public override void _Process(double delta)
	{
		if (_iframeTimer > 0f)
		{
			_iframeTimer = Mathf.Max(0f, _iframeTimer - (float)delta);
		}
	}

	/// <summary>
	/// Наносит урон с учётом PhysicalResist.
	/// <paramref name="physicalResistIgnore"/> (0…1) снижает эффективный resist.
	/// Возвращает фактически снятый HP (0 если удар проигнорирован).
	/// </summary>
	public int TakeDamage(int amount, Vector3 sourcePosition = default, float physicalResistIgnore = 0f)
	{
		if (IsDead || amount <= 0 || IsInvulnerable)
		{
			return 0;
		}

		float ignore = Mathf.Clamp(physicalResistIgnore, 0f, 1f);
		float resist = Mathf.Clamp(PhysicalResist * (1f - ignore), 0f, 0.95f);
		int applied = Mathf.Max(1, Mathf.RoundToInt(amount * (1f - resist)));
		ApplyHpLoss(applied, grantIframes: true, sourcePosition);
		return applied;
	}

	/// <summary>
	/// Урон без i-frames и без PhysicalResist (перегрузка Скверны и т.п.).
	/// Не эмитит Damaged — чтобы не крутить набор Скверны от drain.
	/// </summary>
	public bool ApplyDrain(int amount)
	{
		if (IsDead || amount <= 0)
		{
			return false;
		}

		ApplyHpLoss(amount, grantIframes: false, sourcePosition: default, emitDamaged: false);
		return true;
	}

	private void ApplyHpLoss(int amount, bool grantIframes, Vector3 sourcePosition, bool emitDamaged = true)
	{
		Current = Mathf.Max(0, Current - amount);

		if (grantIframes)
		{
			_iframeTimer = InvulnerabilityDuration;
		}

		EmitSignal(SignalName.HealthChanged, Current, MaxHealth);

		if (emitDamaged)
		{
			EmitSignal(SignalName.Damaged, amount, sourcePosition);
		}

		if (Current <= 0)
		{
			EmitSignal(SignalName.Died);
		}
	}

	public void Heal(int amount)
	{
		if (IsDead || amount <= 0)
		{
			return;
		}

		Current = Mathf.Min(MaxHealth, Current + amount);
		EmitSignal(SignalName.HealthChanged, Current, MaxHealth);
	}

	public void HealFull()
	{
		if (IsDead)
		{
			return;
		}

		Current = MaxHealth;
		_iframeTimer = 0f;
		EmitSignal(SignalName.HealthChanged, Current, MaxHealth);
	}
}
