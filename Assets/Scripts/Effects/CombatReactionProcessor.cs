using System.Collections.Generic;
using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;

namespace BridgeOfBlood.Effects
{
	/// <summary>
	/// Dispatches item-authored <see cref="CombatAttackSpawnReaction"/> after simulation using frame combat events.
	/// Resolves <see cref="EnemyCombatSnapshot"/> for ailments from live <see cref="EnemyBuffers"/> here (not in ailment/kill emitters).
	/// Run before <see cref="GameSimulation.ClearFrameCombatEvents"/> so lists are still populated.
	/// </summary>
	public static class CombatReactionProcessor
	{
		static readonly Dictionary<int, int> ScratchEntityToIndex = new Dictionary<int, int>(256);

		static void RebuildEntityIdToIndex(EnemyBuffers buffers)
		{
			ScratchEntityToIndex.Clear();
			int n = buffers.Length;
			for (int i = 0; i < n; i++)
				ScratchEntityToIndex[buffers.EntityIds[i]] = i;
		}

		static EnemyCombatSnapshot ResolveSnapshotForEntity(int enemyEntityId, EnemyBuffers buffers)
		{
			if (!ScratchEntityToIndex.TryGetValue(enemyEntityId, out int idx))
				return default;
			if (idx < 0 || idx >= buffers.Length)
				return default;
			return EnemyCombatSnapshotUtil.FromEnemyIndex(idx, buffers.Vitality, buffers.CombatTraits);
		}

		public static void ProcessAfterSimulationFrame(
			NativeArray<EnemyKilledEvent> killEvents,
			NativeArray<StatusAilmentAppliedEvent> ailmentEvents,
			EnemyBuffers enemyBuffers,
			PlayerInventory inventory,
			AttackEntityManager attackEntities)
		{
			RebuildEntityIdToIndex(enemyBuffers);

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

					if (reaction.trigger == CombatReactionTrigger.EnemyKilled)
					{
						for (int k = 0; k < killEvents.Length; k++)
						{
							EnemyKilledEvent ke = killEvents[k];
							reaction.TrySpawnFromKill(in ke, attackEntities);
						}
					}
					else if (reaction.trigger == CombatReactionTrigger.StatusAilmentApplied)
					{
						for (int a = 0; a < ailmentEvents.Length; a++)
						{
							StatusAilmentAppliedEvent ae = ailmentEvents[a];
							EnemyCombatSnapshot snap = ResolveSnapshotForEntity(ae.enemyEntityId, enemyBuffers);
							reaction.TrySpawnFromAilment(in ae, in snap, attackEntities);
						}
					}
				}
			}
		}
	}
}
