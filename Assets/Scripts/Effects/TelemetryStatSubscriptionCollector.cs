using System.Collections.Generic;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Telemetry;

namespace BridgeOfBlood.Effects
{
	public static class TelemetryStatSubscriptionCollector
	{
		public static void CollectFromInventory(
			IReadOnlyList<Item> passives,
			IReadOnlyList<RuntimeSpell> spells,
			List<TelemetryStatArgs> destination)
		{
			destination.Clear();
			if (passives != null)
			{
				for (int i = 0; i < passives.Count; i++)
					passives[i].CollectTelemetrySubscriptions(destination);
			}

			if (spells == null)
				return;

			for (int s = 0; s < spells.Count; s++)
			{
				RuntimeSpell spell = spells[s];
				for (int g = 0; g < spell.Gems.SlotCount; g++)
				{
					RuntimeGem gem = spell.Gems.GetSlot(g);
					if (gem == null)
						continue;
					gem.Definition.CollectTelemetrySubscriptions(destination);
				}
			}
		}
	}
}
