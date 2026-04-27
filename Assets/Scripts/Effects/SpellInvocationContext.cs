using System.Collections.Generic;
using BridgeOfBlood.Data.Spells;

namespace BridgeOfBlood.Effects
{
	public struct SpellInvocationContext
	{
		public int totalSpellsCasted;
		public int spellLoopNumber;
		public int spellSlotNumber;
		public int spellLoopSlotCount;
		public int spellLoopsPerRound;
		public IReadOnlyList<RuntimeSpell> spells;
	}
}
