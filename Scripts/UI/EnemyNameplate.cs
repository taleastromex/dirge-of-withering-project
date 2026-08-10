using Godot;

namespace DirgeOfWithering;

/// <summary>
/// World-space name + HP above an enemy. Screen-aligned billboard while aggro'd.
/// </summary>
public partial class EnemyNameplate : Node3D
{
	[Export]
	public Health? Health { get; set; }

	[Export]
	public string Title { get; set; } = "Enemy";

	[Export]
	public float HeightOffset { get; set; } = 2.15f;

	[Export]
	public FontFile? DisplayFont { get; set; }

	[Export]
	public bool AggroVisible { get; set; }

	[Export]
	public float FadeSpeed { get; set; } = 8f;

	private Label3D? _nameLabel;
	private MeshInstance3D? _barBg;
	private MeshInstance3D? _barFill;
	private StandardMaterial3D? _barBgMat;
	private StandardMaterial3D? _barFillMat;
	private Camera3D? _camera;
	private bool _built;
	private float _fade;

	public override void _Ready()
	{
		Position = new Vector3(0f, HeightOffset, 0f);
		Health ??= GetParent()?.GetNodeOrNull<Health>("Health");
		LoadFontIfNeeded();
		BuildVisuals();
		Visible = true;
		ApplyFade(0f);
	}

	public override void _Process(double delta)
	{
		if (!_built)
		{
			return;
		}

		_camera ??= GetViewport().GetCamera3D();
		bool wantShow = AggroVisible && IsOnScreen();
		float target = wantShow ? 1f : 0f;
		_fade = Mathf.MoveToward(_fade, target, (float)delta * FadeSpeed);
		ApplyFade(_fade);

		if (_fade <= 0.001f)
		{
			return;
		}

		AlignToCamera();
		UpdateHpBar();
	}

	public void SetAggroVisible(bool visible)
	{
		AggroVisible = visible;
	}

	public void SetTitle(string title)
	{
		Title = title;
		if (_nameLabel != null)
		{
			_nameLabel.Text = title;
		}
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

	private void BuildVisuals()
	{
		_nameLabel = new Label3D
		{
			Text = Title,
			FontSize = 28,
			PixelSize = 0.0036f,
			Modulate = new Color(0.9f, 0.82f, 0.72f),
			OutlineModulate = new Color(0.05f, 0.03f, 0.04f, 0.9f),
			OutlineSize = 6,
			Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
			Position = new Vector3(0f, 0.1f, 0f),
			NoDepthTest = true,
			RenderPriority = 10,
			FixedSize = false
		};
		if (DisplayFont != null)
		{
			_nameLabel.Font = DisplayFont;
		}

		AddChild(_nameLabel);

		_barBg = MakeBarMesh(
			size: new Vector2(0.95f, 0.055f),
			color: new Color(0.05f, 0.04f, 0.05f, 0.85f),
			priority: 10,
			mat: out _barBgMat);
		_barBg.Position = new Vector3(0f, -0.02f, 0f);
		AddChild(_barBg);

		_barFill = MakeBarMesh(
			size: new Vector2(0.92f, 0.038f),
			color: new Color(0.65f, 0.18f, 0.2f, 1f),
			priority: 11,
			mat: out _barFillMat);
		_barFill.Position = new Vector3(0f, -0.02f, 0.001f);
		AddChild(_barFill);

		_built = true;
	}

	private static MeshInstance3D MakeBarMesh(Vector2 size, Color color, int priority, out StandardMaterial3D mat)
	{
		var mesh = new QuadMesh { Size = size };
		mat = new StandardMaterial3D
		{
			AlbedoColor = color,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			NoDepthTest = true,
			RenderPriority = priority,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
		return new MeshInstance3D
		{
			Mesh = mesh,
			MaterialOverride = mat,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
	}

	private void ApplyFade(float a)
	{
		if (_nameLabel != null)
		{
			Color m = _nameLabel.Modulate;
			m.A = a;
			_nameLabel.Modulate = m;
			Color o = _nameLabel.OutlineModulate;
			o.A = 0.9f * a;
			_nameLabel.OutlineModulate = o;
		}

		if (_barBgMat != null)
		{
			Color c = _barBgMat.AlbedoColor;
			c.A = 0.85f * a;
			_barBgMat.AlbedoColor = c;
		}

		if (_barFillMat != null)
		{
			Color c = _barFillMat.AlbedoColor;
			c.A = a;
			_barFillMat.AlbedoColor = c;
		}
	}

	private void UpdateHpBar()
	{
		if (_barFill == null || Health == null || !GodotObject.IsInstanceValid(Health))
		{
			return;
		}

		float t = Health.MaxHealth <= 0 ? 0f : Mathf.Clamp((float)Health.Current / Health.MaxHealth, 0f, 1f);
		_barFill.Scale = new Vector3(Mathf.Max(t, 0.001f), 1f, 1f);
		_barFill.Position = new Vector3((t - 1f) * 0.46f, -0.02f, 0.001f);
	}

	private bool IsOnScreen()
	{
		if (_camera == null)
		{
			return false;
		}

		Vector3 world = GlobalPosition;
		if (!_camera.IsPositionInFrustum(world))
		{
			return false;
		}

		Vector3 local = _camera.ToLocal(world);
		return local.Z < -0.2f;
	}

	private void AlignToCamera()
	{
		if (_camera == null)
		{
			return;
		}

		Vector3 origin = GlobalPosition;
		Basis basis = _camera.GlobalTransform.Basis;
		GlobalTransform = new Transform3D(basis.Orthonormalized(), origin);
	}
}
