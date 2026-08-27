using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using UnityEngine;

namespace BridgeOfBlood.Data.Spells
{
	[System.Serializable]
	public struct ResolvedModifier
	{
		public float flat;
		public float percentIncreased;
		public float moreCombined;

		public float Multiplier => (1f + percentIncreased / 100f) * moreCombined;

		public static readonly ResolvedModifier Identity = new() { moreCombined = 1f };
	}

	public static class SpellModificationsApplicator
	{
		public static ResolvedModifier Resolve(SpellModifications mods, SpellModificationProperty prop, SpellAttributeMask mask)
		{
			if (!mods.modifiers.TryGetValue(prop, out var list))
				return ResolvedModifier.Identity;

			float flat = 0f, pct = 0f, more = 1f;
			foreach (var m in list)
			{
				if (m.filter != SpellAttributeMask.None && (mask & m.filter) == 0) continue;
				flat += m.GetFlat();
				pct += m.GetPercent();
				float mv = m.GetMore();
				if (mv != 0f) more *= (1f + mv / 100f);
			}
			return new ResolvedModifier { flat = flat, percentIncreased = pct, moreCombined = more };
		}
		//TODO i dont like the implication that we need to call multiple methods to resolve had a spell is modified.
		/// <summary>
		/// Applies Projectiles modifications to an emitter's base count. Shared by the emission handler (spawn)
		/// and spell preview renderers so both report the same number.
		/// </summary>
		public static int ResolveEmitCount(SpellModifications mods, int baseCount, SpellAttributeMask mask)
		{
			if (mods != null)
			{
				ResolvedModifier resolved = Resolve(mods, SpellModificationProperty.Projectiles, mask);
				baseCount = Mathf.Max(1, (int)(baseCount * resolved.Multiplier) + (int)resolved.flat);
			}
			return baseCount < 1 ? 1 : baseCount;
		}

		public static float ResolveManaCost(SpellModifications mods, float baseCost, SpellAttributeMask mask)
		{
			if (mods != null)
			{
				ResolvedModifier resolved = Resolve(mods, SpellModificationProperty.ManaCost, mask);
				baseCost = (baseCost + resolved.flat) * resolved.Multiplier;
			}
			return baseCost < 0f ? 0f : baseCost;
		}

		/// <summary>
		/// Writes running total mana cost at each loop index (prefix sums). Caller owns <paramref name="cumulativeOut"/>.
		/// </summary>
		public static void EvaluateLoopManaCosts(
			IReadOnlyList<RuntimeSpell> spells,
			SpellModificationCollection collection,
			List<float> cumulativeOut)
		{
			cumulativeOut.Clear();
			if (spells == null)
				return;

			float running = 0f;
			for (int i = 0; i < spells.Count; i++)
			{
				RuntimeSpell spell = spells[i];
				SpellAuthoringData def = spell.Definition;
				float baseCost = def != null ? def.manaCost : 0f;
				SpellAttributeMask mask = def != null ? def.attributeMask : SpellAttributeMask.None;
				SpellModifications mods = collection != null ? collection.ResolveFor(spell.spellId) : null;
				running += ResolveManaCost(mods, baseCost, mask);
				cumulativeOut.Add(running);
			}
		}
	}
}
