using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Вешает StaticBody3D на меши CathedralArt.
/// Платформа святилища — тонкая плита; алтарь/арки/вход — trimesh по геометрии.
/// </summary>
public partial class CathedralArtCollision : Node
{
	[Export]
	public NodePath ArtRootPath { get; set; } = new("Geometry/CathedralArt");

	[Export]
	public float MinAxis { get; set; } = 0.15f;

	[Export]
	public float WideFootprint { get; set; } = 4.5f;

	[Export]
	public float ThinSlabHeight { get; set; } = 0.28f;

	public override void _Ready()
	{
		Node? root = GetNodeOrNull(ArtRootPath);
		if (root == null)
		{
			GD.PushWarning("CathedralArtCollision: ArtRoot не найден.");
			return;
		}

		CallDeferred(MethodName.BuildColliders, root);
	}

	private void BuildColliders(Node root)
	{
		foreach (Node node in root.GetChildren())
		{
			Walk(node);
		}
	}

	private void Walk(Node node)
	{
		if (node is MeshInstance3D mesh)
		{
			TryAddCollider(mesh);
		}

		foreach (Node child in node.GetChildren())
		{
			Walk(child);
		}
	}

	private void TryAddCollider(MeshInstance3D mesh)
	{
		string name = mesh.Name.ToString();
		if (name.StartsWith("Floor", System.StringComparison.OrdinalIgnoreCase)
			|| name.StartsWith("HangingLantern", System.StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		if (mesh.Mesh == null || mesh.GetNodeOrNull("ArtCollider") != null)
		{
			return;
		}

		Aabb aabb = mesh.GetAabb();
		Vector3 size = aabb.Size;
		if (size.X < MinAxis && size.Y < MinAxis && size.Z < MinAxis)
		{
			return;
		}

		var body = new StaticBody3D
		{
			Name = "ArtCollider",
			CollisionLayer = CombatLayers.World,
			CollisionMask = 0
		};

		CollisionShape3D shapeNode;
		if (IsSanctumSolid(name))
		{
			// Trimesh = нельзя пройти сквозь алтарь/арки; без «крыши» от AABB.
			Shape3D? trimesh = mesh.Mesh.CreateTrimeshShape();
			shapeNode = trimesh == null
				? MakeBoxCollider(aabb)
				: new CollisionShape3D { Name = "CollisionShape3D", Shape = trimesh };
		}
		else
		{
			shapeNode = MakeBoxCollider(aabb);
		}

		mesh.AddChild(body);
		body.AddChild(shapeNode);
	}

	private static CollisionShape3D MakeBoxCollider(Aabb aabb)
	{
		return new CollisionShape3D
		{
			Name = "CollisionShape3D",
			Shape = new BoxShape3D { Size = aabb.Size },
			Position = aabb.GetCenter()
		};
	}

	/// <summary>Крупные пропы апсиды — trimesh при сложной форме; кубы и так ок как box.</summary>
	private static bool IsSanctumSolid(string name)
	{
		if (name.StartsWith("EntranceWall", System.StringComparison.Ordinal)
			|| name.StartsWith("ApseWall", System.StringComparison.Ordinal)
			|| name.StartsWith("Wall", System.StringComparison.Ordinal))
		{
			return false;
		}

		// Сложные формы апсиды — trimesh; пилоны остаются box AABB.
		return name.StartsWith("ApseRuin", System.StringComparison.OrdinalIgnoreCase)
			|| name.Equals("ApseAltar", System.StringComparison.OrdinalIgnoreCase);
	}
}
