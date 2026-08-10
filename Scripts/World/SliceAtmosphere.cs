using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Применяет палитру Эрренгарда к освещению и материалам сцены среза (2.1).
/// Ноды ищутся по имени — см. FloodedCathedralSlice.tscn.
/// </summary>
public partial class SliceAtmosphere : Node3D
{
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
				omni.LightColor = ErrengardPalette.IchorCrimson.Lightened(0.15f);
				omni.LightEnergy = 2.35f;
			}
		}

		if (GetNodeOrNull<OmniLight3D>("Lighting/AltarGlow") is { } altarGlow)
		{
			altarGlow.LightColor = ErrengardPalette.BoneYellow;
			altarGlow.LightEnergy = 1.1f;
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
			bool isSurface = mesh.Name.ToString().Contains("Surface", System.StringComparison.OrdinalIgnoreCase);
			bool isCore = mesh.Name.ToString().Contains("Core", System.StringComparison.OrdinalIgnoreCase);
			bool isStain = name.Contains("WetStain", System.StringComparison.OrdinalIgnoreCase);

			mat.AlbedoColor = isStain
				? new Color(0.12f, 0.06f, 0.08f, 0.55f)
				: isSurface
					? new Color(0.55f, 0.08f, 0.12f, 0.55f)
					: isCore
						? new Color(0.22f, 0.02f, 0.04f, 1f)
						: new Color(0.42f, 0.05f, 0.08f, 0.92f);
			mat.Transparency = isStain || isSurface || !isCore
				? BaseMaterial3D.TransparencyEnum.Alpha
				: BaseMaterial3D.TransparencyEnum.Disabled;
			mat.Roughness = isSurface ? 0.12f : isCore ? 0.45f : 0.22f;
			mat.Metallic = isSurface ? 0.55f : 0.28f;
			mat.EmissionEnabled = true;
			mat.Emission = isCore
				? new Color(0.9f, 0.12f, 0.16f)
				: ErrengardPalette.IchorCrimson.Lightened(isSurface ? 0.35f : 0.1f);
			mat.EmissionEnergyMultiplier = isCore ? 2.4f : isSurface ? 1.8f : isStain ? 0.25f : 1.15f;
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
}
