using System.Collections.Generic;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Spells;

namespace BridgeOfBlood.Effects
{
	/// <summary>
	/// Builds passive item <see cref="CombatAttackSpawnReaction"/> entries into managed contracts for <see cref="CombatReactionProcessor"/>.
	/// Contracts hold authoring data + the frame's modifications; damage is rolled at spawn, not here.
	/// </summary>
	public static class CombatReactionContractBuilder
	{
		public static void Build(
			PlayerInventory inventory,
			SpellModifications mods,
			IReadOnlyList<RuntimeSpell> spells,
			out List<CombatSpawnContract> contracts)
		{
			contracts = new List<CombatSpawnContract>(16);

			IReadOnlyList<Item> items = inventory.GetPassiveItems();
			for (int i = 0; i < items.Count; i++)
			{
				Item item = items[i];
				if (item.combatReactions == null)
					continue;

				for (int r = 0; r < item.combatReactions.Count; r++)
				{
					CombatAttackSpawnReaction reaction = item.combatReactions[r];
					if (reaction == null)
						continue;

					CombatAttackSpawnReactionRuntime snap = reaction.BakeRuntimeSnapshot();

					bool definitionSpellResolved = false;
					int definitionFilterSpellId = 0;
					if (snap.spellDefinitionInstanceIdFilter != 0)
					{
						for (int s = 0; s < spells.Count; s++)
						{
							RuntimeSpell rs = spells[s];
							if (!CombatAttackSpawnReaction.MatchesSpellFilter(rs, in snap))
								continue;
							definitionFilterSpellId = rs.spellId;
							definitionSpellResolved = true;
							break;
						}
					}

					contracts.Add(new CombatSpawnContract
					{
						filters = snap,
						attackData = reaction.attackEntity,
						modifications = mods,
						definitionSpellResolved = definitionSpellResolved,
						definitionFilterSpellId = definitionFilterSpellId,
					});
				}
			}
		}
	}
}
