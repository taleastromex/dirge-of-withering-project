using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Шкала Скверны (0–100): риск/награда.
/// Одна ступенчатая кривая урона/скорости (Concepts/DAMAGE.md, этап 3).
/// </summary>
public partial class Blight : Node
{
	[Signal]
	public delegate void BlightChangedEventHandler(float current, float max);

	[Signal]
	public delegate void OverloadStartedEventHandler();

	[Signal]
	public delegate void OverloadEndedEventHandler();

	[Export]
	public float MaxBlight { get; set; } = 100f;

	/// <summary>Порог HIGH / первой ступени баффа (канон: 50%).</summary>
	[Export]
	public float HighThreshold { get; set; } = 50f;

	public float Current { get; private set; }

	public bool IsHigh => Current >= HighThreshold;

	public bool IsOverloaded => Current >= MaxBlight - 0.001f;

	public float Normalized => MaxBlight <= 0f ? 0f : Mathf.Clamp(Current / MaxBlight, 0f, 1f);

	/// <summary>Доля шкалы 0…1 для ступеней.</summary>
	public float Percent => Normalized;

	/// <summary>Бонус к физ. урону (0…1), не включая базу.</summary>
	public float FilthDamageBonus => ResolveDamageBonus(Percent);

	public float DamageMultiplier => 1f + FilthDamageBonus;

	public float SpeedMultiplier => 1f + ResolveSpeedBonus(Percent);

	private bool _wasOverloaded;

	public override void _Ready()
	{
		EmitSignal(SignalName.BlightChanged, Current, MaxBlight);
	}

	public void Add(float amount)
	{
		if (amount <= 0f)
		{
			return;
		}

		SetBlight(Current + amount);
	}

	public void Remove(float amount)
	{
		if (amount <= 0f)
		{
			return;
		}

		SetBlight(Current - amount);
	}

	public void SetBlight(float value)
	{
		float previous = Current;
		Current = Mathf.Clamp(value, 0f, MaxBlight);

		if (!Mathf.IsEqualApprox(previous, Current))
		{
			EmitSignal(SignalName.BlightChanged, Current, MaxBlight);
		}

		bool overloaded = IsOverloaded;
		if (overloaded && !_wasOverloaded)
		{
			_wasOverloaded = true;
			EmitSignal(SignalName.OverloadStarted);
		}
		else if (!overloaded && _wasOverloaded)
		{
			_wasOverloaded = false;
			EmitSignal(SignalName.OverloadEnded);
		}
	}

	/// <summary>
	/// Ступени: &lt;50% → 0 | ≥50 → 0.25 | ≥75 → 0.50 | ≥90 → 0.75 | 100% → 1.0
	/// </summary>
	public static float ResolveDamageBonus(float percent01)
	{
		float p = Mathf.Clamp(percent01, 0f, 1f);
		if (p >= 0.999f)
		{
			return 1f;
		}

		if (p >= 0.90f)
		{
			return 0.75f;
		}

		if (p >= 0.75f)
		{
			return 0.50f;
		}

		if (p >= 0.50f)
		{
			return 0.25f;
		}

		return 0f;
	}

	/// <summary>≥50 → +10% | ≥75 → +15% | 100% → +20%</summary>
	public static float ResolveSpeedBonus(float percent01)
	{
		float p = Mathf.Clamp(percent01, 0f, 1f);
		if (p >= 0.999f)
		{
			return 0.20f;
		}

		if (p >= 0.75f)
		{
			return 0.15f;
		}

		if (p >= 0.50f)
		{
			return 0.10f;
		}

		return 0f;
	}
}
