using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Связывает Скверну с игроком: набор от хитбоксов/heavy, drain на перегрузке, тинт мира, баффы.
/// </summary>
public partial class BlightController : Node
{
	[Export]
	public Blight? Blight { get; set; }

	[Export]
	public Health? Health { get; set; }

	[Export]
	public Player? Player { get; set; }

	/// <summary>Потолок FILTH от одного Высвобождения Ихора.</summary>
	[Export]
	public float HeavyFilthSoftCap { get; set; } = 20f;

	[Export]
	public float OverloadDrainPerSecond { get; set; } = 6f;

	/// <summary>Пассивный спад Скверны вне перегрузки (ед/сек).</summary>
	[Export]
	public float PassiveDecayPerSecond { get; set; } = 1f;

	/// <summary>Пауза после набора Скверны, прежде чем начнётся спад.</summary>
	[Export]
	public float DecayDelayAfterGain { get; set; } = 0.6f;

	[Export]
	public Color OverloadTint { get; set; } = new(0.45f, 0.08f, 0.1f);

	private WorldEnvironment? _worldEnvironment;
	private Godot.Environment? _environment;
	private Color _baseAmbient;
	private float _baseAmbientEnergy;
	private Color _baseBackground;
	private float _drainAccumulator;
	private float _decayDelayTimer;
	private bool _tintActive;
	private bool _environmentCached;

	public override void _Ready()
	{
		Blight ??= GetNodeOrNull<Blight>("../Blight") ?? GetParent()?.GetNodeOrNull<Blight>("Blight");
		Health ??= GetNodeOrNull<Health>("../Health") ?? GetParent()?.GetNodeOrNull<Health>("Health");
		Player ??= GetParent() as Player;

		// После SliceAtmosphere / полного входа сцены в дерево.
		CallDeferred(MethodName.CacheEnvironment);

		if (Blight != null)
		{
			Blight.OverloadStarted += OnOverloadStarted;
			Blight.OverloadEnded += OnOverloadEnded;
			Blight.BlightChanged += OnBlightChanged;
			CallDeferred(MethodName.UpdatePlayerBlightLook);
		}
	}

	public override void _ExitTree()
	{
		// Важно до ReloadCurrentScene: не оставляем тинт в shared Environment.
		RestoreEnvironment();
	}

	public override void _Process(double delta)
	{
		if (Blight == null || Health == null || Health.IsDead)
		{
			return;
		}

		float dt = (float)delta;
		UpdateWorldTint();
		ApplyPassiveDecay(dt);
		ApplyOverloadDrain(dt);
	}

	/// <summary>
	/// FILTH от удара (искажённые и т.п.). amount — уже после resist цели.
	/// </summary>
	public void NotifyFilthFromHit(float amount)
	{
		if (Blight == null || amount <= 0f)
		{
			return;
		}

		Blight.Add(amount);
		_decayDelayTimer = DecayDelayAfterGain;
	}

	/// <summary>
	/// Высвобождение Ихора: filth_self = min(0.5 × (2×N), SoftCap).
	/// </summary>
	public void NotifyHeavyAttackUsed(int lightBaseDamage)
	{
		if (Blight == null || lightBaseDamage <= 0)
		{
			return;
		}

		float releaseN = 2f * lightBaseDamage;
		float filth = Mathf.Min(0.5f * releaseN, HeavyFilthSoftCap);
		Blight.Add(filth);
		_decayDelayTimer = DecayDelayAfterGain;
	}

	public float GetDamageMultiplier() => Blight?.DamageMultiplier ?? 1f;

	public float GetSpeedMultiplier() => Blight?.SpeedMultiplier ?? 1f;

	private void ApplyPassiveDecay(float delta)
	{
		if (Blight == null || Blight.IsOverloaded || PassiveDecayPerSecond <= 0f)
		{
			return;
		}

		if (_decayDelayTimer > 0f)
		{
			_decayDelayTimer -= delta;
			return;
		}

		if (Blight.Current > 0f)
		{
			Blight.Remove(PassiveDecayPerSecond * delta);
		}
	}

	private void ApplyOverloadDrain(float delta)
	{
		if (Blight == null || Health == null || !Blight.IsOverloaded || OverloadDrainPerSecond <= 0f)
		{
			_drainAccumulator = 0f;
			return;
		}

		_drainAccumulator += OverloadDrainPerSecond * delta;
		int drain = Mathf.FloorToInt(_drainAccumulator);
		if (drain <= 0)
		{
			return;
		}

		_drainAccumulator -= drain;
		Health.ApplyDrain(drain);
	}

	private void OnOverloadStarted()
	{
		_tintActive = true;
		GameAudio.Instance?.PlaySfxOneShot(SliceAudioIds.FilthOverload, volumeDbOffset: -1f);
	}

	private void OnOverloadEnded()
	{
		_tintActive = false;
		RestoreEnvironment();
	}

	private void OnBlightChanged(float current, float max)
	{
		UpdateWorldTint();
		UpdatePlayerBlightLook();
	}

	private void CacheEnvironment()
	{
		_worldEnvironment = GetTree().CurrentScene?.GetNodeOrNull<WorldEnvironment>("WorldEnvironment");
		if (_worldEnvironment?.Environment == null)
		{
			return;
		}

		// Не мутируем subresource из .tscn — иначе тинт переживает ReloadCurrentScene.
		_environment = (Godot.Environment)_worldEnvironment.Environment.Duplicate();
		_worldEnvironment.Environment = _environment;

		// Каноническая база среза (и сброс, если кэш уже был загрязнён прошлым тинтом).
		_baseAmbient = ErrengardPalette.Ambient;
		_baseAmbientEnergy = 0.22f;
		_baseBackground = ErrengardPalette.Background;

		_environmentCached = true;
		RestoreEnvironment();
		UpdatePlayerBlightLook();
	}

	private void UpdateWorldTint()
	{
		if (!_environmentCached || _environment == null || Blight == null)
		{
			return;
		}

		float t = 0f;
		if (Blight.IsOverloaded)
		{
			t = 1f;
		}
		else if (Blight.IsHigh)
		{
			t = Mathf.InverseLerp(Blight.HighThreshold, Blight.MaxBlight, Blight.Current) * 0.55f;
		}

		if (t <= 0.001f && !_tintActive)
		{
			RestoreEnvironment();
			return;
		}

		_environment.AmbientLightColor = _baseAmbient.Lerp(OverloadTint, t);
		_environment.AmbientLightEnergy = Mathf.Lerp(_baseAmbientEnergy, _baseAmbientEnergy + 0.25f, t);
		_environment.BackgroundColor = _baseBackground.Lerp(OverloadTint * 0.35f, t * 0.8f);
	}

	private void RestoreEnvironment()
	{
		if (_environment == null)
		{
			return;
		}

		_environment.AmbientLightColor = _baseAmbient;
		_environment.AmbientLightEnergy = _baseAmbientEnergy;
		_environment.BackgroundColor = _baseBackground;
		_tintActive = false;
	}

	private void UpdatePlayerBlightLook()
	{
		if (Player == null || Blight == null)
		{
			return;
		}

		bool dead = Health != null && Health.IsDead;
		Player.SetBlightVisual(Blight.Normalized, Blight.IsOverloaded);
		Player.SetFilthAuraActive(!dead && Blight.IsHigh);
	}
}
