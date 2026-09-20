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

		public FloatRange ResolveRange(FloatRange baseRange)
		{
			float mult = Multiplier;
			var range = new FloatRange
			{
				min = (baseRange.min + flat) * mult,
				max = (baseRange.max + flat) * mult
			};
			range.ClampOrder();
			return range;
		}

		public float TotalIncrease()
		{
			return Multiplier;
		}
	}

	public static class SpellModificationsApplicator
	{
		/// <summary>
		/// Applies Projectiles modifications to an emitter's base count. Shared by the emission handler (spawn)
		/// and spell preview renderers so both report the same number.
		/// </summary>
		public static int ResolveEmitCount(ModificationSet mods, int baseCount, SpellAttributeMask mask)
		{
			if (mods != null)
			{
				ResolvedModifier resolved = ParameterModificationMerger.Merge(
					mods.Get<ProjectileModificationData>(),
					modification => modification.parameters,
					modification => modification.filter,
					mask);
				baseCount = Mathf.Max(1, (int)(baseCount * resolved.Multiplier) + (int)resolved.flat);
			}
			return baseCount < 1 ? 1 : baseCount;
		}

		/// <summary>
		/// Resolves how many times one loop-slot press invokes its spell. The unmodified count is always one.
		/// </summary>
		public static int ResolveMulticastCount(ModificationSet mods, SpellAttributeMask mask)
		{
			const int baseCount = 1;
			if (mods == null)
				return baseCount;

			ResolvedModifier resolved = ParameterModificationMerger.Merge(
				mods.Get<MulticastModificationData>(),
				modification => modification.parameters,
				modification => modification.filter,
				mask);
			return Mathf.Max(1, (int)(baseCount * resolved.Multiplier) + (int)resolved.flat);
		}

		public static float ResolveManaCost(ModificationSet mods, float baseCost, SpellAttributeMask mask)
		{
			if (mods != null)
			{
				ResolvedModifier resolved = ParameterModificationMerger.Merge(
					mods.Get<ManaCostModificationData>(),
					modification => modification.parameters,
					modification => modification.filter,
					mask);
				baseCost = (baseCost + resolved.flat) * resolved.Multiplier;
			}
			return baseCost < 0f ? 0f : baseCost;
		}

		public static float ResolveCastCompletionDuration(
			ModificationSet mods,
			float baseDuration,
			SpellAttributeMask mask)
		{
			if (mods == null)
				return Mathf.Max(0f, baseDuration);

			ResolvedModifier resolved = ParameterModificationMerger.Merge(
				mods.Get<CastSpeedModificationData>(),
				modification => modification.parameters,
				modification => modification.filter,
				mask);
			return Mathf.Max(0f, (baseDuration - resolved.flat) / resolved.Multiplier);
		}

		/// <summary>
		/// Writes running total mana cost at each loop index (prefix sums). Caller owns <paramref name="cumulativeOut"/>.
		/// </summary>
		public static void EvaluateLoopManaCosts(
			IReadOnlyList<RuntimeSpell> spells,
			ModificationCatalog catalog,
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
				ModificationSet mods = catalog != null ? catalog.ResolveFor(spell.spellId) : null;
				running += ResolveManaCost(mods, baseCost, mask);
				cumulativeOut.Add(running);
			}
		}
	}
}
