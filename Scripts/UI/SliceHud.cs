using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Slice HUD (2.5): Health + FILTH bars. English copy only. No nearest-enemy panel.
/// </summary>
public partial class SliceHud : CanvasLayer
{
	[Export]
	public NodePath PlayerPath { get; set; } = new("../Player");

	[Export]
	public FontFile? DisplayFont { get; set; }

	[Export]
	public float HintVisibleSeconds { get; set; } = 6.5f;

	[Export]
	public float HintFadeSeconds { get; set; } = 1.5f;

	private Control? _root;
	private Label? _healthLabel;
	private Label? _filthLabel;
	private ProgressBar? _healthBar;
	private ProgressBar? _filthBar;
	private Control? _filthTrack;
	private ColorRect? _highMark;
	private Label? _hintLabel;
	private StyleBoxFlat? _healthFill;
	private StyleBoxFlat? _filthFill;
	private Health? _playerHealth;
	private Blight? _playerFilth;
	private float _pulse;
	private float _healthFlash;
	private float _hintAge;
	private bool _hintDone;
	private bool _healthHooked;

	private static readonly Color HealthFill = new(0.62f, 0.16f, 0.2f);
	private static readonly Color HealthFlash = new(0.95f, 0.82f, 0.78f);
	private static readonly Color FilthNormal = new(0.4f, 0.1f, 0.14f);
	private static readonly Color FilthHigh = new(0.78f, 0.48f, 0.14f);
	private static readonly Color FilthOverload = new(0.92f, 0.12f, 0.14f);
	private static readonly Color LabelColor = new(0.86f, 0.78f, 0.68f);

	public override void _Ready()
	{
		LoadFontIfNeeded();
		BuildUi();
		ResolvePlayer();
	}

	public override void _ExitTree()
	{
		UnhookHealth();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_pulse += dt * 4.2f;

		if (_playerHealth == null || !GodotObject.IsInstanceValid(_playerHealth))
		{
			ResolvePlayer();
		}

		UpdateHint(dt);
		if (_healthFlash > 0f)
		{
			_healthFlash = Mathf.Max(0f, _healthFlash - dt);
		}

		UpdateBars();
	}

	private void LoadFontIfNeeded()
	{
		if (DisplayFont != null)
		{
			return;
		}

		const string path = "res://Assets/UI/Fonts/Cinzel-Regular.ttf";
		if (ResourceLoader.Exists(path))
		{
			DisplayFont = GD.Load<FontFile>(path);
		}
	}

	private void BuildUi()
	{
		_root = new MarginContainer
		{
			AnchorRight = 0f,
			AnchorBottom = 0f,
			OffsetLeft = 20,
			OffsetTop = 18,
			OffsetRight = 340,
			OffsetBottom = 140
		};
		AddChild(_root);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 6);
		_root.AddChild(column);

		_healthLabel = MakeLabel("Health");
		column.AddChild(_healthLabel);

		var healthTrack = new Control { CustomMinimumSize = new Vector2(280, 12) };
		column.AddChild(healthTrack);
		_healthBar = MakeBar(HealthFill);
		_healthBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_healthFill = _healthBar.GetThemeStylebox("fill") as StyleBoxFlat;
		healthTrack.AddChild(_healthBar);

		_filthLabel = MakeLabel("FILTH");
		column.AddChild(_filthLabel);

		_filthTrack = new Control { CustomMinimumSize = new Vector2(280, 12) };
		column.AddChild(_filthTrack);
		_filthBar = MakeBar(FilthNormal);
		_filthBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_filthFill = _filthBar.GetThemeStylebox("fill") as StyleBoxFlat;
		_filthTrack.AddChild(_filthBar);

		_highMark = new ColorRect
		{
			Color = new Color(0.92f, 0.78f, 0.45f, 0.9f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Size = new Vector2(2, 12),
			ZIndex = 2
		};
		_filthTrack.AddChild(_highMark);

		_hintLabel = new Label
		{
			Text = "LMB strike  ·  RMB heavy (+FILTH)  ·  Altar cleanses",
			AnchorTop = 1f,
			AnchorBottom = 1f,
			AnchorLeft = 0f,
			AnchorRight = 0f,
			OffsetLeft = 20,
			OffsetTop = -36,
			OffsetRight = 560,
			OffsetBottom = -14
		};
		_hintLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.56f, 0.5f, 0.85f));
		_hintLabel.AddThemeFontSizeOverride("font_size", 12);
		if (DisplayFont != null)
		{
			_hintLabel.AddThemeFontOverride("font", DisplayFont);
		}

		AddChild(_hintLabel);
	}

	private Label MakeLabel(string text)
	{
		var label = new Label { Text = text };
		label.AddThemeColorOverride("font_color", LabelColor);
		label.AddThemeFontSizeOverride("font_size", 15);
		if (DisplayFont != null)
		{
			label.AddThemeFontOverride("font", DisplayFont);
		}

		return label;
	}

	private static ProgressBar MakeBar(Color fill)
	{
		var bar = new ProgressBar
		{
			CustomMinimumSize = new Vector2(280, 12),
			MaxValue = 100,
			Value = 0,
			ShowPercentage = false
		};

		var bg = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.04f, 0.05f, 0.78f),
			ContentMarginLeft = 1,
			ContentMarginTop = 1,
			ContentMarginRight = 1,
			ContentMarginBottom = 1
		};
		var fillBox = new StyleBoxFlat { BgColor = fill };
		bar.AddThemeStyleboxOverride("background", bg);
		bar.AddThemeStyleboxOverride("fill", fillBox);
		return bar;
	}

	private void UpdateHint(float dt)
	{
		if (_hintDone || _hintLabel == null)
		{
			return;
		}

		_hintAge += dt;
		if (_hintAge < HintVisibleSeconds)
		{
			return;
		}

		float t = (_hintAge - HintVisibleSeconds) / Mathf.Max(0.01f, HintFadeSeconds);
		if (t >= 1f)
		{
			_hintLabel.Visible = false;
			_hintDone = true;
			return;
		}

		Color c = _hintLabel.GetThemeColor("font_color");
		c.A = Mathf.Lerp(0.85f, 0f, t);
		_hintLabel.AddThemeColorOverride("font_color", c);
	}

	private void UpdateBars()
	{
		if (_healthBar != null && _healthLabel != null)
		{
			if (_playerHealth != null)
			{
				_healthBar.MaxValue = _playerHealth.MaxHealth;
				_healthBar.Value = _playerHealth.Current;
				_healthLabel.Text = $"Health  {_playerHealth.Current}/{_playerHealth.MaxHealth}";
				if (_healthFill != null)
				{
					float flashT = Mathf.Clamp(_healthFlash / 0.18f, 0f, 1f);
					_healthFill.BgColor = HealthFill.Lerp(HealthFlash, flashT);
				}
			}
			else
			{
				_healthBar.Value = 0;
				_healthLabel.Text = "Health  —";
			}
		}

		if (_filthBar == null || _filthLabel == null)
		{
			return;
		}

		if (_playerFilth == null || !GodotObject.IsInstanceValid(_playerFilth))
		{
			_filthBar.Value = 0;
			_filthLabel.Text = "FILTH  —";
			return;
		}

		_filthBar.MaxValue = _playerFilth.MaxBlight;
		_filthBar.Value = _playerFilth.Current;
		UpdateHighMark();

		Color fill = FilthNormal;
		string state = "";
		if (_playerFilth.IsOverloaded)
		{
			float pulse = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(_pulse));
			fill = new Color(
				FilthOverload.R * pulse,
				FilthOverload.G * pulse,
				FilthOverload.B * pulse,
				1f);
			state = "  OVERLOAD";
		}
		else if (_playerFilth.IsHigh)
		{
			fill = FilthHigh;
			state = "  HIGH";
		}

		if (_filthFill != null)
		{
			_filthFill.BgColor = fill;
		}

		_filthLabel.Text = $"FILTH  {_playerFilth.Current:0}/{_playerFilth.MaxBlight:0}{state}";
	}

	private void UpdateHighMark()
	{
		if (_highMark == null || _filthTrack == null || _playerFilth == null)
		{
			return;
		}

		float width = _filthTrack.Size.X;
		if (width < 1f)
		{
			width = _filthTrack.CustomMinimumSize.X;
		}

		float t = _playerFilth.MaxBlight <= 0f
			? 0.6f
			: Mathf.Clamp(_playerFilth.HighThreshold / _playerFilth.MaxBlight, 0f, 1f);
		_highMark.Position = new Vector2(width * t - 1f, 0f);
		_highMark.Size = new Vector2(2f, Mathf.Max(12f, _filthTrack.Size.Y));
	}

	private void ResolvePlayer()
	{
		UnhookHealth();

		Node3D? player = GetNodeOrNull(PlayerPath) as Node3D
			?? GetTree().GetFirstNodeInGroup("player") as Node3D;
		_playerHealth = player?.GetNodeOrNull<Health>("Health");
		_playerFilth = player?.GetNodeOrNull<Blight>("Blight");

		if (_playerHealth != null)
		{
			_playerHealth.Damaged += OnPlayerDamaged;
			_healthHooked = true;
		}
	}

	private void UnhookHealth()
	{
		if (_healthHooked && _playerHealth != null && GodotObject.IsInstanceValid(_playerHealth))
		{
			_playerHealth.Damaged -= OnPlayerDamaged;
		}

		_healthHooked = false;
	}

	private void OnPlayerDamaged(int amount, Vector3 sourcePosition)
	{
		_healthFlash = 0.18f;
	}
}
