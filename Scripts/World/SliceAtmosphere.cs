using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Применяет палитру Эрренгарда к освещению и материалам сцены среза (2.1).
/// Ноды ищутся по имени — см. FloodedCathedralSlice.tscn.
/// </summary>
public partial class SliceAtmosphere : Node3D
{
	private static Shader? _ichorMistShader;
	private static Shader? _ichorBloodPoolShader;

	public override void _Ready()
	{
		ApplyEnvironment();
		ApplyLights();
		ApplyMaterialsRecursive(GetNodeOrNull("Geometry") ?? this);
	}

	private void ApplyEnvironment()
	{
		var worldEnv = GetNodeOrNull<WorldEnvironment>("WorldEnvironment");
		if (worldEnv?.Environment == null)
		{
			return;
		}

		Godot.Environment env = worldEnv.Environment;
		env.BackgroundColor = ErrengardPalette.Background;
		env.AmbientLightColor = ErrengardPalette.Ambient;
		env.AmbientLightEnergy = 0.22f;

		env.FogEnabled = true;
		env.FogMode = Godot.Environment.FogModeEnum.Depth;
		env.FogLightColor = new Color(0.12f, 0.1f, 0.11f);
		env.FogLightEnergy = 0.62f;
		env.FogDensity = 0.036f;
		env.FogAerialPerspective = 0.55f;
		env.FogSkyAffect = 0.45f;
		env.FogDepthBegin = 4.5f;
		env.FogDepthEnd = 36f;
		env.FogDepthCurve = 0.7f;

		env.AdjustmentEnabled = true;
		env.AdjustmentBrightness = 0.98f;
		env.AdjustmentContrast = 1.08f;
		env.AdjustmentSaturation = 0.82f;
	}

	private void ApplyLights()
	{
		if (GetNodeOrNull<DirectionalLight3D>("Lighting/KeyLight") is { } key)
		{
			key.LightColor = ErrengardPalette.KeyLight;
			key.LightEnergy = 0.62f;
		}

		if (GetNodeOrNull<DirectionalLight3D>("Lighting/FillLight") is { } fill)
		{
			fill.LightColor = ErrengardPalette.FillLight;
			fill.LightEnergy = 0.22f;
		}

		foreach (Node node in GetTree().GetNodesInGroup("ichor_glow"))
		{
			if (node is OmniLight3D omni)
			{
				omni.LightColor = ErrengardPalette.IchorCrimson.Lightened(0.1f);
				omni.LightEnergy = 2.2f;
			}
		}

		if (GetNodeOrNull<OmniLight3D>("Lighting/AltarGlow") is { } altarGlow)
		{
			altarGlow.LightColor = ErrengardPalette.BoneYellow;
			// Energy is driven by BlightAltar while cleansing; keep a calm idle baseline.
			if (altarGlow.LightEnergy < 0.5f)
			{
				altarGlow.LightEnergy = 1.15f;
			}
		}

		foreach (Node node in GetTree().GetNodesInGroup("breach_light"))
		{
			if (node is SpotLight3D spot)
			{
				spot.LightColor = ErrengardPalette.BoneYellow;
			}
		}
	}

	private static void ApplyMaterialsRecursive(Node root)
	{
		foreach (Node child in root.GetChildren())
		{
			// Sketchfab-сборка уже с PBR/тинтом — не затираем albedo палитрой.
			if (child.Name == "CathedralArt")
			{
				continue;
			}

			if (child is MeshInstance3D mesh)
			{
				string ownerName = child.GetParent()?.Name.ToString() ?? child.Name.ToString();
				string hint = $"{ownerName}/{mesh.Name}";
				ApplyMeshMaterial(mesh, hint);
			}

			ApplyMaterialsRecursive(child);
		}
	}

	private static void ApplyMeshMaterial(MeshInstance3D mesh, string ownerName)
	{
		string meshName = mesh.Name.ToString();
		bool mistVolume = meshName.Contains("Mist", System.StringComparison.OrdinalIgnoreCase)
			|| meshName.Contains("Haze", System.StringComparison.OrdinalIgnoreCase);
		if (mistVolume
			&& (ownerName.Contains("Ichor", System.StringComparison.OrdinalIgnoreCase)
				|| ownerName.Contains("Puddle", System.StringComparison.OrdinalIgnoreCase)))
		{
			ApplySoftMistMaterial(mesh, isHaze: meshName.Contains("Haze", System.StringComparison.OrdinalIgnoreCase));
			return;
		}

		if (meshName.Contains("Blood", System.StringComparison.OrdinalIgnoreCase)
			&& (ownerName.Contains("Ichor", System.StringComparison.OrdinalIgnoreCase)
				|| ownerName.Contains("Puddle", System.StringComparison.OrdinalIgnoreCase)))
		{
			ApplyBloodPoolMaterial(mesh);
			return;
		}

		StandardMaterial3D? mat = mesh.GetActiveMaterial(0) as StandardMaterial3D
			?? mesh.GetSurfaceOverrideMaterial(0) as StandardMaterial3D;

		if (mat == null)
		{
			mat = new StandardMaterial3D();
			mesh.SetSurfaceOverrideMaterial(0, mat);
		}
		else
		{
			// Не мутируем shared subresource — дублируем.
			mat = (StandardMaterial3D)mat.Duplicate();
			mesh.SetSurfaceOverrideMaterial(0, mat);
		}

		string name = ownerName;
		if (name.Contains("Floor", System.StringComparison.OrdinalIgnoreCase))
		{
			mat.AlbedoColor = ErrengardPalette.FloorAsh;
			mat.Roughness = 0.96f;
		}
		else if (name.Contains("Ichor", System.StringComparison.OrdinalIgnoreCase)
			|| name.Contains("Puddle", System.StringComparison.OrdinalIgnoreCase)
			|| name.Contains("WetStain", System.StringComparison.OrdinalIgnoreCase))
		{
			bool isSurface = meshName.Contains("Surface", System.StringComparison.OrdinalIgnoreCase);
			bool isCore = meshName.Contains("Core", System.StringComparison.OrdinalIgnoreCase);
			bool isStain = name.Contains("WetStain", System.StringComparison.OrdinalIgnoreCase);

			mat.AlbedoColor = isStain
				? new Color(0.18f, 0.05f, 0.07f, 0.7f)
				: isSurface
					? new Color(0.55f, 0.06f, 0.09f, 0.72f)
					: isCore
						? new Color(0.5f, 0.04f, 0.07f, 0.85f)
						: new Color(0.38f, 0.03f, 0.05f, 0.9f);
			mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
			mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
			mat.Roughness = isSurface ? 0.18f : 0.32f;
			mat.Metallic = isSurface ? 0.35f : 0.18f;
			mat.EmissionEnabled = true;
			mat.Emission = ErrengardPalette.IchorCrimson.Lightened(isSurface ? 0.25f : 0.08f);
			mat.EmissionEnergyMultiplier = isCore ? 1.35f : isSurface ? 1.0f : isStain ? 0.35f : 0.85f;
		}
		else if (name.Contains("Altar", System.StringComparison.OrdinalIgnoreCase)
			|| name.Contains("Dais", System.StringComparison.OrdinalIgnoreCase))
		{
			mat.AlbedoColor = ErrengardPalette.BoneYellow;
			mat.Roughness = 0.72f;
		}
		else if (name.Contains("Pillar", System.StringComparison.OrdinalIgnoreCase)
			|| name.Contains("Apse", System.StringComparison.OrdinalIgnoreCase)
			|| name.Contains("Entrance", System.StringComparison.OrdinalIgnoreCase)
			|| name.Contains("Gate", System.StringComparison.OrdinalIgnoreCase))
		{
			mat.AlbedoColor = ErrengardPalette.StoneDark;
			mat.Roughness = 0.94f;
		}
		else
		{
			mat.AlbedoColor = ErrengardPalette.Stone;
			mat.Roughness = 0.92f;
		}
	}

	private static void ApplySoftMistMaterial(MeshInstance3D mesh, bool isHaze)
	{
		_ichorMistShader ??= GD.Load<Shader>("res://Assets/Shaders/IchorMist.gdshader");
		if (_ichorMistShader == null)
		{
			return;
		}

		var mat = new ShaderMaterial { Shader = _ichorMistShader };
		mat.SetShaderParameter(
			"albedo",
			isHaze
				? new Color(0.28f, 0.04f, 0.07f, 0.12f)
				: new Color(0.34f, 0.05f, 0.09f, 0.16f));
		mat.SetShaderParameter("soft_power", isHaze ? 3.8f : 3.3f);
		mat.SetShaderParameter("emission_strength", isHaze ? 0.18f : 0.26f);
		mat.SetShaderParameter("density", isHaze ? 0.55f : 0.65f);
		mesh.MaterialOverride = mat;
		mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
	}

	private static void ApplyBloodPoolMaterial(MeshInstance3D mesh)
	{
		_ichorBloodPoolShader ??= GD.Load<Shader>("res://Assets/Shaders/IchorBloodPool.gdshader");

		string parentName = mesh.GetParent()?.Name.ToString() ?? string.Empty;
		bool puddleB = parentName.Contains("PuddleB", System.StringComparison.OrdinalIgnoreCase)
			|| parentName.EndsWith("B", System.StringComparison.Ordinal);

		Texture2D? bloodTex = GD.Load<Texture2D>(
			puddleB
				? "res://Assets/Textures/Ichor/ichor_blood_b.png"
				: "res://Assets/Textures/Ichor/ichor_blood_a.png");

		Color tint = puddleB
			? new Color(1.15f, 0.1f, 0.14f, 1f)
			: new Color(1.25f, 0.12f, 0.15f, 1f);

		// Prefer a dense unshaded shader; fall back to StandardMaterial3D if needed.
		if (_ichorBloodPoolShader != null && bloodTex != null)
		{
			var mat = new ShaderMaterial { Shader = _ichorBloodPoolShader };
			mat.SetShaderParameter("blood_tex", bloodTex);
			mat.SetShaderParameter("tint", tint);
			mat.SetShaderParameter("emission_strength", 0.7f);
			mat.SetShaderParameter("alpha_boost", 1.4f);
			mat.SetShaderParameter("edge_softness", 1.45f);
			mat.SetShaderParameter("core_opacity", 0.92f);
			mesh.MaterialOverride = mat;
		}
		else if (bloodTex != null)
		{
			var mat = new StandardMaterial3D
			{
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				CullMode = BaseMaterial3D.CullModeEnum.Disabled,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoTexture = bloodTex,
				AlbedoColor = tint,
				EmissionEnabled = true,
				EmissionTexture = bloodTex,
				Emission = new Color(0.7f, 0.06f, 0.1f),
				EmissionEnergyMultiplier = 0.9f,
			};
			mesh.MaterialOverride = mat;
		}

		mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		mesh.SortingOffset = 2f;
		mesh.Position = new Vector3(mesh.Position.X, 0.1f, mesh.Position.Z);
		mesh.Visible = true;

		EnsureBloodDecal(mesh, bloodTex, tint, puddleB);
	}

	private static void EnsureBloodDecal(MeshInstance3D mesh, Texture2D? bloodTex, Color tint, bool puddleB)
	{
		if (bloodTex == null || mesh.GetParent() is not Node3D parent)
		{
			return;
		}

		Decal? decal = parent.GetNodeOrNull<Decal>("BloodDecal");
		if (decal == null)
		{
			decal = new Decal { Name = "BloodDecal" };
			parent.AddChild(decal);
		}

		decal.TextureAlbedo = bloodTex;
		decal.TextureEmission = bloodTex;
		decal.Modulate = new Color(0.72f, 0.07f, 0.11f, 0.92f);
		decal.AlbedoMix = 0.92f;
		decal.EmissionEnergy = 0.85f;
		decal.CullMask = 0xFFFFF; // all layers
		decal.UpperFade = 0.45f;
		decal.LowerFade = 0.45f;
		decal.Size = puddleB
			? new Vector3(3.8f, 1.0f, 4.4f)
			: new Vector3(4.3f, 1.0f, 3.7f);
		decal.Position = new Vector3(mesh.Position.X, 0.35f, mesh.Position.Z);
		decal.Rotation = mesh.Rotation;
		decal.Visible = true;
	}
}
