using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Мост AI → AnimationPlayer. Локомоция лупится, бой нативным темпом, death держит позу.
/// </summary>
public partial class EnemyAnimDriver : Node
{
	public enum AnimKind
	{
		None,
		Idle,
		Chase,
		Telegraph,
		Attack,
		Stagger,
		Death
	}

	[Export]
	public AnimationPlayer? AnimPlayer { get; set; }

	[Export] public string IdleClip { get; set; } = "idle";
	[Export] public string ChaseClip { get; set; } = "run";
	[Export] public string TelegraphClip { get; set; } = "stagger";
	[Export] public string AttackClip { get; set; } = "attack";
	[Export] public string StaggerClip { get; set; } = "stagger";
	[Export] public string DeathClip { get; set; } = "death";

	/// <summary>Доля длины attack-клипа, когда хитбокс активен (0..1).</summary>
	[Export(PropertyHint.Range, "0,1,0.01")]
	public float AttackHitNormStart { get; set; } = 0.48f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float AttackHitNormEnd { get; set; } = 0.62f;

	private AnimKind _kind = AnimKind.None;
	private string _current = "";
	private bool _bound;
	private bool _deathPoseHeld;

	public AnimKind CurrentKind => _kind;

	public override void _Ready()
	{
		CallDeferred(MethodName.BindPlayer);
	}

	public void BindPlayer()
	{
		if (AnimPlayer == null)
		{
			AnimPlayer = FindAnimationPlayer(GetParent() ?? this);
		}

		if (AnimPlayer == null || _bound)
		{
			return;
		}

		ConfigureLoop(IdleClip, loop: true);
		ConfigureLoop(ChaseClip, loop: true);
		ConfigureLoop(TelegraphClip, loop: false);
		ConfigureLoop(AttackClip, loop: false);
		ConfigureLoop(StaggerClip, loop: false);
		ConfigureLoop(DeathClip, loop: false);
		AnimPlayer.SpeedScale = 1f;
		AnimPlayer.AnimationFinished += OnAnimationFinished;
		_bound = true;
	}

	public override void _ExitTree()
	{
		if (AnimPlayer != null && _bound)
		{
			AnimPlayer.AnimationFinished -= OnAnimationFinished;
		}
	}

	public void PlayIdle() => PlayKind(AnimKind.Idle, IdleClip, 0.2f, forceRestart: false);

	public void PlayChase() => PlayKind(AnimKind.Chase, ChaseClip, 0.18f, forceRestart: false);

	public void PlayTelegraph()
	{
		BindPlayer();
		if (AnimPlayer != null)
		{
			AnimPlayer.SpeedScale = 0.85f;
		}

		PlayKind(AnimKind.Telegraph, TelegraphClip, 0.1f, forceRestart: true);
	}

	public void PlayAttack()
	{
		BindPlayer();
		if (AnimPlayer != null)
		{
			AnimPlayer.SpeedScale = 1f;
		}

		PlayKind(AnimKind.Attack, AttackClip, 0.08f, forceRestart: true);
	}

	public void PlayStagger()
	{
		BindPlayer();
		if (AnimPlayer != null)
		{
			AnimPlayer.SpeedScale = 1f;
		}

		PlayKind(AnimKind.Stagger, StaggerClip, 0.05f, forceRestart: true);
	}

	public void PlayDeath()
	{
		BindPlayer();
		_deathPoseHeld = false;
		if (AnimPlayer != null)
		{
			AnimPlayer.SpeedScale = 1f;
		}

		PlayKind(AnimKind.Death, DeathClip, 0.05f, forceRestart: true);
	}

	/// <summary>
	/// Зафиксировать текущий кадр death (только пауза). Не прыгает в конец клипа.
	/// </summary>
	public void HoldDeathPose()
	{
		if (AnimPlayer == null || _deathPoseHeld || _kind != AnimKind.Death)
		{
			return;
		}

		AnimPlayer.Pause();
		_deathPoseHeld = true;
	}

	public void ResetSpeed()
	{
		if (AnimPlayer != null)
		{
			AnimPlayer.SpeedScale = 1f;
		}
	}

	public float GetCurrentLength()
	{
		if (AnimPlayer == null || string.IsNullOrEmpty(_current))
		{
			return 0f;
		}

		return (float)AnimPlayer.CurrentAnimationLength;
	}

	public float GetCurrentPosition()
	{
		if (AnimPlayer == null)
		{
			return 0f;
		}

		return (float)AnimPlayer.CurrentAnimationPosition;
	}

	public bool IsAttackHitWindow()
	{
		if (_kind != AnimKind.Attack)
		{
			return false;
		}

		float len = GetCurrentLength();
		if (len <= 0.01f)
		{
			return false;
		}

		float t = GetCurrentPosition() / len;
		return t >= AttackHitNormStart && t <= AttackHitNormEnd;
	}

	public bool IsAttackFinished()
	{
		if (_kind != AnimKind.Attack || AnimPlayer == null)
		{
			return true;
		}

		if (!AnimPlayer.IsPlaying())
		{
			return true;
		}

		float len = GetCurrentLength();
		return len > 0.01f && GetCurrentPosition() >= len - 0.02f;
	}

	private void OnAnimationFinished(StringName animName)
	{
		if (_kind != AnimKind.Death)
		{
			return;
		}

		// Клип доиграл — держим кадр (Pause), без Seek.
		HoldDeathPose();
	}

	private void PlayKind(AnimKind kind, string clip, float blend, bool forceRestart)
	{
		BindPlayer();
		if (AnimPlayer == null || string.IsNullOrEmpty(clip))
		{
			return;
		}

		string? resolved = ResolveClip(clip);
		if (resolved == null)
		{
			if (kind is AnimKind.Attack or AnimKind.Death or AnimKind.Telegraph)
			{
				GD.PushWarning($"EnemyAnimDriver: clip '{clip}' not found.");
			}

			return;
		}

		if (!forceRestart && _kind == kind && _current == resolved && AnimPlayer.IsPlaying())
		{
			return;
		}

		AnimPlayer.Play(resolved, customBlend: blend);
		_current = resolved;
		_kind = kind;
	}

	private void ConfigureLoop(string clip, bool loop)
	{
		string? resolved = ResolveClip(clip);
		if (resolved == null || AnimPlayer == null)
		{
			return;
		}

		Animation? anim = AnimPlayer.GetAnimation(resolved);
		if (anim == null)
		{
			return;
		}

		anim.LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
	}

	private string? ResolveClip(string clip)
	{
		if (AnimPlayer == null)
		{
			return null;
		}

		if (AnimPlayer.HasAnimation(clip))
		{
			return clip;
		}

		foreach (string name in AnimPlayer.GetAnimationList())
		{
			if (name == clip || name.EndsWith("/" + clip))
			{
				return name;
			}
		}

		return null;
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
