using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Прототипный HUD: HP игрока и текущего врага из группы enemies.
/// </summary>
public partial class DebugHud : CanvasLayer
{
	[Export]
	public NodePath PlayerPath { get; set; } = new("../Player");

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
			ResolvePlayer();
		}

		if (_enemyHealth == null || !GodotObject.IsInstanceValid(_enemyHealth))
		{
			ResolveEnemy();
		}

		string playerText = _playerHealth != null
			? $"Player HP: {_playerHealth.Current}/{_playerHealth.MaxHealth}"
			: "Player HP: —";

		string enemyText = _enemyHealth != null && GodotObject.IsInstanceValid(_enemyHealth)
			? $"Enemy HP: {_enemyHealth.Current}/{_enemyHealth.MaxHealth}"
			: "Enemy HP: respawning…";

		string iframe = _playerHealth != null && _playerHealth.IsInvulnerable ? " [i-frames]" : "";

		if (_label != null)
		{
			_label.Text = $"{playerText}{iframe}\n{enemyText}\nLMB — attack";
		}
	}

	private void ResolveTargets()
	{
		ResolvePlayer();
		ResolveEnemy();
	}

	private void ResolvePlayer()
	{
		Node? player = GetNodeOrNull(PlayerPath) ?? GetTree().GetFirstNodeInGroup("player");
		_playerHealth = player?.GetNodeOrNull<Health>("Health");
	}

	private void ResolveEnemy()
	{
		Node? enemy = GetTree().GetFirstNodeInGroup("enemies");
		_enemyHealth = enemy?.GetNodeOrNull<Health>("Health");
	}
}
