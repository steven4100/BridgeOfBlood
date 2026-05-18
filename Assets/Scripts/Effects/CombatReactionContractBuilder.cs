using System.Collections.Generic;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Spells;
using Unity.Collections;

namespace BridgeOfBlood.Effects
{
	/// <summary>
	/// Bakes passive item <see cref="CombatAttackSpawnReaction"/> entries into struct contracts for <see cref="CombatReactionProcessor"/>.
	/// </summary>
	public static class CombatReactionContractBuilder
	{
		/// <summary>
		/// Allocates <paramref name="contracts"/> with <paramref name="allocator"/>; caller must <see cref="NativeArray{T}.Dispose"/> when done.
		/// </summary>
		public static void Build(
			PlayerInventory inventory,
			SpellModifications mods,
			IReadOnlyList<RuntimeSpell> spells,
			Allocator allocator,
			out NativeArray<CombatSpawnContract> contracts)
		{
			var contractList = new List<CombatSpawnContract>(16);

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

					AttackEntitySpawnPayload template = reaction.BakeTemplatePayload(in snap, mods);

					contractList.Add(new CombatSpawnContract
					{
						filters = snap,
						templatePayload = template,
						definitionSpellResolved = definitionSpellResolved,
						definitionFilterSpellId = definitionFilterSpellId,
					});
				}
			}

			contracts = new NativeArray<CombatSpawnContract>(contractList.Count, allocator);
			for (int i = 0; i < contractList.Count; i++)
				contracts[i] = contractList[i];
		}
	}
}
