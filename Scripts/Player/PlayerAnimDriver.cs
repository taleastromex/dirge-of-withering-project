using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Локомоция / атака / смерть для Mixamo-модели игрока.
/// </summary>
public partial class PlayerAnimDriver : Node
{
	public enum AnimKind
	{
		None,
		Idle,
		Walk,
		Run,
		Attack,
		Hurt,
		Death
	}

	[Export]
	public AnimationPlayer? AnimPlayer { get; set; }

	[Export] public string IdleClip { get; set; } = "idle";
	[Export] public string WalkClip { get; set; } = "walk";
	[Export] public string RunClip { get; set; } = "run";
	[Export] public string AttackClip { get; set; } = "attack";
	[Export] public string HeavyAttackClip { get; set; } = "attack_heavy";
	[Export] public string HurtClip { get; set; } = "stagger";
	[Export] public string DeathClip { get; set; } = "death";

	/// <summary>Ускорение hurt-клипа (stagger Mixamo обычно длинный).</summary>
	[Export(PropertyHint.Range, "1,3,0.05")]
	public float HurtSpeedScale { get; set; } = 2.1f;

	/// <summary>Скорость (горизонт.), выше которой играем run вместо walk.</summary>
	[Export]
	public float RunSpeedThreshold { get; set; } = 2.2f;

	/// <summary>Горизонтальная скорость, при которой walk играет в native tempo.</summary>
	[Export]
	public float WalkReferenceSpeed { get; set; } = 1.35f;

	/// <summary>Горизонтальная скорость, при которой run играет в native tempo.</summary>
	[Export]
	public float RunReferenceSpeed { get; set; } = 4.0f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float AttackHitNormStart { get; set; } = 0.38f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float AttackHitNormEnd { get; set; } = 0.58f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float HeavyAttackHitNormStart { get; set; } = 0.58f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float HeavyAttackHitNormEnd { get; set; } = 0.78f;

	/// <summary>Stop clips and hold bind / T-pose (weapon grip tuning).</summary>
	[Export]
	public bool HoldRestPose { get; set; }

	private AnimKind _kind = AnimKind.None;
	private string _current = "";
	private bool _bound;
	private bool _deathPoseHeld;
	private bool _heavyAttack;

	public AnimKind CurrentKind => _kind;

	public bool IsPlayingAttack => _kind == AnimKind.Attack;

	public bool IsHurt => _kind == AnimKind.Hurt;

	public bool IsHeavyAttack => _heavyAttack;

	public override void _Ready()
	{
		CallDeferred(MethodName.BindPlayer);
	}

	public void GetAttackHitWindow(out float start, out float end)
	{
		start = _heavyAttack ? HeavyAttackHitNormStart : AttackHitNormStart;
		end = _heavyAttack ? HeavyAttackHitNormEnd : AttackHitNormEnd;
	}

	/// <summary>
	/// 0 = idle grip, 1 = fully extended strike grip.
	/// Ramps in before the hitbox window and out after it.
	/// </summary>
	public float GetWeaponStrikeWeight(float blendInNorm = 0.1f, float blendOutNorm = 0.14f)
	{
		if (_kind != AnimKind.Attack)
		{
			return 0f;
		}

		float len = GetCurrentLength();
		if (len <= 0.01f)
		{
			return 0f;
		}

		float t = GetCurrentPosition() / len;
		GetAttackHitWindow(out float hitStart, out float hitEnd);
		float rampIn = Mathf.Max(0f, hitStart - Mathf.Max(0.01f, blendInNorm));
		float rampOut = Mathf.Min(1f, hitEnd + Mathf.Max(0.01f, blendOutNorm));

		if (t <= rampIn || t >= rampOut)
		{
			return 0f;
		}

		if (t < hitStart)
		{
			return SmoothStep(rampIn, hitStart, t);
		}

		if (t <= hitEnd)
		{
			return 1f;
		}

		return 1f - SmoothStep(hitEnd, rampOut, t);
	}

	private static float SmoothStep(float edge0, float edge1, float x)
	{
		float t = Mathf.Clamp((x - edge0) / Mathf.Max(0.0001f, edge1 - edge0), 0f, 1f);
		return t * t * (3f - 2f * t);
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
		ConfigureLoop(WalkClip, loop: true);
		ConfigureLoop(RunClip, loop: true);
		ConfigureLoop(AttackClip, loop: false);
		ConfigureLoop(HeavyAttackClip, loop: false);
		ConfigureLoop(HurtClip, loop: false);
		ConfigureLoop(DeathClip, loop: false);
		AnimPlayer.SpeedScale = 1f;
		AnimPlayer.AnimationFinished += OnAnimationFinished;
		_bound = true;
		PlayIdle();
	}

	public override void _ExitTree()
	{
		if (AnimPlayer != null && _bound)
		{
			AnimPlayer.AnimationFinished -= OnAnimationFinished;
		}
	}

	public void UpdateLocomotion(float horizontalSpeed)
	{
		if (HoldRestPose)
		{
			ForceRestPose();
			return;
		}

		if (_kind is AnimKind.Attack or AnimKind.Hurt or AnimKind.Death)
		{
			return;
		}

		if (horizontalSpeed < 0.12f)
		{
			if (AnimPlayer != null)
			{
				AnimPlayer.SpeedScale = 1f;
			}

			PlayIdle();
			return;
		}

		if (horizontalSpeed >= RunSpeedThreshold)
		{
			PlayRun();
			SetLocomotionScale(horizontalSpeed / Mathf.Max(0.1f, RunReferenceSpeed));
		}
		else
		{
			PlayWalk();
			SetLocomotionScale(horizontalSpeed / Mathf.Max(0.1f, WalkReferenceSpeed));
		}
	}

	public void PlayIdle() => PlayKind(AnimKind.Idle, IdleClip, 0.2f, forceRestart: false);

	public void PlayWalk() => PlayKind(AnimKind.Walk, WalkClip, 0.15f, forceRestart: false);

	public void PlayRun() => PlayKind(AnimKind.Run, RunClip, 0.15f, forceRestart: false);

	/// <summary>Stop clips and reset skeleton to bind / T-pose (weapon grip tuning).</summary>
	public void ForceRestPose()
	{
		BindPlayer();
		if (AnimPlayer != null)
		{
			AnimPlayer.Stop();
			AnimPlayer.SpeedScale = 1f;
		}

		_kind = AnimKind.None;
		_current = "";
		_deathPoseHeld = false;
		_heavyAttack = false;

		Skeleton3D? skeleton = FindSkeleton(GetParent() ?? this);
		if (skeleton == null)
		{
			return;
		}

		skeleton.ResetBonePoses();
	}

	public void PlayAttack(bool heavy)
	{
		if (HoldRestPose)
		{
			return;
		}

		if (_kind == AnimKind.Death || _kind == AnimKind.Hurt)
		{
			return;
		}

		BindPlayer();
		_heavyAttack = heavy;
		if (AnimPlayer != null)
		{
			AnimPlayer.SpeedScale = 1f;
		}

		string clip = heavy ? HeavyAttackClip : AttackClip;
		if (ResolveClip(clip) == null)
		{
			clip = AttackClip;
			_heavyAttack = false;
		}

		PlayKind(AnimKind.Attack, clip, 0.05f, forceRestart: true);
	}

	/// <summary>Hit react: interrupts attack/locomotion until clip ends.</summary>
	public void PlayHurt()
	{
		if (_kind == AnimKind.Death)
		{
			return;
		}

		BindPlayer();
		_heavyAttack = false;
		if (AnimPlayer != null)
		{
			AnimPlayer.SpeedScale = HurtSpeedScale;
		}

		PlayKind(AnimKind.Hurt, HurtClip, 0.04f, forceRestart: true);
	}

	public void PlayDeath()
	{
		BindPlayer();
		_deathPoseHeld = false;
		_heavyAttack = false;
		if (AnimPlayer != null)
		{
			AnimPlayer.SpeedScale = 1f;
		}

		PlayKind(AnimKind.Death, DeathClip, 0.05f, forceRestart: true);
	}

	public void NotifyAttackFinished()
	{
		if (_kind == AnimKind.Attack)
		{
			_kind = AnimKind.None;
			_current = "";
			_heavyAttack = false;
			if (AnimPlayer != null)
			{
				AnimPlayer.SpeedScale = 1f;
			}
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
		float start = _heavyAttack ? HeavyAttackHitNormStart : AttackHitNormStart;
		float end = _heavyAttack ? HeavyAttackHitNormEnd : AttackHitNormEnd;
		return t >= start && t <= end;
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

	private void SetLocomotionScale(float scale)
	{
		if (AnimPlayer == null)
		{
			return;
		}

		AnimPlayer.SpeedScale = Mathf.Clamp(scale, 0.75f, 1.35f);
	}

	private void OnAnimationFinished(StringName animName)
	{
		if (_kind == AnimKind.Attack || _kind == AnimKind.Hurt)
		{
			_kind = AnimKind.None;
			_current = "";
			_heavyAttack = false;
			return;
		}

		if (_kind == AnimKind.Death)
		{
			HoldDeathPose();
		}
	}

	private void HoldDeathPose()
	{
		if (AnimPlayer == null || _deathPoseHeld || _kind != AnimKind.Death)
		{
			return;
		}

		AnimPlayer.Pause();
		_deathPoseHeld = true;
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
			if (kind is AnimKind.Attack or AnimKind.Death)
			{
				GD.PushWarning($"PlayerAnimDriver: clip '{clip}' not found.");
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

	private static Skeleton3D? FindSkeleton(Node root)
	{
		if (root is Skeleton3D sk)
		{
			return sk;
		}

		foreach (Node child in root.GetChildren())
		{
			Skeleton3D? found = FindSkeleton(child);
			if (found != null)
			{
				return found;
			}
		}

		return null;
	}
}
