using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Vertical-slice death overlay: dim screen, "You Died", Restart button.
/// ProcessMode Always so UI works while the tree is paused.
/// </summary>
public partial class DeathScreen : CanvasLayer
{
	[Export]
	public FontFile? DisplayFont { get; set; }

	private Control? _root;
	private Label? _title;
	private Button? _restart;
	private bool _visibleUi;

	public override void _Ready()
	{
		Layer = 128;
		ProcessMode = ProcessModeEnum.Always;
		AddToGroup("death_screen");
		LoadFontIfNeeded();
		BuildUi();
		SetShown(false);
	}

	public void ShowDeathScreen()
	{
		SetShown(true);
		GetTree().Paused = true;
		_restart?.GrabFocus();
	}

	private void LoadFontIfNeeded()
	{
		if (DisplayFont != null)
		{
			return;
		}

		DisplayFont = GD.Load<FontFile>("res://Assets/UI/Fonts/Cinzel-Regular.ttf");
	}

	private void BuildUi()
	{
		_root = new Control
		{
			Name = "DeathRoot",
			ProcessMode = ProcessModeEnum.Always,
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		_root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		var dim = new ColorRect
		{
			Name = "Dim",
			Color = new Color(0.02f, 0.01f, 0.02f, 0.78f),
			ProcessMode = ProcessModeEnum.Always,
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(dim);

		var center = new CenterContainer
		{
			Name = "Center",
			ProcessMode = ProcessModeEnum.Always,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(center);

		var column = new VBoxContainer
		{
			Name = "Column",
			ProcessMode = ProcessModeEnum.Always,
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		column.AddThemeConstantOverride("separation", 36);
		center.AddChild(column);

		_title = new Label
		{
			Text = "You Died",
			HorizontalAlignment = HorizontalAlignment.Center,
			ProcessMode = ProcessModeEnum.Always,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		if (DisplayFont != null)
		{
			_title.AddThemeFontOverride("font", DisplayFont);
		}

		_title.AddThemeFontSizeOverride("font_size", 64);
		_title.AddThemeColorOverride("font_color", new Color(0.86f, 0.14f, 0.16f));
		_title.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.85f));
		_title.AddThemeConstantOverride("shadow_offset_x", 2);
		_title.AddThemeConstantOverride("shadow_offset_y", 3);
		column.AddChild(_title);

		_restart = new Button
		{
			Text = "Restart",
			CustomMinimumSize = new Vector2(200f, 48f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			ProcessMode = ProcessModeEnum.Always,
		};
		if (DisplayFont != null)
		{
			_restart.AddThemeFontOverride("font", DisplayFont);
		}

		_restart.AddThemeFontSizeOverride("font_size", 22);
		_restart.Pressed += OnRestartPressed;
		column.AddChild(_restart);
	}

	private void SetShown(bool shown)
	{
		_visibleUi = shown;
		if (_root != null)
		{
			_root.Visible = shown;
		}

		if (_title != null)
		{
			_title.Visible = shown;
		}

		if (_restart != null)
		{
			_restart.Visible = shown;
		}
	}

	private void OnRestartPressed()
	{
		GameAudio.Instance?.StopAllSfx();
		GetTree().Paused = false;
		GetTree().ReloadCurrentScene();
	}
}
