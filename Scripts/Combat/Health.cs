using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Общий компонент здоровья для игрока и врагов.
/// TODO (этап 2): хуки под Скверну (урон от перегрузки, модификаторы).
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

	/// <summary>Наносит урон. Возвращает false, если удар проигнорирован.</summary>
	public bool TakeDamage(int amount, Vector3 sourcePosition = default)
	{
		if (IsDead || amount <= 0 || IsInvulnerable)
		{
			return false;
		}

		Current = Mathf.Max(0, Current - amount);
		_iframeTimer = InvulnerabilityDuration;

		EmitSignal(SignalName.HealthChanged, Current, MaxHealth);
		EmitSignal(SignalName.Damaged, amount, sourcePosition);

		if (Current <= 0)
		{
			EmitSignal(SignalName.Died);
		}

		return true;
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
