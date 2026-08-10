using System.Collections.Generic;
using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Если геометрия мира перекрывает игрока от камеры — делает её полупрозрачной.
/// Стены гасятся целой стороной (WallL / WallR / Entrance / Apse), а не одним сегментом.
/// </summary>
public partial class CameraOcclusionFader : Node
{
	[Export]
	public Node3D? Target { get; set; }

	[Export]
	public Camera3D? Camera { get; set; }

	[Export]
	public NodePath ArtRootPath { get; set; } = new("../../Geometry/CathedralArt");

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float FadeTransparency { get; set; } = 0.88f;

	[Export]
	public float FadeLerpSpeed { get; set; } = 10f;

	[Export]
	public Vector3[] ProbeOffsets { get; set; } =
	{
		new(0f, 0.4f, 0f),
		new(0f, 1.0f, 0f),
		new(0f, 1.5f, 0f)
	};

	private readonly HashSet<GeometryInstance3D> _desiredFade = new();
	private readonly Dictionary<GeometryInstance3D, float> _currentFade = new();
	private readonly List<MeshInstance3D> _artMeshes = new();
	private readonly Dictionary<string, List<MeshInstance3D>> _wallSides = new();

	public override void _Ready()
	{
		Camera ??= GetParent()?.GetNodeOrNull<Camera3D>("Camera3D");
		Target ??= GetTree().CurrentScene?.GetNodeOrNull<Node3D>("Player");
		CallDeferred(MethodName.CacheArtMeshes);
	}

	private void CacheArtMeshes()
	{
		_artMeshes.Clear();
		_wallSides.Clear();
		Node? art = GetNodeOrNull(ArtRootPath)
			?? GetTree().CurrentScene?.GetNodeOrNull("Geometry/CathedralArt");
		if (art == null)
		{
			return;
		}

		CollectArtMeshes(art);
	}

	private void CollectArtMeshes(Node node)
	{
		if (node is MeshInstance3D mesh)
		{
			string name = mesh.Name.ToString();
			if (!name.StartsWith("Floor", System.StringComparison.OrdinalIgnoreCase))
			{
				_artMeshes.Add(mesh);
				string? side = WallSideKey(name);
				if (side != null)
				{
					if (!_wallSides.TryGetValue(side, out List<MeshInstance3D>? list))
					{
						list = new List<MeshInstance3D>();
						_wallSides[side] = list;
					}

					list.Add(mesh);
				}
			}
		}

		foreach (Node child in node.GetChildren())
		{
			CollectArtMeshes(child);
		}
	}

	/// <summary>WallL / WallR / EntranceWall / ApseWall — целая сторона.</summary>
	private static string? WallSideKey(string name)
	{
		if (name.StartsWith("WallL", System.StringComparison.Ordinal))
		{
			return "WallL";
		}

		if (name.StartsWith("WallR", System.StringComparison.Ordinal))
		{
			return "WallR";
		}

		if (name.StartsWith("EntranceWall", System.StringComparison.Ordinal))
		{
			return "EntranceWall";
		}

		if (name.StartsWith("ApseWall", System.StringComparison.Ordinal))
		{
			return "ApseWall";
		}

		return null;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Camera == null || Target == null)
		{
			return;
		}

		_desiredFade.Clear();
		CollectOccluders();
		UpdateFades((float)delta);
	}

	private void CollectOccluders()
	{
		PhysicsDirectSpaceState3D space = Camera!.GetWorld3D().DirectSpaceState;
		Vector3 origin = Camera.GlobalPosition;

		var exclude = new Godot.Collections.Array<Rid>();
		if (Target is CollisionObject3D body)
		{
			exclude.Add(body.GetRid());
		}

		foreach (Vector3 offset in ProbeOffsets)
		{
			Vector3 to = Target!.GlobalPosition + offset;
			var query = PhysicsRayQueryParameters3D.Create(origin, to);
			query.CollisionMask = CombatLayers.World;
			query.Exclude = exclude;

			const int maxHits = 8;
			for (int i = 0; i < maxHits; i++)
			{
				var hit = space.IntersectRay(query);
				if (hit.Count == 0)
				{
					break;
				}

				var collider = hit["collider"].AsGodotObject() as Node;
				if (collider == null)
				{
					break;
				}

				if (!ShouldFadeCollider(collider))
				{
					Vector3 pos = hit["position"].AsVector3();
					Vector3 dir = (to - origin).Normalized();
					query.From = pos + dir * 0.05f;
					continue;
				}

				RegisterFadeMeshes(collider);
				ExpandWallSideFrom(collider);

				Vector3 hitPos = hit["position"].AsVector3();
				Vector3 rayDir = (to - origin).Normalized();
				query.From = hitPos + rayDir * 0.05f;
			}

			RegisterArtMeshesAlongSegment(origin, to);
		}
	}

	private static bool ShouldFadeCollider(Node collider)
	{
		string name = collider.Name.ToString();
		if (name.Contains("Floor", System.StringComparison.OrdinalIgnoreCase) ||
			name.Contains("Puddle", System.StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return true;
	}

	private void RegisterFadeMeshes(Node collider)
	{
		if (collider is GeometryInstance3D geo)
		{
			_desiredFade.Add(geo);
			ExpandWallSideFrom(geo);
		}

		if (collider.GetParent() is GeometryInstance3D parentGeo)
		{
			_desiredFade.Add(parentGeo);
			ExpandWallSideFrom(parentGeo);
		}

		foreach (Node child in collider.GetChildren())
		{
			if (child is GeometryInstance3D childGeo)
			{
				_desiredFade.Add(childGeo);
				ExpandWallSideFrom(childGeo);
			}
		}

		// Legacy StaticBody WallLeft / WallRight → вся арт-сторона.
		string colliderName = collider.Name.ToString();
		if (colliderName.Contains("WallLeft", System.StringComparison.OrdinalIgnoreCase))
		{
			FadeWallSide("WallL");
		}
		else if (colliderName.Contains("WallRight", System.StringComparison.OrdinalIgnoreCase))
		{
			FadeWallSide("WallR");
		}
		else if (colliderName.Contains("WallEntrance", System.StringComparison.OrdinalIgnoreCase))
		{
			FadeWallSide("EntranceWall");
		}
		else if (colliderName.Contains("WallApse", System.StringComparison.OrdinalIgnoreCase))
		{
			FadeWallSide("ApseWall");
		}
	}

	private void ExpandWallSideFrom(Node node)
	{
		string? side = WallSideKey(node.Name.ToString());
		if (side != null)
		{
			FadeWallSide(side);
		}
	}

	private void FadeWallSide(string side)
	{
		if (!_wallSides.TryGetValue(side, out List<MeshInstance3D>? list))
		{
			return;
		}

		foreach (MeshInstance3D mesh in list)
		{
			if (GodotObject.IsInstanceValid(mesh))
			{
				_desiredFade.Add(mesh);
			}
		}
	}

	private void RegisterArtMeshesAlongSegment(Vector3 from, Vector3 to)
	{
		foreach (MeshInstance3D mesh in _artMeshes)
		{
			if (!GodotObject.IsInstanceValid(mesh) || !mesh.IsVisibleInTree())
			{
				continue;
			}

			Aabb world = (mesh.GlobalTransform * mesh.GetAabb()).Grow(0.12f);
			if (!SegmentIntersectsAabb(from, to, world))
			{
				continue;
			}

			_desiredFade.Add(mesh);
			ExpandWallSideFrom(mesh);
		}
	}

	private static bool SegmentIntersectsAabb(Vector3 from, Vector3 to, Aabb aabb)
	{
		Vector3 dir = to - from;
		Vector3 min = aabb.Position;
		Vector3 max = aabb.Position + aabb.Size;
		float tMin = 0f;
		float tMax = 1f;
		return ClipAxis(from.X, dir.X, min.X, max.X, ref tMin, ref tMax)
			&& ClipAxis(from.Y, dir.Y, min.Y, max.Y, ref tMin, ref tMax)
			&& ClipAxis(from.Z, dir.Z, min.Z, max.Z, ref tMin, ref tMax)
			&& tMax >= tMin;
	}

	private static bool ClipAxis(float origin, float dir, float min, float max, ref float tMin, ref float tMax)
	{
		if (Mathf.Abs(dir) < 1e-8f)
		{
			return origin >= min && origin <= max;
		}

		float inv = 1f / dir;
		float t1 = (min - origin) * inv;
		float t2 = (max - origin) * inv;
		if (t1 > t2)
		{
			(t1, t2) = (t2, t1);
		}

		tMin = Mathf.Max(tMin, t1);
		tMax = Mathf.Min(tMax, t2);
		return tMin <= tMax;
	}

	private void UpdateFades(float delta)
	{
		foreach (GeometryInstance3D geo in _desiredFade)
		{
			if (!GodotObject.IsInstanceValid(geo))
			{
				continue;
			}

			_currentFade.TryGetValue(geo, out float current);
			float next = Mathf.MoveToward(current, FadeTransparency, FadeLerpSpeed * delta);
			geo.Transparency = next;
			_currentFade[geo] = next;
		}

		var toClear = new List<GeometryInstance3D>();
		foreach (KeyValuePair<GeometryInstance3D, float> pair in _currentFade)
		{
			GeometryInstance3D geo = pair.Key;
			if (_desiredFade.Contains(geo) || !GodotObject.IsInstanceValid(geo))
			{
				if (!GodotObject.IsInstanceValid(geo))
				{
					toClear.Add(geo);
				}

				continue;
			}

			float next = Mathf.MoveToward(pair.Value, 0f, FadeLerpSpeed * delta);
			geo.Transparency = next;
			if (next <= 0.001f)
			{
				geo.Transparency = 0f;
				toClear.Add(geo);
			}
			else
			{
				_currentFade[geo] = next;
			}
		}

		foreach (GeometryInstance3D geo in toClear)
		{
			_currentFade.Remove(geo);
		}
	}
}
