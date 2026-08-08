using System.Collections.Generic;
using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Если геометрия мира перекрывает игрока от камеры — делает её полупрозрачной
/// (через GeometryInstance3D.Transparency), затем возвращает обратно.
/// </summary>
public partial class CameraOcclusionFader : Node
{
	[Export]
	public Node3D? Target { get; set; }

	[Export]
	public Camera3D? Camera { get; set; }

	/// <summary>0 = непрозрачно, 1 = полностью прозрачно.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")]
	public float FadeTransparency { get; set; } = 0.65f;

	[Export]
	public float FadeLerpSpeed { get; set; } = 10f;

	/// <summary>Точки на теле цели для нескольких лучей (локальные смещения).</summary>
	[Export]
	public Vector3[] ProbeOffsets { get; set; } =
	{
		new(0f, 0.4f, 0f),
		new(0f, 1.0f, 0f),
		new(0f, 1.5f, 0f)
	};

	private readonly HashSet<GeometryInstance3D> _desiredFade = new();
	private readonly Dictionary<GeometryInstance3D, float> _currentFade = new();

	public override void _Ready()
	{
		Camera ??= GetParent()?.GetNodeOrNull<Camera3D>("Camera3D");
		Target ??= GetTree().CurrentScene?.GetNodeOrNull<Node3D>("Player");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Camera == null || Target == null)
		{
			return;
		}

		_desiredFade.Clear();
		CollectOccluders();

		float dt = (float)delta;
		UpdateFades(dt);
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

			// Несколько попаданий вдоль луча: стена может быть толстой / несколько объектов.
			const int maxHits = 6;
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
					// Сдвигаем луч чуть дальше и продолжаем (пол и т.п.).
					Vector3 pos = hit["position"].AsVector3();
					Vector3 dir = (to - origin).Normalized();
					query.From = pos + dir * 0.05f;
					continue;
				}

				RegisterFadeMeshes(collider);

				Vector3 hitPos = hit["position"].AsVector3();
				Vector3 rayDir = (to - origin).Normalized();
				query.From = hitPos + rayDir * 0.05f;
			}
		}
	}

	private static bool ShouldFadeCollider(Node collider)
	{
		string name = collider.Name;
		// Пол и декоративные лужи не гасим.
		if (name.ToString().Contains("Floor", System.StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (name.ToString().Contains("Puddle", System.StringComparison.OrdinalIgnoreCase))
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
		}

		foreach (Node child in collider.GetChildren())
		{
			if (child is GeometryInstance3D childGeo)
			{
				_desiredFade.Add(childGeo);
			}
		}
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
