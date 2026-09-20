using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Telemetry;
using UnityEngine;

namespace BridgeOfBlood.Effects
{
	public interface IValue<out T>
	{
		T Resolve(EffectContext context);
		string GetDescription();

		void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination) { }
	}

	[Serializable]
	public class ConstantValue : IValue<float>
	{
		public float value;

		public float Resolve(EffectContext context) => value;

		public string GetDescription() => EffectDescription.FormatNumber(value);
	}

	[Serializable, MenuPath("Combat")]
	public class TelemetryStatValue : IValue<float>
	{
		public float coefficient = 1f;
		public MetricsScope scope;

		[SerializeReference, SerializeInterface]
		public TelemetryStatArgs args;

		public float Resolve(EffectContext context)
		{
			if (args is DpsTelemetryStatArgs)
				return coefficient * context.GetTelemetryDps(scope);
			return coefficient * context.GetTelemetry(scope, args.GetKey());
		}

		public string GetDescription()
		{
			if (args == null)
				return string.Empty;
			return EffectDescription.Scale(coefficient, EffectDescription.TelemetryStatPhrase(args, scope));
		}

		public void CollectTelemetrySubscriptions(List<TelemetryStatArgs> destination)
		{
			if (args == null || !args.RequiresEventSubscription)
				return;
			destination.Add(args);
		}
	}

	public enum SpellInvocationProperty : byte
	{
		TotalSpellsCasted,
		SpellLoopNumber,
		SpellSlotNumber,
		SpellLoopSlotCount,
		SpellLoopsPerRound,
	}

	public static class SpellInvocationResolver
	{
		public static float GetValue(SpellInvocationProperty property, in SpellInvocationContext context) => property switch
		{
			SpellInvocationProperty.TotalSpellsCasted => context.totalSpellsCasted,
			SpellInvocationProperty.SpellLoopNumber => context.spellLoopNumber,
			SpellInvocationProperty.SpellSlotNumber => context.spellSlotNumber,
			SpellInvocationProperty.SpellLoopSlotCount => context.spellLoopSlotCount,
			SpellInvocationProperty.SpellLoopsPerRound => context.spellLoopsPerRound,
			_ => throw new ArgumentOutOfRangeException(nameof(property), property, null)
		};

		public static SpellAttributeMask GetSlotAttributes(in SpellInvocationContext context, int oneBasedSlot)
		{
			var spells = context.spells;
			if (spells == null || oneBasedSlot < 1 || oneBasedSlot > spells.Count)
				return SpellAttributeMask.None;
			var def = spells[oneBasedSlot - 1].Definition;
			return def != null ? def.attributeMask : SpellAttributeMask.None;
		}

		public static int GetSlotSpellId(in SpellInvocationContext context, int oneBasedSlot)
		{
			var spells = context.spells;
			if (spells == null || oneBasedSlot < 1 || oneBasedSlot > spells.Count)
				return 0;
			return spells[oneBasedSlot - 1].spellId;
		}

		public static int CountSlotsWithAttribute(IReadOnlyList<RuntimeSpell> spells, SpellAttributeMask mask)
		{
			if (spells == null) return 0;
			int count = 0;
			for (int i = 0; i < spells.Count; i++)
			{
				var def = spells[i].Definition;
				if (def != null && (def.attributeMask & mask) != 0)
					count++;
			}
			return count;
		}

		public static int CountCastsWithAttribute(IReadOnlyList<RuntimeSpell> spells, SpellAttributeMask mask)
		{
			if (spells == null) return 0;
			int count = 0;
			for (int i = 0; i < spells.Count; i++)
			{
				var spell = spells[i];
				var def = spell.Definition;
				if (def != null && (def.attributeMask & mask) != 0)
					count += spell.InvocationCount;
			}
			return count;
		}
	}

	[Serializable, MenuPath("Spell")]
	public class SpellInvocationValue : IValue<float>
	{
		public float coefficient = 1f;
		public SpellInvocationProperty property;

		public float Resolve(EffectContext context)
		{
			return coefficient * SpellInvocationResolver.GetValue(property, in context.spellInvocation);
		}

		public string GetDescription() =>
			EffectDescription.Scale(coefficient, EffectDescription.InvocationPhrase(property));
	}

	[Serializable, MenuPath("Spell")]
	public class SpellSlotCountValue : IValue<float>
	{
		public SpellAttributeMask attributeFilter;

		public float Resolve(EffectContext context)
		{
			return SpellInvocationResolver.CountSlotsWithAttribute(
				context.spellInvocation.spells, attributeFilter);
		}

		public string GetDescription()
		{
			string adjectives = EffectDescription.AttributeAdjectives(attributeFilter);
			return string.IsNullOrEmpty(adjectives)
				? "the number of spells in the loop"
				: $"the number of {adjectives} spells in the loop";
		}
	}

	[Serializable, MenuPath("Spell")]
	public class SpellCastCountByAttributeValue : IValue<float>
	{
		public SpellAttributeMask attributeFilter;

		public float Resolve(EffectContext context)
		{
			return SpellInvocationResolver.CountCastsWithAttribute(
				context.spellInvocation.spells, attributeFilter);
		}

		public string GetDescription()
		{
			string adjectives = EffectDescription.AttributeAdjectives(attributeFilter);
			return string.IsNullOrEmpty(adjectives)
				? "spells cast"
				: $"{adjectives} spells cast";
		}
	}

	public enum SlotReference : byte
	{
		Current,
		Previous
	}

	[Serializable, MenuPath("Spell")]
	public class SlotAttributeCheckValue : IValue<float>
	{
		public SlotReference slot;
		public SpellAttributeMask attributeFilter;

		public float Resolve(EffectContext context)
		{
			ref readonly var inv = ref context.spellInvocation;
			int oneBasedSlot = slot switch
			{
				SlotReference.Current => inv.spellSlotNumber,
				SlotReference.Previous => inv.spellSlotNumber - 1,
				_ => 0
			};
			var attributes = SpellInvocationResolver.GetSlotAttributes(in inv, oneBasedSlot);
			return (attributes & attributeFilter) != 0 ? 1f : 0f;
		}

		public string GetDescription() =>
			EffectDescription.SlotAttributeCheck(slot, attributeFilter, negated: false);
	}
}
