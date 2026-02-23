using BridgeOfBlood.Data.Shared;
using Unity.Collections;

namespace BridgeOfBlood.Data.Spells
{
	/// <summary>
	/// Resolves spell modifications through the modifier pipeline.
	/// Applies modifiers in order: Flat → Additive → Conditional → Global → Cross-Spell
	/// Ensures multiplier order is explicit and predictable.
	/// </summary>
	public static class SpellModificationResolver
	{
		
	}
}

