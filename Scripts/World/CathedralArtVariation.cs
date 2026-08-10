using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Ломает заметный тайлинг пола/стен CathedralArt: повороты плиток, UV-сдвиг, лёгкий тинт.
/// </summary>
public partial class CathedralArtVariation : Node
{
	[Export]
	public NodePath ArtRootPath { get; set; } = new("../CathedralArt");

	[Export]
	public int Seed { get; set; } = 2408;

	[Export(PropertyHint.Range, "0,0.35,0.01")]
	public float TintJitter { get; set; } = 0.14f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float WetTileChance { get; set; } = 0.28f;

	public override void _Ready()
	{
		Node? root = GetNodeOrNull(ArtRootPath);
		if (root == null)
		{
			GD.PushWarning("CathedralArtVariation: ArtRoot не найден.");
			return;
		}

		CallDeferred(MethodName.Apply, root);
	}

	private void Apply(Node root)
	{
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)Seed;

		Walk(root, rng);
	}

	private void Walk(Node node, RandomNumberGenerator rng)
	{
		if (node is MeshInstance3D mesh)
		{
			TryVary(mesh, rng);
		}

		foreach (Node child in node.GetChildren())
		{
			Walk(child, rng);
		}
	}

	private void TryVary(MeshInstance3D mesh, RandomNumberGenerator rng)
	{
		string name = mesh.Name.ToString();
		bool isFloor = name.StartsWith("Floor", System.StringComparison.OrdinalIgnoreCase);
		bool isWall = name.StartsWith("Wall", System.StringComparison.OrdinalIgnoreCase)
			|| name.StartsWith("EntranceWall", System.StringComparison.OrdinalIgnoreCase)
			|| name.StartsWith("ApseWall", System.StringComparison.OrdinalIgnoreCase);

		if (!isFloor && !isWall)
		{
			return;
		}

		if (isFloor)
		{
			int turns = rng.RandiRange(0, 3);
			if (turns > 0)
			{
				mesh.RotateY(Mathf.DegToRad(90f * turns));
			}
		}

		int surfaces = mesh.Mesh?.GetSurfaceCount() ?? 0;
		for (int i = 0; i < surfaces; i++)
		{
			Material? source = mesh.GetActiveMaterial(i);
			if (source is not BaseMaterial3D shared)
			{
				continue;
			}

			BaseMaterial3D mat = (BaseMaterial3D)shared.Duplicate();
			mat.Uv1Offset = new Vector3(rng.Randf(), rng.Randf(), 0f);
			float scaleJitter = rng.RandfRange(0.82f, 1.18f);
			mat.Uv1Scale = new Vector3(scaleJitter, scaleJitter, 1f);

			if (mat is StandardMaterial3D std)
			{
				Color albedo = std.AlbedoColor;
				float j = TintJitter;
				albedo.R = Mathf.Clamp(albedo.R + rng.RandfRange(-j, j), 0.05f, 1f);
				albedo.G = Mathf.Clamp(albedo.G + rng.RandfRange(-j * 0.7f, j * 0.7f), 0.05f, 1f);
				albedo.B = Mathf.Clamp(albedo.B + rng.RandfRange(-j * 0.7f, j * 0.7f), 0.05f, 1f);

				if (isFloor && rng.Randf() < WetTileChance)
				{
					albedo = albedo.Darkened(0.22f);
					albedo = albedo.Lerp(ErrengardPalette.IchorCrimson, 0.12f);
					std.Roughness = Mathf.Clamp(std.Roughness - 0.25f, 0.25f, 1f);
					std.Metallic = Mathf.Max(std.Metallic, 0.08f);
				}
				else if (isWall && rng.Randf() < 0.2f)
				{
					albedo = albedo.Darkened(0.18f);
					std.Roughness = Mathf.Clamp(std.Roughness + 0.05f, 0f, 1f);
				}

				std.AlbedoColor = albedo;
			}

			mesh.SetSurfaceOverrideMaterial(i, mat);
		}
	}
}
