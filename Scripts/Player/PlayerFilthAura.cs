using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Starlight aura around the player while FILTH is at/above the HIGH threshold.
/// Sketchfab pack is authored ~0.2 m wide — we auto-fit it to a readable ground ring.
/// </summary>
public partial class PlayerFilthAura : Node3D
{
	[Export]
	public string EffectPath { get; set; } =
		"res://Assets/ThirdParty/appearance-effect-starlight/Processed/StarlightEffect.gltf";

	/// <summary>Desired world diameter of the aura ring (meters).</summary>
	[Export]
	public float TargetDiameterMeters { get; set; } = 2.6f;

	[Export]
	public Vector3 LocalOffset { get; set; } = new(0f, 0.12f, 0f);

	[Export]
	public Color AuraColor { get; set; } = new(0.85f, 0.12f, 0.14f, 0.38f);

	/// <summary>Extra opacity multiplier for additive meshes (keeps the ring readable but soft).</summary>
	[Export(PropertyHint.Range, "0.05,1,0.01")]
	public float AuraIntensity { get; set; } = 0.42f;

	private Node3D? _effectRoot;
	private AnimationPlayer? _anim;
	private bool _wantVisible;

	public override void _Ready()
	{
		Position = LocalOffset;
		Visible = false;
		CallDeferred(nameof(BuildEffect));
	}

	public void SetHighFilthActive(bool active)
	{
		_wantVisible = active;
		if (_effectRoot == null)
		{
			return;
		}

		Visible = active;
		if (active)
		{
			RestartLoop();
		}
		else if (_anim != null && _anim.IsPlaying())
		{
			_anim.Stop();
		}
	}

	private void BuildEffect()
	{
		PackedScene? packed = GD.Load<PackedScene>(EffectPath);
		if (packed == null)
		{
			GD.PushWarning($"PlayerFilthAura: failed to load '{EffectPath}'.");
			return;
		}

		Node instance = packed.Instantiate();
		if (instance is not Node3D root)
		{
			instance.QueueFree();
			GD.PushWarning("PlayerFilthAura: effect root is not Node3D.");
			return;
		}

		_effectRoot = root;
		_effectRoot.Name = "StarlightEffect";
		AddChild(_effectRoot);

		ApplyRedMaterials(_effectRoot);
		FitEffectScale(_effectRoot);
		_anim = FindAnimationPlayer(_effectRoot);
		ConfigureLoop(_anim);

		if (_wantVisible)
		{
			Visible = true;
			RestartLoop();
		}
	}

	private void FitEffectScale(Node3D root)
	{
		Aabb? bounds = null;
		CollectMeshAabb(root, Transform3D.Identity, ref bounds);
		if (bounds == null)
		{
			root.Scale = Vector3.One * 14f;
			GD.Print("PlayerFilthAura: no mesh AABB, fallback scale=14.");
			return;
		}

		Aabb bb = bounds.Value;
		float diameter = Mathf.Max(bb.Size.X, Mathf.Max(bb.Size.Y, bb.Size.Z));
		float scale = diameter > 0.001f ? TargetDiameterMeters / diameter : 14f;
		root.Scale = Vector3.One * scale;

		// Center horizontally on the player; keep slightly above floor.
		Vector3 center = bb.GetCenter() * scale;
		root.Position = new Vector3(-center.X, -bb.Position.Y * scale, -center.Z);
		GD.Print($"PlayerFilthAura: sourceDiameter={diameter:0.###} fitScale={scale:0.##} target={TargetDiameterMeters:0.##}.");
	}

	private static void CollectMeshAabb(Node node, Transform3D parent, ref Aabb? bounds)
	{
		Transform3D local = parent;
		if (node is Node3D n3)
		{
			local = parent * n3.Transform;
		}

		if (node is MeshInstance3D mi && mi.Mesh != null)
		{
			Aabb meshAabb = local * mi.Mesh.GetAabb();
			bounds = bounds == null ? meshAabb : bounds.Value.Merge(meshAabb);
		}

		foreach (Node child in node.GetChildren())
		{
			CollectMeshAabb(child, local, ref bounds);
		}
	}

	private void RestartLoop()
	{
		if (_anim == null)
		{
			return;
		}

		string[] list = _anim.GetAnimationList();
		if (list.Length == 0)
		{
			return;
		}

		_anim.Play(list[0]);
	}

	private void ApplyRedMaterials(Node root)
	{
		Texture2D? star = GD.Load<Texture2D>(
			"res://Assets/ThirdParty/appearance-effect-starlight/textures/ATM_lo.png");
		Texture2D? beam = GD.Load<Texture2D>(
			"res://Assets/ThirdParty/appearance-effect-starlight/textures/guangshu.png");
		Texture2D? soft = GD.Load<Texture2D>(
			"res://Assets/ThirdParty/appearance-effect-starlight/textures/guangshushu4.png");
		Texture2D? ring = GD.Load<Texture2D>(
			"res://Assets/ThirdParty/appearance-effect-starlight/textures/longyuan.png");

		ApplyRedMaterialsRecursive(root, star, beam, soft, ring);
	}

	private void ApplyRedMaterialsRecursive(
		Node root,
		Texture2D? star,
		Texture2D? beam,
		Texture2D? soft,
		Texture2D? ring)
	{
		foreach (Node child in root.GetChildren())
		{
			ApplyRedMaterialsRecursive(child, star, beam, soft, ring);
		}

		if (root is not MeshInstance3D mesh)
		{
			return;
		}

		string meshName = mesh.Mesh?.ResourceName ?? string.Empty;
		string key = $"{mesh.Name} {meshName}".ToLowerInvariant();
		Texture2D? tex = PickTexture(key, star, beam, soft, ring);
		Color tint = new(
			AuraColor.R * AuraIntensity,
			AuraColor.G * AuraIntensity,
			AuraColor.B * AuraIntensity,
			Mathf.Clamp(AuraColor.A, 0.15f, 0.55f));
		var mat = new StandardMaterial3D
		{
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			AlbedoColor = tint,
			AlbedoTexture = tex,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
			DisableReceiveShadows = true,
		};
		mesh.MaterialOverride = mat;
		mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
	}

	private static Texture2D? PickTexture(
		string key,
		Texture2D? star,
		Texture2D? beam,
		Texture2D? soft,
		Texture2D? ring)
	{
		if (key.Contains("xuanwo") || key.Contains("yuan"))
		{
			return ring;
		}

		if (key.Contains("jianbian"))
		{
			return soft ?? beam;
		}

		if (key.Contains("shu"))
		{
			return beam;
		}

		return star;
	}

	private static void ConfigureLoop(AnimationPlayer? anim)
	{
		if (anim == null)
		{
			return;
		}

		foreach (string name in anim.GetAnimationList())
		{
			Animation? clip = anim.GetAnimation(name);
			if (clip != null)
			{
				clip.LoopMode = Animation.LoopModeEnum.Linear;
			}
		}
	}

	private static AnimationPlayer? FindAnimationPlayer(Node root)
	{
		if (root is AnimationPlayer ap)
		{
			return ap;
		}

		foreach (Node child in root.GetChildren())
		{
			AnimationPlayer? found = FindAnimationPlayer(child);
			if (found != null)
			{
				return found;
			}
		}

		return null;
	}
}
