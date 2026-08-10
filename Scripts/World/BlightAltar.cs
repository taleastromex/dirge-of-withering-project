using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Sanctuary cleanse zone: standing here drains FILTH. Pulses visible altar/light feedback.
/// </summary>
public partial class BlightAltar : Area3D
{
	[Export]
	public float CleansePerSecond { get; set; } = 38f;

	[Export]
	public float EnterBurstCleanse { get; set; } = 22f;

	[Export]
	public MeshInstance3D? VisualMesh { get; set; }

	[Export]
	public OmniLight3D? AltarLight { get; set; }

	[Export]
	public NodePath ArtRootPath { get; set; } = new("../../../CathedralArt");

	[Export]
	public string AltarMeshName { get; set; } = "ApseAltar";

	[Export]
	public float IdleLightEnergy { get; set; } = 1.15f;

	[Export]
	public float ActiveLightEnergy { get; set; } = 3.4f;

	[Export]
	public float IdleEmission { get; set; } = 0.55f;

	[Export]
	public float ActiveEmission { get; set; } = 2.4f;

	private Blight? _playerBlight;
	private readonly System.Collections.Generic.List<StandardMaterial3D> _glowMaterials = new();
	private float _pulse;
	private float _activeBlend;
	private MeshInstance3D? _aura;

	public override void _Ready()
	{
		Monitoring = true;
		Monitorable = false;
		CollisionLayer = 0;
		CollisionMask = CombatLayers.Player;

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;

		AltarLight ??= GetTree().CurrentScene?.GetNodeOrNull<OmniLight3D>("Lighting/AltarGlow");
		CallDeferred(MethodName.BindVisuals);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_pulse += dt * 4.2f;

		bool cleansing = _playerBlight != null && GodotObject.IsInstanceValid(_playerBlight);
		_activeBlend = Mathf.MoveToward(_activeBlend, cleansing ? 1f : 0f, dt * 2.8f);

		float energy = Mathf.Lerp(IdleEmission, ActiveEmission, _activeBlend);
		energy += Mathf.Sin(_pulse) * Mathf.Lerp(0.08f, 0.45f, _activeBlend);
		foreach (StandardMaterial3D mat in _glowMaterials)
		{
			mat.EmissionEnabled = true;
			mat.Emission = ErrengardPalette.BoneYellow.Lightened(0.15f * _activeBlend);
			mat.EmissionEnergyMultiplier = energy;
		}

		if (AltarLight != null)
		{
			float light = Mathf.Lerp(IdleLightEnergy, ActiveLightEnergy, _activeBlend);
			light += Mathf.Sin(_pulse * 1.3f) * Mathf.Lerp(0.05f, 0.55f, _activeBlend);
			AltarLight.LightEnergy = light;
			AltarLight.LightColor = ErrengardPalette.BoneYellow.Lightened(0.2f * _activeBlend);
			AltarLight.OmniRange = Mathf.Lerp(5.5f, 8.5f, _activeBlend);
		}

		if (_aura?.MaterialOverride is ShaderMaterial auraMat)
		{
			float dens = Mathf.Lerp(0.35f, 0.95f, _activeBlend);
			auraMat.SetShaderParameter("density", dens);
			auraMat.SetShaderParameter(
				"emission_strength",
				Mathf.Lerp(0.25f, 0.85f, _activeBlend) + Mathf.Sin(_pulse) * 0.08f * _activeBlend);
		}

		if (!cleansing || CleansePerSecond <= 0f)
		{
			return;
		}

		_playerBlight!.Remove(CleansePerSecond * dt);
	}

	private void BindVisuals()
	{
		_glowMaterials.Clear();

		VisualMesh ??= ResolveAltarMesh();
		if (VisualMesh != null)
		{
			CaptureMeshMaterials(VisualMesh);
		}

		EnsureAura();
	}

	private MeshInstance3D? ResolveAltarMesh()
	{
		if (VisualMesh != null && VisualMesh.Visible)
		{
			return VisualMesh;
		}

		Node? art = GetNodeOrNull(ArtRootPath)
			?? GetTree().CurrentScene?.GetNodeOrNull("Geometry/CathedralArt");
		if (art == null)
		{
			return null;
		}

		return FindMeshByName(art, AltarMeshName);
	}

	private static MeshInstance3D? FindMeshByName(Node root, string meshName)
	{
		if (root is MeshInstance3D mi
			&& mi.Name.ToString().Equals(meshName, System.StringComparison.OrdinalIgnoreCase))
		{
			return mi;
		}

		foreach (Node child in root.GetChildren())
		{
			MeshInstance3D? found = FindMeshByName(child, meshName);
			if (found != null)
			{
				return found;
			}
		}

		return null;
	}

	private void CaptureMeshMaterials(MeshInstance3D mesh)
	{
		int surfaces = mesh.Mesh?.GetSurfaceCount() ?? 0;
		if (surfaces <= 0 && mesh.MaterialOverride is StandardMaterial3D single)
		{
			StandardMaterial3D dup = (StandardMaterial3D)single.Duplicate();
			dup.EmissionEnabled = true;
			dup.Emission = ErrengardPalette.BoneYellow;
			mesh.MaterialOverride = dup;
			_glowMaterials.Add(dup);
			return;
		}

		for (int i = 0; i < surfaces; i++)
		{
			Material? source = mesh.GetActiveMaterial(i);
			if (source is not StandardMaterial3D shared)
			{
				continue;
			}

			StandardMaterial3D dup = (StandardMaterial3D)shared.Duplicate();
			dup.EmissionEnabled = true;
			dup.Emission = ErrengardPalette.BoneYellow;
			dup.EmissionEnergyMultiplier = IdleEmission;
			mesh.SetSurfaceOverrideMaterial(i, dup);
			_glowMaterials.Add(dup);
		}
	}

	private void EnsureAura()
	{
		if (GetNodeOrNull("CleanseAura") is MeshInstance3D existing)
		{
			_aura = existing;
			return;
		}

		Shader? shader = GD.Load<Shader>("res://Assets/Shaders/IchorMist.gdshader");
		if (shader == null)
		{
			return;
		}

		var mat = new ShaderMaterial { Shader = shader };
		mat.SetShaderParameter("albedo", new Color(0.55f, 0.45f, 0.28f, 0.22f));
		mat.SetShaderParameter("soft_power", 3.2f);
		mat.SetShaderParameter("emission_strength", 0.3f);
		mat.SetShaderParameter("density", 0.4f);

		_aura = new MeshInstance3D
		{
			Name = "CleanseAura",
			Mesh = new SphereMesh
			{
				Radius = 1.8f,
				Height = 2.6f,
				IsHemisphere = true,
				RadialSegments = 24,
				Rings = 14
			},
			MaterialOverride = mat,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Position = new Vector3(0f, 0.05f, 0f)
		};
		AddChild(_aura);
	}

	private void OnBodyEntered(Node3D body)
	{
		if (body is not Player player)
		{
			return;
		}

		_playerBlight = player.GetNodeOrNull<Blight>("Blight");
		if (_playerBlight != null && EnterBurstCleanse > 0f)
		{
			_playerBlight.Remove(EnterBurstCleanse);
		}
	}

	private void OnBodyExited(Node3D body)
	{
		if (body is Player)
		{
			_playerBlight = null;
		}
	}
}
