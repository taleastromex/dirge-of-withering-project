using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Debug HUD для TestWorld / ребаланса: HP, FILTH, урон, resist.
/// </summary>
public partial class DebugHud : CanvasLayer
{
	[Export]
	public NodePath PlayerPath { get; set; } = new("../Player");

	private Label? _statusLabel;
	private ProgressBar? _hpBar;
	private ProgressBar? _blightBar;
	private Label? _hpLabel;
	private Label? _blightLabel;
	private Health? _playerHealth;
	private Blight? _playerBlight;
	private BlightController? _blightController;
	private PlayerAttack? _playerAttack;
	private Node3D? _playerNode;
	private BasicEnemy? _focusEnemy;
	private Health? _enemyHealth;

	private static readonly Color HpFill = new(0.65f, 0.18f, 0.22f);
	private static readonly Color BlightNormal = new(0.42f, 0.12f, 0.16f);
	private static readonly Color BlightHigh = new(0.78f, 0.48f, 0.14f);
	private static readonly Color BlightOverload = new(0.92f, 0.12f, 0.14f);

	public override void _Ready()
	{
		BuildUi();
		ResolveTargets();
	}

	public override void _Process(double delta)
	{
		if (_playerHealth == null || !GodotObject.IsInstanceValid(_playerHealth))
		{
			ResolvePlayer();
		}

		ResolveClosestEnemy();
		UpdateBars();
	}

	private void BuildUi()
	{
		var root = new VBoxContainer
		{
			Position = new Vector2(16, 16),
			CustomMinimumSize = new Vector2(340, 0)
		};
		AddChild(root);

		_hpLabel = MakeLabel("HP");
		root.AddChild(_hpLabel);
		_hpBar = MakeBar(HpFill);
		root.AddChild(_hpBar);

		_blightLabel = MakeLabel("Blight");
		root.AddChild(_blightLabel);
		_blightBar = MakeBar(BlightNormal);
		root.AddChild(_blightBar);

		_statusLabel = MakeLabel("—");
		_statusLabel.AddThemeFontSizeOverride("font_size", 14);
		root.AddChild(_statusLabel);
	}

	private static Label MakeLabel(string text)
	{
		var label = new Label { Text = text };
		label.AddThemeColorOverride("font_color", new Color(0.9f, 0.82f, 0.75f));
		label.AddThemeFontSizeOverride("font_size", 16);
		return label;
	}

	private static ProgressBar MakeBar(Color fill)
	{
		var bar = new ProgressBar
		{
			CustomMinimumSize = new Vector2(340, 18),
			MaxValue = 100,
			Value = 0,
			ShowPercentage = false
		};

		var bg = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.07f, 0.08f, 0.85f),
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomLeft = 2,
			CornerRadiusBottomRight = 2,
			ContentMarginLeft = 2,
			ContentMarginTop = 2,
			ContentMarginRight = 2,
			ContentMarginBottom = 2
		};
		var fillBox = new StyleBoxFlat
		{
			BgColor = fill,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomLeft = 2,
			CornerRadiusBottomRight = 2
		};
		bar.AddThemeStyleboxOverride("background", bg);
		bar.AddThemeStyleboxOverride("fill", fillBox);
		return bar;
	}

	private void UpdateBars()
	{
		if (_hpBar != null && _hpLabel != null)
		{
			if (_playerHealth != null)
			{
				_hpBar.MaxValue = _playerHealth.MaxHealth;
				_hpBar.Value = _playerHealth.Current;
				string iframe = _playerHealth.IsInvulnerable ? "  [i-frames]" : "";
				_hpLabel.Text =
					$"HP  {_playerHealth.Current}/{_playerHealth.MaxHealth}  resist {_playerHealth.PhysicalResist:0%}{iframe}";
			}
			else
			{
				_hpBar.Value = 0;
				_hpLabel.Text = "HP  —";
			}
		}

		if (_blightBar != null && _blightLabel != null)
		{
			if (_playerBlight != null && GodotObject.IsInstanceValid(_playerBlight))
			{
				_blightBar.MaxValue = _playerBlight.MaxBlight;
				_blightBar.Value = _playerBlight.Current;

				Color fill = BlightNormal;
				string state = "";
				if (_playerBlight.IsOverloaded)
				{
					fill = BlightOverload;
					state = "  OVERLOAD";
				}
				else if (_playerBlight.IsHigh)
				{
					fill = BlightHigh;
					state = "  HIGH";
				}

				SetBarFill(_blightBar, fill);
				_blightLabel.Text =
					$"FILTH  {_playerBlight.Current:0}/{_playerBlight.MaxBlight:0}{state}  ×{_playerBlight.DamageMultiplier:0.00}";
			}
			else
			{
				_blightBar.Value = 0;
				_blightLabel.Text = "FILTH  —";
			}
		}

		if (_statusLabel == null)
		{
			return;
		}

		string enemyText;
		if (_focusEnemy != null && GodotObject.IsInstanceValid(_focusEnemy) &&
			_enemyHealth != null && GodotObject.IsInstanceValid(_enemyHealth))
		{
			enemyText =
				$"{_focusEnemy.DisplayName}  {_enemyHealth.Current}/{_enemyHealth.MaxHealth}  resist {_enemyHealth.PhysicalResist:0%}";
		}
		else
		{
			enemyText = "Враги: —";
		}

		int light = _playerAttack?.ComputeOutgoingDamage(false) ?? 0;
		int heavy = _playerAttack?.ComputeOutgoingDamage(true) ?? 0;
		float drain = _blightController?.OverloadDrainPerSecond ?? 0f;
		int n = _playerAttack?.Damage ?? 0;

		_statusLabel.Text =
			$"{enemyText}\n" +
			$"N={n}  light→{light}  heavy→{heavy}  drain {drain:0}/s\n" +
			"LMB light | RMB Высвобождение (+FILTH) | алтарь очищает";
	}

	private static void SetBarFill(ProgressBar bar, Color color)
	{
		if (bar.GetThemeStylebox("fill") is StyleBoxFlat flat)
		{
			flat.BgColor = color;
		}
		else
		{
			var fillBox = new StyleBoxFlat
			{
				BgColor = color,
				CornerRadiusTopLeft = 2,
				CornerRadiusTopRight = 2,
				CornerRadiusBottomLeft = 2,
				CornerRadiusBottomRight = 2
			};
			bar.AddThemeStyleboxOverride("fill", fillBox);
		}
	}

	private void ResolveTargets()
	{
		ResolvePlayer();
		ResolveClosestEnemy();
	}

	private void ResolvePlayer()
	{
		_playerNode = GetNodeOrNull(PlayerPath) as Node3D
			?? GetTree().GetFirstNodeInGroup("player") as Node3D;
		_playerHealth = _playerNode?.GetNodeOrNull<Health>("Health");
		_playerBlight = _playerNode?.GetNodeOrNull<Blight>("Blight");
		_blightController = _playerNode?.GetNodeOrNull<BlightController>("BlightController");
		_playerAttack = _playerNode?.GetNodeOrNull<PlayerAttack>("PlayerAttack");
	}

	private void ResolveClosestEnemy()
	{
		_focusEnemy = null;
		_enemyHealth = null;

		if (_playerNode == null || !GodotObject.IsInstanceValid(_playerNode))
		{
			return;
		}

		float best = float.MaxValue;
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not BasicEnemy enemy || !GodotObject.IsInstanceValid(enemy))
			{
				continue;
			}

			Health? health = enemy.GetNodeOrNull<Health>("Health");
			if (health == null || health.IsDead)
			{
				continue;
			}

			float d = _playerNode.GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition);
			if (d < best)
			{
				best = d;
				_focusEnemy = enemy;
				_enemyHealth = health;
			}
		}
	}
}
