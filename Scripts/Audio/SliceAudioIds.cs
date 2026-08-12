using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Central SFX path table for the Flooded Cathedral vertical slice.
/// </summary>
public static class SliceAudioIds
{
	public const string Ambient =
		"res://Assets/Audio/HauntedLullabiesofJapan_Kazoe Uta (Tension).mp3";

	public const string PlayerHurt = "res://Assets/Audio/SFX/CombatAndGore/slap.wav";
	public const string EnemyDeath = "res://Assets/Audio/SFX/CombatAndGore/squelching_1.wav";
	public const string PlayerDeath = "res://Assets/Audio/SFX/Death/death-sound.mp3";
	public const string FilthOverload = "res://Assets/Audio/SFX/Filth/filth-overload.mp3";
	public const string Altar = "res://Assets/Audio/SFX/altar.mp3";

	public static readonly string[] SwingWhooshes =
	{
		"res://Assets/Audio/SFX/Weapons/Whooshes/WHSH_Whoosh_HoveAud_SwordCombat_07.wav",
	};

	public static readonly string[] SwordHits =
	{
		"res://Assets/Audio/SFX/Weapons/wGore/GOREStab_SwordStabGore_HoveAud_SwordCombat_01.wav",
		"res://Assets/Audio/SFX/Weapons/wGore/GOREStab_SwordStabGore_HoveAud_SwordCombat_11.wav",
		"res://Assets/Audio/SFX/Weapons/wGore/GOREStab_SwordStabGore_HoveAud_SwordCombat_17.wav",
	};

	public static readonly string[] HeavyAttackVoices =
	{
		"res://Assets/Audio/SFX/Voiceline/Actions/VOXEfrt_ActionGrunt_HoveAud_SwordCombat_23.wav",
		"res://Assets/Audio/SFX/Voiceline/Actions/VOXEfrt_ActionGrunt_HoveAud_SwordCombat_29.wav",
		"res://Assets/Audio/SFX/Voiceline/Actions/VOXEfrt_ActionGrunt_HoveAud_SwordCombat_54.wav",
	};

	public static readonly string[] PlayerDeathVoices =
	{
		"res://Assets/Audio/SFX/Voiceline/Death/VOXScrm_DamageGrunt_HoveAudio_SwordCombat_13.wav",
	};

	public static readonly string[] FootstepsConcrete =
	{
		"res://Assets/Audio/SFX/Footsteps/foley_footstep_concrete_1.wav",
		"res://Assets/Audio/SFX/Footsteps/foley_footstep_concrete_2.wav",
		"res://Assets/Audio/SFX/Footsteps/foley_footstep_concrete_3.wav",
	};

	/// <summary>Enemy telegraph / approach growls (pitched down effort grunts).</summary>
	public static readonly string[] EnemyTelegraphGrowls = HeavyAttackVoices;

	public static string Pick(string[] paths)
	{
		if (paths.Length == 0)
		{
			return string.Empty;
		}

		return paths[(int)(GD.Randi() % (uint)paths.Length)];
	}
}
