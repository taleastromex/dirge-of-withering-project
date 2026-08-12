using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Thin audio facade for the Vertical Slice: ambient loop + one-shot SFX.
/// Autoload: GameAudio.
/// </summary>
public partial class GameAudio : Node
{
	public static GameAudio? Instance { get; private set; }

	[Export] public float MusicVolumeDb { get; set; } = -6f;
	[Export] public float SfxVolumeDb { get; set; } = -2f;

	private AudioStreamPlayer? _music;
	private AudioStreamPlayer? _sfx;
	private readonly System.Collections.Generic.Dictionary<string, AudioStream> _cache = new();

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		_music = new AudioStreamPlayer
		{
			Name = "MusicPlayer",
			Bus = "Music",
			VolumeDb = MusicVolumeDb,
			ProcessMode = ProcessModeEnum.Always,
		};
		_sfx = new AudioStreamPlayer
		{
			Name = "SfxPlayer",
			Bus = "SFX",
			VolumeDb = SfxVolumeDb,
			MaxPolyphony = 12,
			ProcessMode = ProcessModeEnum.Always,
		};
		AddChild(_music);
		AddChild(_sfx);
	}

	public void PlayAmbient(string path, bool loop = true)
	{
		AudioStream? stream = LoadStream(path);
		if (_music == null || stream == null)
		{
			return;
		}

		if (stream is AudioStreamMP3 mp3)
		{
			mp3.Loop = loop;
		}
		else if (stream is AudioStreamWav wav)
		{
			wav.LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled;
		}
		else if (stream is AudioStreamOggVorbis ogg)
		{
			ogg.Loop = loop;
		}

		_music.Stream = stream;
		_music.VolumeDb = MusicVolumeDb;
		_music.Play();
	}

	public void StopAmbient()
	{
		_music?.Stop();
	}

	/// <summary>Stops one-shot SFX players (e.g. death sting when Restart is pressed).</summary>
	public void StopAllSfx()
	{
		_sfx?.Stop();

		foreach (Node child in GetChildren())
		{
			if (child is AudioStreamPlayer player
				&& player != _music
				&& player != _sfx)
			{
				player.Stop();
				player.QueueFree();
			}
		}
	}

	public void PlaySfx(string path, float volumeDbOffset = 0f, float pitchScale = 1f)
	{
		AudioStream? stream = LoadStream(path);
		if (_sfx == null || stream == null)
		{
			return;
		}

		_sfx.PitchScale = Mathf.Clamp(pitchScale, 0.7f, 1.4f);
		_sfx.VolumeDb = SfxVolumeDb + volumeDbOffset;
		_sfx.Stream = stream;
		_sfx.Play();
	}

	/// <summary>Fire-and-forget one-shot that won't cut off other SFX mid-clip.</summary>
	public void PlaySfxOneShot(string path, float volumeDbOffset = 0f, float pitchScale = 1f)
	{
		AudioStream? stream = LoadStream(path);
		if (stream == null)
		{
			return;
		}

		var player = new AudioStreamPlayer
		{
			Bus = "SFX",
			Stream = stream,
			VolumeDb = SfxVolumeDb + volumeDbOffset,
			PitchScale = Mathf.Clamp(pitchScale, 0.7f, 1.4f),
			ProcessMode = ProcessModeEnum.Always,
		};
		AddChild(player);
		player.Finished += player.QueueFree;
		player.Play();
	}

	/// <summary>
	/// Spatial one-shot attached under <paramref name="host"/> (enemy footsteps / growls / death).
	/// </summary>
	public void PlaySfx3D(
		Node3D host,
		string path,
		float volumeDbOffset = 0f,
		float pitchScale = 1f,
		float maxDistance = 22f,
		float unitSize = 4f)
	{
		if (host == null || !GodotObject.IsInstanceValid(host))
		{
			return;
		}

		AudioStream? stream = LoadStream(path);
		if (stream == null)
		{
			return;
		}

		var player = new AudioStreamPlayer3D
		{
			Bus = "SFX",
			Stream = stream,
			VolumeDb = SfxVolumeDb + volumeDbOffset,
			PitchScale = Mathf.Clamp(pitchScale, 0.55f, 1.4f),
			MaxDistance = maxDistance,
			UnitSize = unitSize,
			AttenuationFilterCutoffHz = 5000f,
			MaxPolyphony = 4,
		};
		host.AddChild(player);
		player.Finished += player.QueueFree;
		player.Play();
	}

	public AudioStream? LoadStream(string path)
	{
		if (_cache.TryGetValue(path, out AudioStream? cached) && GodotObject.IsInstanceValid(cached))
		{
			return cached;
		}

		AudioStream? stream = GD.Load<AudioStream>(path);
		if (stream != null)
		{
			_cache[path] = stream;
		}

		return stream;
	}
}
