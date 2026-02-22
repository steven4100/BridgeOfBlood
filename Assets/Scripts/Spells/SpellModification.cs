using BridgeOfBlood.Data.Shared;
using Unity.Collections;

namespace BridgeOfBlood.Data.Spells
{
	/// <summary>
	/// Resolves spell modifications through the modifier pipeline.
	/// Applies modifiers in order: Flat → Additive → Conditional → Global → Cross-Spell
	/// Ensures multiplier order is explicit and predictable.
	/// </summary>
	public static class SpellModificationResolver
	{
		/// <summary>
		/// Modifies a spell by applying all relevant modifiers.
		/// Deterministic and side-effect free.
		/// 
		/// Applies modifiers in order: Flat → Multiplicative
		/// Predicate resolution happens upstream - if a modifier exists here, it should be applied.
		/// </summary>
		public static Spell ModifySpell(Spell spell, NativeList<SpellModifier> modifiers)
		{
			var result = spell;

			// Apply modifiers in order
			for (int i = 0; i < modifiers.Length; i++)
			{
				var modifier = modifiers[i];

				// Check if modifier applies to this spell (by attribute mask)
				if ((modifier.targetAttributeMask & spell.attributeMask) == 0 && modifier.targetAttributeMask != 0)
					continue;

				// Step 1: Apply flat damage bonuses (additive)
				if (modifier.flatDamageBonus != 0)
				{
					for (int j = 0; j < result.damages.Length; j++)
					{
						var damage = result.damages[j];
						damage.baseDamage += modifier.flatDamageBonus;
						result.damages[j] = damage;
					}
				}

				// Step 2: Apply damage multipliers (multiplicative)
				if (modifier.damageMultiplier != 1f)
				{
					for (int j = 0; j < result.damages.Length; j++)
					{
						var damage = result.damages[j];
						damage.baseDamage = (int)(damage.baseDamage * modifier.damageMultiplier);
						result.damages[j] = damage;
					}
				}

				// Apply cast time multiplier
				if (modifier.castTimeMultiplier != 1f)
				{
					result.castTime *= modifier.castTimeMultiplier;
				}

				// Note: Spell attributes are immutable at runtime - they define the spell's identity
			}

			return result;
		}
	}
}

