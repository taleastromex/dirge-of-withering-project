using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Reusable ash / ember / ground-smoke accents for the Flooded Cathedral slice.
/// Soft particle textures live under res://Assets/VFX/Particles/.
/// </summary>
public partial class SliceParticleAccents : Node3D
{
	private const string AshTexPath = "res://Assets/VFX/Particles/ash_soft.png";
	private const string EmberTexPath = "res://Assets/VFX/Particles/ash_ember.png";
	private const string SmokeTexPath = "res://Assets/VFX/Particles/smoke_soft.png";

	public override void _Ready()
	{
		Texture2D? ashTex = GD.Load<Texture2D>(AshTexPath);
		Texture2D? emberTex = GD.Load<Texture2D>(EmberTexPath);
		Texture2D? smokeTex = GD.Load<Texture2D>(SmokeTexPath);

		// Neutral ash near the entrance — kept away from ichor puddle A.
		SpawnAsh(
			"EntranceAsh",
			new Vector3(0f, 3.0f, 12.5f),
			new Color(0.7f, 0.6f, 0.48f, 0.75f),
			amount: 12,
			spread: 1.3f,
			ashTex);

		// Crimson ash only over ichor puddles.
		SpawnAsh(
			"IchorAshA",
			new Vector3(-1.2f, 1.8f, 4f),
			new Color(0.95f, 0.18f, 0.16f, 0.95f),
			amount: 20,
			spread: 1.1f,
			ashTex,
			quadSize: 0.15f);

		SpawnAsh(
			"IchorAshB",
			new Vector3(1.6f, 1.8f, -7.5f),
			new Color(0.9f, 0.16f, 0.14f, 0.92f),
			amount: 18,
			spread: 1.15f,
			ashTex,
			quadSize: 0.15f);

		// Warm gold/orange only at the apse / altar (far from puddle A).
		SpawnAsh(
			"AltarAsh",
			new Vector3(0f, 2.2f, -14.2f),
			new Color(1f, 0.78f, 0.28f, 0.9f),
			amount: 16,
			spread: 1.2f,
			ashTex,
			quadSize: 0.13f);

		SpawnAsh(
			"AltarEmbers",
			new Vector3(0f, 1.35f, -14.5f),
			new Color(1f, 0.62f, 0.18f, 0.98f),
			amount: 16,
			spread: 1.0f,
			emberTex,
			upward: true,
			quadSize: 0.11f);

		// Extra orange away from ichor A — side niche / right wall mid.
		SpawnAsh(
			"SideWarmAsh",
			new Vector3(4.8f, 2.4f, -1.5f),
			new Color(1f, 0.7f, 0.32f, 0.85f),
			amount: 12,
			spread: 1.0f,
			ashTex,
			quadSize: 0.13f);

		// Low smoke blankets: horizontal sheets (not camera billboards).
		// Skip altar apse (~z=-14..-16) — its own glow reads cleaner without fog.
		Vector3[] smokeSpots =
		{
			new(-3.6f, 0.18f, 5f),
			new(3.6f, 0.18f, 5f),
			new(-3.6f, 0.18f, -3f),
			new(3.6f, 0.18f, -3f),
			new(-5.4f, 0.16f, 8f),
			new(5.4f, 0.16f, -5f),
			new(-5.3f, 0.16f, 1f),
			new(5.3f, 0.16f, 1.5f),
			new(-2.2f, 0.15f, 13.2f),
			new(2.2f, 0.15f, 13.2f),
			new(-5.2f, 0.16f, -10f),
			new(5.2f, 0.16f, -12f),
		};

		for (int i = 0; i < smokeSpots.Length; i++)
		{
			SpawnGroundSmoke($"GroundSmoke_{i}", smokeSpots[i], smokeTex);
		}
	}

	private void SpawnAsh(
		string name,
		Vector3 position,
		Color color,
		int amount,
		float spread,
		Texture2D? texture,
		bool upward = false,
		float quadSize = 0.14f)
	{
		var particles = new GpuParticles3D
		{
			Name = name,
			Position = position,
			Amount = amount,
			Lifetime = 5.2f,
			VisibilityAabb = new Aabb(new Vector3(-5f, -3f, -5f), new Vector3(10f, 10f, 10f)),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};

		var mat = new ParticleProcessMaterial
		{
			Direction = upward ? new Vector3(0f, 1f, 0f) : new Vector3(0f, -0.12f, 0.05f),
			Spread = 32f,
			InitialVelocityMin = upward ? 0.12f : 0.04f,
			InitialVelocityMax = upward ? 0.5f : 0.22f,
			Gravity = upward ? new Vector3(0f, 0.04f, 0f) : new Vector3(0f, -0.1f, 0f),
			AngularVelocityMin = -25f,
			AngularVelocityMax = 25f,
			ScaleMin = 0.55f,
			ScaleMax = 1.15f,
			Color = color,
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
			EmissionBoxExtents = new Vector3(spread, 0.45f, spread),
		};
		particles.ProcessMaterial = mat;
		particles.DrawPass1 = MakeBillboardQuad(quadSize, texture);
		AddChild(particles);
	}

	private void SpawnGroundSmoke(string name, Vector3 position, Texture2D? texture)
	{
		var particles = new GpuParticles3D
		{
			Name = name,
			Position = position,
			Amount = 18,
			Lifetime = 9f,
			Explosiveness = 0.15f,
			VisibilityAabb = new Aabb(new Vector3(-6f, -1f, -6f), new Vector3(12f, 5f, 12f)),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};

		var mat = new ParticleProcessMaterial
		{
			Direction = new Vector3(0f, 0.15f, 0f),
			Spread = 70f,
			InitialVelocityMin = 0.01f,
			InitialVelocityMax = 0.06f,
			Gravity = Vector3.Zero,
			AngularVelocityMin = -8f,
			AngularVelocityMax = 8f,
			ScaleMin = 2.2f,
			ScaleMax = 4.4f,
			Color = new Color(0.55f, 0.5f, 0.46f, 0.72f),
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
			EmissionBoxExtents = new Vector3(1.35f, 0.08f, 1.35f),
		};
		particles.ProcessMaterial = mat;
		particles.DrawPass1 = MakeFloorSmokeQuad(1.15f, texture);
		AddChild(particles);
	}

	/// <summary>Quad lying on XZ so top-down camera sees a smoke blanket, not an edge-on billboard.</summary>
	private static ArrayMesh MakeFloorSmokeQuad(float size, Texture2D? texture)
	{
		float h = size * 0.5f;
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		// Two-sided XZ quad, normal +Y.
		Vector3[] corners =
		{
			new(-h, 0f, -h),
			new(h, 0f, -h),
			new(h, 0f, h),
			new(-h, 0f, h),
		};
		Vector2[] uvs =
		{
			new(0f, 0f),
			new(1f, 0f),
			new(1f, 1f),
			new(0f, 1f),
		};
		int[] indices = { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
		foreach (int i in indices)
		{
			st.SetNormal(Vector3.Up);
			st.SetUV(uvs[i]);
			st.AddVertex(corners[i]);
		}

		var drawMat = new StandardMaterial3D
		{
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			VertexColorUseAsAlbedo = true,
			AlbedoColor = Colors.White,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Disabled,
		};
		if (texture != null)
		{
			drawMat.AlbedoTexture = texture;
		}

		ArrayMesh mesh = st.Commit();
		mesh.SurfaceSetMaterial(0, drawMat);
		return mesh;
	}

	private static QuadMesh MakeBillboardQuad(float size, Texture2D? texture)
	{
		var mesh = new QuadMesh { Size = new Vector2(size, size) };
		var drawMat = new StandardMaterial3D
		{
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			VertexColorUseAsAlbedo = true,
			AlbedoColor = Colors.White,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
		};
		if (texture != null)
		{
			drawMat.AlbedoTexture = texture;
		}

		mesh.Material = drawMat;
		return mesh;
	}
}
