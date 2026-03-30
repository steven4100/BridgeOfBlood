using BridgeOfBlood.Data.Shared;

namespace BridgeOfBlood.Data.Spells
{
	/// <summary>
	/// Per-loop-slot runtime state. Static spell data is read from <see cref="Definition"/>; only invocation counters mutate during play.
	/// </summary>
	public sealed class RuntimeSpell
	{
		public SpellAuthoringData Definition { get; }

		public int spellId;
		public int invocationCount;
		public double roundTimeInvokedAt;

		public RuntimeSpell(SpellAuthoringData definition)
		{
			Definition = definition;
			spellId = definition != null ? definition.GetInstanceID() : 0;
			invocationCount = 0;
			roundTimeInvokedAt = 0;
		}
	}

	/// <summary>
	/// Spell modifier applied during the modification pipeline.
	/// Follows the scaling philosophy: Flat → Additive → Conditional → Global → Cross-Spell
	///
	/// Note: Conditional evaluation happens upstream (in idol/effect systems).
	/// If a SpellModifier exists, it should be applied - predicates are resolved before modifier creation.
	///
	/// Spell attributes are immutable at runtime - they define the spell's identity and are set during authoring.
	/// </summary>
	public struct SpellModifier
	{
		public SpellAttributeMask targetAttributeMask;

		public int flatDamageBonus;
		public float damageMultiplier;
		public float castTimeMultiplier;
	}
}
