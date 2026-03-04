using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using UnityEngine;

namespace BridgeOfBlood.Data.Spells
{
	/// <summary>
	/// Bakes SpellModifications into cloned AttackEntityData so the returned entity has modified values.
	/// </summary>
	public static class SpellModificationsApplicator
	{
		public static AttackEntityData CloneAndApply(AttackEntityData source, SpellAttributeMask spellAttributeMask, SpellModifications mods)
		{
			if (source == null) return null;
			if (mods == null) return source;

			var clone = Object.Instantiate(source);

			float typePhys = ResolveToMultiplier(Get(mods.damageTypeScaling, DamageType.Physical));
			float typeCold = ResolveToMultiplier(Get(mods.damageTypeScaling, DamageType.Cold));
			float typeFire = ResolveToMultiplier(Get(mods.damageTypeScaling, DamageType.Fire));
			float typeLightning = ResolveToMultiplier(Get(mods.damageTypeScaling, DamageType.Lightning));

			float attr = 1f;
			if (mods.spellAttributeDamageScaling != null && spellAttributeMask != SpellAttributeMask.None)
			{
				foreach (var kvp in mods.spellAttributeDamageScaling)
					if ((spellAttributeMask & kvp.Key) != 0)
						attr *= ResolveToMultiplier(kvp.Value);
			}

			clone.physicalDamage = Mathf.Max(0f, source.physicalDamage * typePhys * attr);
			clone.coldDamage = Mathf.Max(0f, source.coldDamage * typeCold * attr);
			clone.fireDamage = Mathf.Max(0f, source.fireDamage * typeFire * attr);
			clone.lightningDamage = Mathf.Max(0f, source.lightningDamage * typeLightning * attr);

			if (mods.criticalStrikeChance != null)
			{
				float chanceMult = ResolveToMultiplier(mods.criticalStrikeChance);
				int chanceFlat = GetFlatAdditive(mods.criticalStrikeChance);
				clone.critChance = Mathf.Clamp01(source.critChance * chanceMult + chanceFlat / 100f);
			}

			if (mods.criticalStrikeMultiplier != null)
			{
				float multMult = ResolveToMultiplier(mods.criticalStrikeMultiplier);
				int multFlat = GetFlatAdditive(mods.criticalStrikeMultiplier);
				clone.critDamageMultiplier = Mathf.Max(1f, source.critDamageMultiplier * multMult + multFlat / 100f);
			}

			if (mods.flatAddedDamage != null)
			{
				foreach (var flat in mods.flatAddedDamage)
				{
					float v = (flat.min + flat.max) * 0.5f;
					switch (flat.type)
					{
						case DamageType.Physical: clone.physicalDamage += v; break;
						case DamageType.Cold: clone.coldDamage += v; break;
						case DamageType.Fire: clone.fireDamage += v; break;
						case DamageType.Lightning: clone.lightningDamage += v; break;
					}
				}
			}

			if (mods.areaOfEffect != null)
			{
				float aoe = ResolveToMultiplier(mods.areaOfEffect);
				int aoeFlat = GetFlatAdditive(mods.areaOfEffect);
				var h = clone.hitBoxData;
				if (h.isSphere) h.sphereRadius = h.sphereRadius * aoe + aoeFlat;
				if (h.isRect) h.rectDimension = h.rectDimension * aoe + new Vector2(aoeFlat, aoeFlat);
				clone.hitBoxData = h;
			}

			clone.behaviors = CloneBehaviorsAndApply(source.behaviors, mods);
			return clone;
		}

		static List<AttackEntityBehavior> CloneBehaviorsAndApply(List<AttackEntityBehavior> source, SpellModifications mods)
		{
			if (source == null) return new List<AttackEntityBehavior>();
			var list = new List<AttackEntityBehavior>(source.Count);
			float chainMult = ResolveToMultiplier(mods.chains);
			int chainFlat = GetFlatAdditive(mods.chains);
			float pierceMult = ResolveToMultiplier(mods.pierce);
			int pierceFlat = GetFlatAdditive(mods.pierce);

			foreach (var b in source)
			{
				if (b == null) continue;
				if (b is PierceBehavior pb)
				{
					var c = new PierceBehavior { isActive = pb.isActive, maxEnemiesHit = Mathf.Max(0, (int)(pb.maxEnemiesHit * pierceMult) + pierceFlat) };
					list.Add(c);
				}
				else if (b is ChainBehavior cb)
				{
					var c = new ChainBehavior
					{
						isActive = cb.isActive,
						mode = cb.mode,
						chainCount = Mathf.Max(0, (int)(cb.chainCount * chainMult) + chainFlat),
						chainRange = cb.chainRange,
						targetSelect = cb.targetSelect,
						excludePreviouslyHit = cb.excludePreviouslyHit
					};
					list.Add(c);
				}
				else if (b is ExpirationBehavior eb)
				{
					list.Add(new ExpirationBehavior { isActive = eb.isActive, maxTimeAlive = eb.maxTimeAlive, maxDistanceTravelled = eb.maxDistanceTravelled });
				}
			}
			return list;
		}

		public static float ResolveToMultiplier(ParamaterModifier mod)
		{
			if (mod == null) return 1f;
			float additive = 1f + mod.percentIncreased / 100f;
			float more = 1f;
			if (mod.moreMultipliers != null)
				for (int i = 0; i < mod.moreMultipliers.Count; i++)
					more *= 1f + mod.moreMultipliers[i] / 100f;
			return additive * more;
		}

		static int GetFlatAdditive(ParamaterModifier mod) => mod?.flatAdditiveValue ?? 0;

		static ParamaterModifier Get(Dictionary<DamageType, ParamaterModifier> d, DamageType t)
		{
			return d != null && d.TryGetValue(t, out var m) ? m : null;
		}
	}
}
