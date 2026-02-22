using BridgeOfBlood.Data.Shared;
using Unity.Collections;
using Unity.Mathematics;

namespace BridgeOfBlood.Data.Spells
{
	

	/// <summary>
	/// Runtime spell data. Struct-based for performance and determinism.
	/// Spells are modified through the modifier pipeline, not direct mutation.
	/// </summary>
	public class Spell
	{
		// Base stats (flat)
		
		public int baseMultiplier;
		
		// Timing
		public float castCompletionDuration;
		public float castTime;
		
		// Attributes for tag-based synergies
		public SpellAttributeMask attributeMask;
		
		// Runtime tracking
		public int spellId;
		public int invocationCount;
		public double roundTimeInvokedAt;
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
		public SpellAttributeMask targetAttributeMask; // Which spells this applies to (for filtering)
		
		// Modifier values
		public int flatDamageBonus;
		public float damageMultiplier;
		public float castTimeMultiplier;
	}
}

