using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;

namespace BridgeOfBlood.Effects
{
	public interface IValue<out T>
	{
		T Resolve(EffectContext context);
	}

	[Serializable]
	public class ConstantValue : IValue<float>
	{
		public float value;

		public float Resolve(EffectContext context) => value;
	}

	[Serializable, MenuPath("Combat")]
	public class CombatMetricValue : IValue<float>
	{
		public float coefficient = 1f;
		public MetricsScope scope;
		public CombatMetricProperty property;

		public float Resolve(EffectContext context)
		{
			var metrics = context.GetMetrics(scope);
			return coefficient * CombatMetricResolver.GetValue(property, in metrics);
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
					count += spell.invocationCount;
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
	}
}
