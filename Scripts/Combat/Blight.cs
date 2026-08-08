using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Шкала Скверны (0–100): риск/награда Vertical Slice 2.2.
/// Высокая Скверна усиливает бой; 100% — перегрузка (drain HP снаружи).
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

	/// <summary>С этого порога включаются боевые баффы.</summary>
	[Export]
	public float HighThreshold { get; set; } = 60f;

	[Export]
	public float HighDamageBonus { get; set; } = 0.35f;

	[Export]
	public float HighSpeedBonus { get; set; } = 0.18f;

	[Export]
	public float OverloadDamageBonus { get; set; } = 0.25f;

	[Export]
	public float OverloadSpeedBonus { get; set; } = 0.12f;

	public float Current { get; private set; }

	public bool IsHigh => Current >= HighThreshold;

	public bool IsOverloaded => Current >= MaxBlight - 0.001f;

	public float Normalized => MaxBlight <= 0f ? 0f : Mathf.Clamp(Current / MaxBlight, 0f, 1f);

	public float DamageMultiplier
	{
		get
		{
			float m = 1f;
			if (IsHigh)
			{
				m += HighDamageBonus;
			}

			if (IsOverloaded)
			{
				m += OverloadDamageBonus;
			}

			return m;
		}
	}

	public float SpeedMultiplier
	{
		get
		{
			float m = 1f;
			if (IsHigh)
			{
				m += HighSpeedBonus;
			}

			if (IsOverloaded)
			{
				m += OverloadSpeedBonus;
			}

			return m;
		}
	}

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
}
