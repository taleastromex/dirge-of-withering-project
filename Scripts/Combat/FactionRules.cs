using Godot;

namespace DirgeOfWithering;

/// <summary>
/// Targeting / harm rules between Player and NPC categories.
/// </summary>
public static class FactionRules
{
	public static bool TryGetCategory(Node? node, out NpcCategory category)
	{
		switch (node)
		{
			case Player player:
				category = player.Category;
				return true;
			case BasicEnemy enemy:
				category = enemy.Category;
				return true;
			default:
				category = default;
				return false;
		}
	}

	public static bool CanTarget(Node attacker, Node victim)
	{
		if (!GodotObject.IsInstanceValid(attacker)
			|| !GodotObject.IsInstanceValid(victim)
			|| attacker == victim)
		{
			return false;
		}

		if (attacker is Player)
		{
			return victim is BasicEnemy || victim.IsInGroup("enemies");
		}

		if (!TryGetCategory(attacker, out NpcCategory atkCat))
		{
			return false;
		}

		bool victimIsPlayer = victim is Player;

		switch (atkCat)
		{
			case NpcCategory.Distorted:
				// Humans (including Player) and Undead — not other Distorted.
				if (victimIsPlayer)
				{
					return true;
				}

				return TryGetCategory(victim, out NpcCategory distVictim)
					&& distVictim is NpcCategory.Human or NpcCategory.Undead;

			case NpcCategory.Human:
				// Player OR Distorted — not other Humans.
				if (victimIsPlayer)
				{
					return true;
				}

				return TryGetCategory(victim, out NpcCategory humanVictim)
					&& humanVictim == NpcCategory.Distorted;

			case NpcCategory.Undead:
				// Player + Human + Distorted — not other Undead.
				if (victimIsPlayer)
				{
					return true;
				}

				if (!TryGetCategory(victim, out NpcCategory undeadVictim))
				{
					return false;
				}

				return undeadVictim is NpcCategory.Human or NpcCategory.Distorted;

			default:
				return false;
		}
	}

	public static bool CanHarm(Node attacker, Node victim) => CanTarget(attacker, victim);
}
