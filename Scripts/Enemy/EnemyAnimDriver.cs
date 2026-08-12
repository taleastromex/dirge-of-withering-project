using System.Collections.Generic;
using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Мост AI → AnimationPlayer. Локомоция лупится, бой нативным темпом, death держит позу.
/// Атака может чередовать Attack / AttackAlt / AttackHeavy, если клипы заданы.
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

	public enum AttackVariant
	{
		Normal,
		Alt,
		Heavy
	}

	[Export]
	public AnimationPlayer? AnimPlayer { get; set; }

	[Export] public string IdleClip { get; set; } = "idle";
	[Export] public string ChaseClip { get; set; } = "run";
	[Export] public string TelegraphClip { get; set; } = "stagger";
	[Export] public string AttackClip { get; set; } = "attack";
	[Export] public string AttackAltClip { get; set; } = "";
	[Export] public string AttackHeavyClip { get; set; } = "";
	[Export] public string StaggerClip { get; set; } = "stagger";
	[Export] public string DeathClip { get; set; } = "death";
	/// <summary>Доп. обычная смерть (рандом с Death / DeathDying).</summary>
	[Export] public string DeathAltClip { get; set; } = "";
	/// <summary>Ещё одна обычная смерть (напр. Mixamo Dying), не затирает оригинальный death.</summary>
	[Export] public string DeathDyingClip { get; set; } = "";
	/// <summary>Экспрессивная смерть (flyback) — только по флагу explosive.</summary>
	[Export] public string DeathExplosiveClip { get; set; } = "";

	/// <summary>Ускорение death — меньше «стоят перед падением».</summary>
	[Export(PropertyHint.Range, "1,3,0.05")]
	public float DeathSpeedScale { get; set; } = 1.85f;

	/// <summary>Темп attack / attack_alt / attack_heavy (Mixamo часто тянет).</summary>
	[Export(PropertyHint.Range, "0.5,2.5,0.05")]
	public float AttackSpeedScale { get; set; } = 1f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float AttackHitNormStart { get; set; } = 0.48f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float AttackHitNormEnd { get; set; } = 0.62f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float AttackAltHitNormStart { get; set; } = 0.40f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float AttackAltHitNormEnd { get; set; } = 0.58f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float AttackHeavyHitNormStart { get; set; } = 0.52f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float AttackHeavyHitNormEnd { get; set; } = 0.62f;

	private AnimKind _kind = AnimKind.None;
	private string _current = "";
	private bool _bound;
	private bool _deathPoseHeld;
	private AttackVariant _attackVariant = AttackVariant.Normal;
	private float _hitStart = 0.48f;
	private float _hitEnd = 0.62f;

	public AnimKind CurrentKind => _kind;

	public AttackVariant CurrentAttackVariant => _attackVariant;

	public bool IsDeathPoseHeld => _deathPoseHeld;

	/// <summary>Death-клип доиграл или уже зафиксирован на последнем кадре.</summary>
	public bool IsDeathReadyToSettle()
	{
		if (_kind != AnimKind.Death)
		{
			return true;
		}

		if (_deathPoseHeld)
		{
			return true;
		}

		return IsDeathFinished();
	}

	public bool IsDeathFinished()
	{
		if (_kind != AnimKind.Death || AnimPlayer == null)
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
		ConfigureLoop(AttackAltClip, loop: false);
		ConfigureLoop(AttackHeavyClip, loop: false);
		ConfigureLoop(StaggerClip, loop: false);
		ConfigureLoop(DeathClip, loop: false);
		ConfigureLoop(DeathAltClip, loop: false);
		ConfigureLoop(DeathDyingClip, loop: false);
		ConfigureLoop(DeathExplosiveClip, loop: false);
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

	public bool HasHeavyAttackClip()
	{
		return !string.IsNullOrWhiteSpace(AttackHeavyClip) && ResolveClip(AttackHeavyClip) != null;
	}

	/// <param name="allowHeavy">false — heavy выкидываем из пула (слишком близко/далеко).</param>
	/// <param name="preferHeavy">true — с шансом preferChance берём heavy, если он в пуле.</param>
	/// <param name="requireHeavy">true — только heavy; иначе не стартуем атаку-клип.</param>
	public void PlayAttack(
		bool allowHeavy = true,
		bool preferHeavy = false,
		float preferChance = 0.7f,
		bool requireHeavy = false)
	{
		BindPlayer();
		if (AnimPlayer != null)
		{
			AnimPlayer.SpeedScale = AttackSpeedScale;
		}

		PickAttackVariant(
			allowHeavy,
			preferHeavy,
			preferChance,
			requireHeavy,
			out string clip,
			out AttackVariant variant,
			out float hitStart,
			out float hitEnd);
		_attackVariant = variant;
		_hitStart = hitStart;
		_hitEnd = hitEnd;
		PlayKind(AnimKind.Attack, clip, 0.08f, forceRestart: true);
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

	/// <param name="explosive">
	/// true — Flying Back / DeathExplosiveClip, если есть; иначе обычный рандом death/death_alt.
	/// </param>
	public void PlayDeath(bool explosive = false)
	{
		BindPlayer();
		_deathPoseHeld = false;
		if (AnimPlayer != null)
		{
			AnimPlayer.SpeedScale = DeathSpeedScale;
		}

		string clip = PickDeathClip(explosive);
		PlayKind(AnimKind.Death, clip, 0.04f, forceRestart: true);
	}

	private string PickDeathClip(bool explosive)
	{
		if (explosive)
		{
			string? explosiveResolved = ResolveClip(DeathExplosiveClip);
			if (explosiveResolved != null)
			{
				return DeathExplosiveClip;
			}
		}

		var options = new List<string>(3);
		TryAddDeathOption(options, DeathClip);
		TryAddDeathOption(options, DeathAltClip);
		TryAddDeathOption(options, DeathDyingClip);

		if (options.Count == 0)
		{
			return DeathClip;
		}

		int i = (int)(GD.Randi() % (uint)options.Count);
		return options[i];
	}

	private void TryAddDeathOption(List<string> options, string clip)
	{
		if (string.IsNullOrWhiteSpace(clip))
		{
			return;
		}

		if (ResolveClip(clip) == null)
		{
			return;
		}

		options.Add(clip);
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

		Skeleton3D? skeleton = FindSkeleton(GetParent() ?? this);
		if (skeleton == null)
		{
			return;
		}

		skeleton.ResetBonePoses();
	}

	public float GetCurrentLength()
	{
		if (AnimPlayer == null || string.IsNullOrEmpty(_current))
		{
			return 0f;
		}

		return (float)AnimPlayer.CurrentAnimationLength;
	}

	/// <summary>Длительность текущего клипа в секундах с учётом SpeedScale.</summary>
	public float GetCurrentPlaybackSeconds()
	{
		float len = GetCurrentLength();
		if (len <= 0.01f || AnimPlayer == null)
		{
			return 0f;
		}

		float speed = Mathf.Abs(AnimPlayer.SpeedScale);
		return speed > 0.01f ? len / speed : len;
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
		return t >= _hitStart && t <= _hitEnd;
	}

	/// <summary>
	/// 0 = idle grip, 1 = strike grip. Ramps around the active hit window.
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
		float rampIn = Mathf.Max(0f, _hitStart - Mathf.Max(0.01f, blendInNorm));
		float rampOut = Mathf.Min(1f, _hitEnd + Mathf.Max(0.01f, blendOutNorm));

		if (t <= rampIn || t >= rampOut)
		{
			return 0f;
		}

		if (t < _hitStart)
		{
			float u = Mathf.Clamp((t - rampIn) / Mathf.Max(0.0001f, _hitStart - rampIn), 0f, 1f);
			return u * u * (3f - 2f * u);
		}

		if (t <= _hitEnd)
		{
			return 1f;
		}

		float v = Mathf.Clamp((t - _hitEnd) / Mathf.Max(0.0001f, rampOut - _hitEnd), 0f, 1f);
		return 1f - (v * v * (3f - 2f * v));
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

	private void PickAttackVariant(
		bool allowHeavy,
		bool preferHeavy,
		float preferChance,
		bool requireHeavy,
		out string clip,
		out AttackVariant variant,
		out float hitStart,
		out float hitEnd)
	{
		var options = new List<(string Clip, AttackVariant Variant, float Start, float End)>(3);
		TryAddAttackOption(options, AttackClip, AttackVariant.Normal, AttackHitNormStart, AttackHitNormEnd);
		TryAddAttackOption(options, AttackAltClip, AttackVariant.Alt, AttackAltHitNormStart, AttackAltHitNormEnd);
		if (allowHeavy || requireHeavy)
		{
			TryAddAttackOption(
				options,
				AttackHeavyClip,
				AttackVariant.Heavy,
				AttackHeavyHitNormStart,
				AttackHeavyHitNormEnd);
		}

		if (requireHeavy)
		{
			for (int h = 0; h < options.Count; h++)
			{
				if (options[h].Variant == AttackVariant.Heavy)
				{
					(clip, variant, hitStart, hitEnd) = options[h];
					return;
				}
			}

			// Нет heavy — оставляем Normal как маркер неудачи (caller откатит в chase).
			clip = AttackClip;
			variant = AttackVariant.Normal;
			hitStart = AttackHitNormStart;
			hitEnd = AttackHitNormEnd;
			return;
		}

		if (options.Count == 0)
		{
			clip = AttackClip;
			variant = AttackVariant.Normal;
			hitStart = AttackHitNormStart;
			hitEnd = AttackHitNormEnd;
			return;
		}

		if (preferHeavy && preferChance > 0f)
		{
			int heavyIdx = -1;
			for (int h = 0; h < options.Count; h++)
			{
				if (options[h].Variant == AttackVariant.Heavy)
				{
					heavyIdx = h;
					break;
				}
			}

			if (heavyIdx >= 0 && GD.Randf() <= preferChance)
			{
				(clip, variant, hitStart, hitEnd) = options[heavyIdx];
				return;
			}
		}

		int i = (int)(GD.Randi() % (uint)options.Count);
		(clip, variant, hitStart, hitEnd) = options[i];
	}

	private void TryAddAttackOption(
		List<(string Clip, AttackVariant Variant, float Start, float End)> options,
		string clip,
		AttackVariant variant,
		float hitStart,
		float hitEnd)
	{
		if (string.IsNullOrWhiteSpace(clip))
		{
			return;
		}

		if (ResolveClip(clip) == null)
		{
			return;
		}

		options.Add((clip, variant, hitStart, hitEnd));
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
		if (string.IsNullOrWhiteSpace(clip))
		{
			return;
		}

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
