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
			key.LightEnergy = 0.55f;
		}

		if (GetNodeOrNull<DirectionalLight3D>("Lighting/FillLight") is { } fill)
		{
			fill.LightColor = ErrengardPalette.FillLight;
			fill.LightEnergy = 0.18f;
		}

		foreach (Node node in GetTree().GetNodesInGroup("ichor_glow"))
		{
			if (node is OmniLight3D omni)
			{
				omni.LightColor = ErrengardPalette.IchorCrimson;
				omni.LightEnergy = 1.4f;
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
			if (child is MeshInstance3D mesh)
			{
				ApplyMeshMaterial(mesh, child.GetParent()?.Name.ToString() ?? child.Name.ToString());
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
			|| name.Contains("Puddle", System.StringComparison.OrdinalIgnoreCase))
		{
			mat.AlbedoColor = ErrengardPalette.IchorCrimson;
			mat.Roughness = 0.3f;
			mat.Metallic = 0.2f;
			mat.EmissionEnabled = true;
			mat.Emission = ErrengardPalette.IchorCrimson;
			mat.EmissionEnergyMultiplier = 0.55f;
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
