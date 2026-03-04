using BridgeOfBlood.Data.Shared;
using System.Collections.Generic;
using UnityEngine;

namespace BridgeOfBlood.Data.Spells
{
	/// <summary>
	/// ScriptableObject authoring asset for test SpellModifications.
	/// Use Create > Bridge of Blood > Spell Modifications Test to create an asset.
	/// </summary>
	[CreateAssetMenu(fileName = "SpellModificationsTest", menuName = "Bridge of Blood/Spell Modifications Test", order = 0)]
	public class SpellModificationsTestData : ScriptableObject
	{
		[Header("Core parameters")]
		public ParamaterModifier criticalStrikeChance;
		public ParamaterModifier criticalStrikeMultiplier;
		public ParamaterModifier chains;
		public ParamaterModifier pierce;
		public ParamaterModifier areaOfEffect;
		public ParamaterModifier duration;
		public ParamaterModifier castSpeed;

		[Header("Damage scaling (spell attribute)")]
		public List<SpellAttributeScalingEntry> spellAttributeDamageScaling = new List<SpellAttributeScalingEntry>();

		[Header("Damage scaling (damage type)")]
		public List<DamageTypeScalingEntry> damageTypeScaling = new List<DamageTypeScalingEntry>();

		[Header("Damage type penetration")]
		public List<DamageTypeScalingEntry> damageTypePenetration = new List<DamageTypeScalingEntry>();

		[Header("Flat added damage")]
		public List<FlatDamage> flatAddedDamage = new List<FlatDamage>();

		[Header("Conversion")]
		public List<DamageConversion> conversions = new List<DamageConversion>();

		[Header("Extra damage as")]
		public List<ExtraDamageAs> extraDamageAs = new List<ExtraDamageAs>();

		/// <summary>
		/// Builds a runtime SpellModifications instance from this authoring data.
		/// </summary>
		public SpellModifications GetModifications()
		{
			var mods = new SpellModifications
			{
				criticalStrikeChance = criticalStrikeChance,
				criticalStrikeMultiplier = criticalStrikeMultiplier,
				chains = chains,
				pierce = pierce,
				areaOfEffect = areaOfEffect,
				duration = duration,
				castSpeed = castSpeed,
				spellAttributeDamageScaling = BuildDictionary(spellAttributeDamageScaling),
				damageTypeScaling = BuildDictionary(damageTypeScaling),
				damageTypePenetration = BuildDictionary(damageTypePenetration),
				flatAddedDamage = flatAddedDamage != null ? new List<FlatDamage>(flatAddedDamage) : new List<FlatDamage>(),
				conversions = conversions != null ? new List<DamageConversion>(conversions) : new List<DamageConversion>(),
				extraDamageAs = extraDamageAs != null ? new List<ExtraDamageAs>(extraDamageAs) : new List<ExtraDamageAs>()
			};
			return mods;
		}

		private static Dictionary<SpellAttributeMask, ParamaterModifier> BuildDictionary(List<SpellAttributeScalingEntry> entries)
		{
			var dict = new Dictionary<SpellAttributeMask, ParamaterModifier>();
			if (entries == null) return dict;
			foreach (var e in entries)
			{
				if (e.modifier == null) continue;
				if (!dict.TryGetValue(e.attribute, out var existing))
				{
					existing = new ParamaterModifier();
					dict[e.attribute] = existing;
				}
				existing.Add(e.modifier);
			}
			return dict;
		}

		private static Dictionary<DamageType, ParamaterModifier> BuildDictionary(List<DamageTypeScalingEntry> entries)
		{
			var dict = new Dictionary<DamageType, ParamaterModifier>();
			if (entries == null) return dict;
			foreach (var e in entries)
			{
				if (e.modifier == null) continue;
				if (!dict.TryGetValue(e.damageType, out var existing))
				{
					existing = new ParamaterModifier();
					dict[e.damageType] = existing;
				}
				existing.Add(e.modifier);
			}
			return dict;
		}
	}

	[System.Serializable]
	public struct SpellAttributeScalingEntry
	{
		public SpellAttributeMask attribute;
		public ParamaterModifier modifier;
	}

	[System.Serializable]
	public struct DamageTypeScalingEntry
	{
		public DamageType damageType;
		public ParamaterModifier modifier;
	}
}
