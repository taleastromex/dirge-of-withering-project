using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Короткий hit-stop через Engine.TimeScale (ignore time scale на таймере).
/// </summary>
public static class CombatHitStop
{
	private static bool _active;

	public static void Pulse(SceneTree tree, float realSeconds, float timeScale = 0.12f)
	{
		if (_active || tree == null || realSeconds <= 0f)
		{
			return;
		}

		_active = true;
		float previous = (float)Engine.TimeScale;
		Engine.TimeScale = timeScale;

		tree.CreateTimer(realSeconds, processAlways: true, ignoreTimeScale: true).Timeout += () =>
		{
			Engine.TimeScale = previous;
			_active = false;
		};
	}
}
