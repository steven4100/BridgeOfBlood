using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using UnityEngine;

namespace BridgeOfBlood.Data.Spells
{
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

		public static AttackEntityData CloneAndApply(AttackEntityData source, SpellAttributeMask spellAttributeMask, SpellModifications mods)
		{
			if (source == null) return null;
			if (mods == null) return source;

			var clone = Object.Instantiate(source);

			var dmgScaling = Resolve(mods, SpellModificationProperty.DamageScaling, spellAttributeMask);
			var typePhys = Resolve(mods, SpellModificationProperty.PhysicalDamageScaling, spellAttributeMask);
			var typeCold = Resolve(mods, SpellModificationProperty.ColdDamageScaling, spellAttributeMask);
			var typeFire = Resolve(mods, SpellModificationProperty.FireDamageScaling, spellAttributeMask);
			var typeLightning = Resolve(mods, SpellModificationProperty.LightningDamageScaling, spellAttributeMask);

			clone.physicalDamageRange = ApplyDamageRange(source.physicalDamageRange, typePhys, dmgScaling);
			clone.coldDamageRange = ApplyDamageRange(source.coldDamageRange, typeCold, dmgScaling);
			clone.fireDamageRange = ApplyDamageRange(source.fireDamageRange, typeFire, dmgScaling);
			clone.lightningDamageRange = ApplyDamageRange(source.lightningDamageRange, typeLightning, dmgScaling);

			var critChance = Resolve(mods, SpellModificationProperty.CritChance, spellAttributeMask);
			clone.critChanceRange = new FloatRange
			{
				min = Mathf.Clamp01(source.critChanceRange.min * critChance.Multiplier + critChance.flat / 100f),
				max = Mathf.Clamp01(source.critChanceRange.max * critChance.Multiplier + critChance.flat / 100f)
			};
			clone.critChanceRange.ClampOrder();

			var critMult = Resolve(mods, SpellModificationProperty.CritMult, spellAttributeMask);
			clone.critDamageMultiplierRange = new FloatRange
			{
				min = Mathf.Max(1f, source.critDamageMultiplierRange.min * critMult.Multiplier + critMult.flat / 100f),
				max = Mathf.Max(1f, source.critDamageMultiplierRange.max * critMult.Multiplier + critMult.flat / 100f)
			};
			clone.critDamageMultiplierRange.ClampOrder();

			var aoe = Resolve(mods, SpellModificationProperty.AreaOfEffect, spellAttributeMask);
			var h = clone.hitBoxData;
			if (h.isSphere) h.sphereRadius = h.sphereRadius * aoe.Multiplier + aoe.flat;
			if (h.isRect) h.rectDimension = h.rectDimension * aoe.Multiplier + new Vector2(aoe.flat, aoe.flat);
			clone.hitBoxData = h;

			var knockback = Resolve(mods, SpellModificationProperty.KnockbackStrength, spellAttributeMask);
			clone.knockbackStrength = Mathf.Max(0f, source.knockbackStrength * knockback.Multiplier + knockback.flat);

			clone.behaviors = CloneBehaviorsAndApply(source.behaviors, mods, spellAttributeMask);
			return clone;
		}

		static FloatRange ApplyDamageRange(FloatRange source, ResolvedModifier typeMod, ResolvedModifier dmgScaling)
		{
			float mult = typeMod.Multiplier * dmgScaling.Multiplier;
			var r = new FloatRange
			{
				min = Mathf.Max(0f, (source.min + typeMod.flat) * mult),
				max = Mathf.Max(0f, (source.max + typeMod.flat) * mult)
			};
			r.ClampOrder();
			return r;
		}

		static List<AttackEntityBehavior> CloneBehaviorsAndApply(List<AttackEntityBehavior> source, SpellModifications mods, SpellAttributeMask spellMask)
		{
			if (source == null) return new List<AttackEntityBehavior>();
			var list = new List<AttackEntityBehavior>(source.Count);
			foreach (var b in source)
			{
				if (b == null) continue;
				var cloned = b.Clone();
				cloned.ApplyModifications(mods, spellMask);
				list.Add(cloned);
			}
			return list;
		}
	}
}
