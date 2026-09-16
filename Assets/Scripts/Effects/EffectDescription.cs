using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Telemetry;
using UnityEngine;

namespace BridgeOfBlood.Effects
{
	public static class EffectDescription
	{
		public const SpellAttributeMask AllAttributes =
			SpellAttributeMask.AOE
			| SpellAttributeMask.Chains
			| SpellAttributeMask.Physical
			| SpellAttributeMask.Fire
			| SpellAttributeMask.Cold
			| SpellAttributeMask.Lightning;

		public static bool IsAllAttributes(SpellAttributeMask mask) =>
			(mask & AllAttributes) == AllAttributes;

		public static string FormatNumber(float value)
		{
			if (Mathf.Approximately(value, Mathf.Round(value)))
				return Mathf.RoundToInt(value).ToString();
			return value.ToString("0.##");
		}

		public static string ComparisonWord(Comparison comparison) => comparison switch
		{
			Comparison.GreaterThan => "greater than",
			Comparison.LessThan => "less than",
			Comparison.Equal => "equals",
			Comparison.NotEqual => "does not equal",
			Comparison.GreaterThanOrEqual => "at least",
			Comparison.LessThanOrEqual => "at most",
			_ => comparison.ToString()
		};

		public static string StatName(SpellModificationProperty property) => property switch
		{
			SpellModificationProperty.CritChance => "crit chance",
			SpellModificationProperty.CritMult => "crit multiplier",
			SpellModificationProperty.Chains => "chains",
			SpellModificationProperty.Pierce => "pierce",
			SpellModificationProperty.AreaOfEffect => "area of effect",
			SpellModificationProperty.Duration => "duration",
			SpellModificationProperty.CastSpeed => "cast speed",
			SpellModificationProperty.Projectiles => "projectiles",
			SpellModificationProperty.DamageScaling => "damage",
			SpellModificationProperty.PhysicalDamageScaling => "physical damage",
			SpellModificationProperty.ColdDamageScaling => "cold damage",
			SpellModificationProperty.FireDamageScaling => "fire damage",
			SpellModificationProperty.LightningDamageScaling => "lightning damage",
			SpellModificationProperty.PhysicalPenetration => "physical penetration",
			SpellModificationProperty.ColdPenetration => "cold penetration",
			SpellModificationProperty.FirePenetration => "fire penetration",
			SpellModificationProperty.LightningPenetration => "lightning penetration",
			SpellModificationProperty.KnockbackStrength => "knockback",
			SpellModificationProperty.ManaCost => "mana cost",
			_ => property.ToString()
		};

		public static string AttributeAdjectives(SpellAttributeMask mask)
		{
			if (mask == SpellAttributeMask.None)
				return string.Empty;

			var parts = new List<string>();
			if ((mask & SpellAttributeMask.AOE) != 0) parts.Add("AOE");
			if ((mask & SpellAttributeMask.Chains) != 0) parts.Add("Chain");
			if ((mask & SpellAttributeMask.Physical) != 0) parts.Add("Physical");
			if ((mask & SpellAttributeMask.Fire) != 0) parts.Add("Fire");
			if ((mask & SpellAttributeMask.Cold) != 0) parts.Add("Cold");
			if ((mask & SpellAttributeMask.Lightning) != 0) parts.Add("Lightning");
			return JoinOr(parts);
		}

		public static string AilmentNames(StatusAilmentFlag flags)
		{
			if (flags == StatusAilmentFlag.None)
				return string.Empty;

			var parts = new List<string>();
			if ((flags & StatusAilmentFlag.Frozen) != 0) parts.Add("Frozen");
			if ((flags & StatusAilmentFlag.Stunned) != 0) parts.Add("Stunned");
			if ((flags & StatusAilmentFlag.Poisoned) != 0) parts.Add("Poisoned");
			if ((flags & StatusAilmentFlag.Ignited) != 0) parts.Add("Ignited");
			if ((flags & StatusAilmentFlag.Shocked) != 0) parts.Add("Shocked");
			if ((flags & StatusAilmentFlag.Bleeding) != 0) parts.Add("Bleeding");
			return JoinOr(parts);
		}

		public static string InvocationPhrase(SpellInvocationProperty property) => property switch
		{
			SpellInvocationProperty.TotalSpellsCasted => "total spells cast",
			SpellInvocationProperty.SpellLoopNumber => "the current loop number",
			SpellInvocationProperty.SpellSlotNumber => "this spell's slot",
			SpellInvocationProperty.SpellLoopSlotCount => "the number of spells in the loop",
			SpellInvocationProperty.SpellLoopsPerRound => "spell loops per round",
			_ => property.ToString()
		};

		public static string ScopePhrase(MetricsScope scope) => scope switch
		{
			MetricsScope.Frame => "this frame",
			MetricsScope.SpellCast => "this spell cast",
			MetricsScope.SpellLoop => "this spell loop",
			MetricsScope.Round => "this round",
			MetricsScope.Game => "this game",
			_ => scope.ToString()
		};

		public static string TelemetryStatPhrase(TelemetryStatArgs args, MetricsScope scope)
		{
			return $"{args.GetDescription()} {ScopePhrase(scope)}";
		}

		public static string Scale(float coefficient, string phrase)
		{
			if (Mathf.Approximately(coefficient, 1f))
				return phrase;
			return $"{FormatNumber(coefficient)} × {phrase}";
		}

		public static string SlotAttributeCheck(SlotReference slot, SpellAttributeMask attributes, bool negated)
		{
			string which = slot == SlotReference.Previous ? "previous" : "current";
			string copula = negated ? "is not" : "is";
			return $"the {which} spell {copula} {AttributeAdjectives(attributes)}";
		}

		public static string SpellName(RuntimeSpell spell)
		{
			if (spell == null || spell.Definition == null)
				return "a specific spell";
			string display = spell.Definition.ShopItemDefinition.DisplayName;
			return string.IsNullOrEmpty(display) ? spell.Definition.name : display;
		}

		public static string JoinAnd(IReadOnlyList<ICondition> conditions)
		{
			return Join(conditions, c => c.GetDescription(), " and ");
		}

		public static string JoinThen(IReadOnlyList<IEffect> effects)
		{
			return Join(effects, e => e.GetDescription(), "; ");
		}

		public static string JoinLines(IReadOnlyList<IEffect> effects)
		{
			return Join(effects, e => e.GetDescription(), "\n");
		}

		public static string Describe(ParameterModifier modifier)
		{
			if (modifier == null)
				return string.Empty;

			string stat = StatName(modifier.property);
			var parts = new List<string>();
			AppendModifierPart(parts, modifier.flatAdditive, ModifierKind.Flat, stat);
			AppendModifierPart(parts, modifier.percentIncreased, ModifierKind.Increased, stat);
			AppendModifierPart(parts, modifier.moreMultiplier, ModifierKind.More, stat);
			if (parts.Count == 0)
				return string.Empty;

			string text = string.Join(" and ", parts);
			if (modifier.filter == SpellAttributeMask.None)
				return text;
			return $"{text} for {AttributeAdjectives(modifier.filter)} spells";
		}

		public static string Describe(ResolvedModifier modifier, SpellModificationProperty property)
		{
			string stat = StatName(property);
			var parts = new List<string>();
			if (!Mathf.Approximately(modifier.flat, 0f))
				parts.Add($"+{FormatNumber(modifier.flat)} {stat}");
			if (!Mathf.Approximately(modifier.percentIncreased, 0f))
				parts.Add($"{FormatNumber(modifier.percentIncreased)}% increased {stat}");
			if (!Mathf.Approximately(modifier.moreCombined, 1f))
				parts.Add($"{FormatNumber((modifier.moreCombined - 1f) * 100f)}% more {stat}");
			return string.Join(" and ", parts);
		}

		static void AppendModifierPart(List<string> parts, IValue<float> value, ModifierKind kind, string stat)
		{
			if (value == null)
				return;
			string line = FormatModifier(value, kind, stat);
			if (!string.IsNullOrEmpty(line))
				parts.Add(line);
		}

		static string FormatModifier(IValue<float> value, ModifierKind kind, string stat)
		{
			string desc = value.GetDescription();
			return kind switch
			{
				ModifierKind.Flat => $"+{desc} {stat}",
				ModifierKind.Increased => $"{desc}% increased {stat}",
				ModifierKind.More => $"{desc}% more {stat}",
				_ => $"{desc} {stat}"
			};
		}

		static string JoinOr(List<string> parts)
		{
			if (parts.Count == 0)
				return string.Empty;
			if (parts.Count == 1)
				return parts[0];
			return string.Join(" or ", parts);
		}

		static string Join<T>(IReadOnlyList<T> items, Func<T, string> describe, string separator) where T : class
		{
			if (items == null || items.Count == 0)
				return string.Empty;

			var parts = new List<string>(items.Count);
			for (int i = 0; i < items.Count; i++)
			{
				T item = items[i];
				if (item == null)
					continue;
				string text = describe(item);
				if (string.IsNullOrEmpty(text))
					continue;
				parts.Add(text);
			}
			return string.Join(separator, parts);
		}

		enum ModifierKind
		{
			Flat,
			Increased,
			More
		}
	}
}
