using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Прототипный HUD: HP игрока и ближайшего/назначенного врага.
/// Не финальный UI.
/// </summary>
public partial class DebugHud : CanvasLayer
{
	[Export]
	public NodePath PlayerPath { get; set; } = new("../Player");

	[Export]
	public NodePath EnemyPath { get; set; } = new("../BasicEnemy");

	private Label? _label;
	private Health? _playerHealth;
	private Health? _enemyHealth;

	public override void _Ready()
	{
		_label = new Label
		{
			Text = "HP —",
			Position = new Vector2(16, 16)
		};
		_label.AddThemeColorOverride("font_color", new Color(0.9f, 0.82f, 0.75f));
		_label.AddThemeFontSizeOverride("font_size", 18);
		AddChild(_label);

		ResolveTargets();
	}

	public override void _Process(double delta)
	{
		if (_playerHealth == null || !GodotObject.IsInstanceValid(_playerHealth))
		{
			ResolveTargets();
		}

		if (_enemyHealth == null || !GodotObject.IsInstanceValid(_enemyHealth))
		{
			TryResolveEnemy();
		}

		string playerText = _playerHealth != null
			? $"Player HP: {_playerHealth.Current}/{_playerHealth.MaxHealth}"
			: "Player HP: —";

		string enemyText = _enemyHealth != null && GodotObject.IsInstanceValid(_enemyHealth)
			? $"Enemy HP: {_enemyHealth.Current}/{_enemyHealth.MaxHealth}"
			: "Enemy HP: dead";

		if (_label != null)
		{
			_label.Text = $"{playerText}\n{enemyText}\nLMB — attack";
		}
	}

	private void ResolveTargets()
	{
		Node? player = GetNodeOrNull(PlayerPath);
		_playerHealth = player?.GetNodeOrNull<Health>("Health");

		TryResolveEnemy();
	}

	private void TryResolveEnemy()
	{
		Node? enemy = GetNodeOrNull(EnemyPath);
		if (enemy != null && GodotObject.IsInstanceValid(enemy))
		{
			_enemyHealth = enemy.GetNodeOrNull<Health>("Health");
			return;
		}

		Node? fallback = GetTree().GetFirstNodeInGroup("enemies");
		_enemyHealth = fallback?.GetNodeOrNull<Health>("Health");
	}
}
