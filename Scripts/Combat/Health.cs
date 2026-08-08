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
	public delegate void DiedEventHandler();

	[Export]
	public int MaxHealth { get; set; } = 100;

	public int Current { get; private set; }

	public bool IsDead => Current <= 0;

	public override void _Ready()
	{
		Current = MaxHealth;
		EmitSignal(SignalName.HealthChanged, Current, MaxHealth);
	}

	/// <summary>Наносит урон. Возвращает false, если удар проигнорирован.</summary>
	public bool TakeDamage(int amount)
	{
		if (IsDead || amount <= 0)
		{
			return false;
		}

		Current = Mathf.Max(0, Current - amount);
		EmitSignal(SignalName.HealthChanged, Current, MaxHealth);

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
		EmitSignal(SignalName.HealthChanged, Current, MaxHealth);
	}
}
