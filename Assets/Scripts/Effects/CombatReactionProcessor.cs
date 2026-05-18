using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using Unity.Collections;
using Unity.Mathematics;

namespace BridgeOfBlood.Effects
{
	/// <summary>
	/// Resolves baked <see cref="CombatSpawnContract"/> entries into <see cref="CombatReactionSpawnRequest"/>s (no I/O); consumer spawns.
	/// Run (via <see cref="GameSimulation"/>) before <see cref="GameSimulation.ClearFrameCombatEvents"/> so event lists are still populated.
	/// </summary>
	public static class CombatReactionProcessor
	{
		public static void ProcessFrameCombatReactions(
			NativeArray<EnemyKilledEvent> killEvents,
			NativeArray<StatusAilmentAppliedEvent> ailmentEvents,
			NativeArray<CombatSpawnContract> contracts,
			NativeList<CombatReactionSpawnRequest> spawnRequestsOut)
		{
			for (int c = 0; c < contracts.Length; c++)
			{
				CombatSpawnContract contract = contracts[c];
				CombatAttackSpawnReactionRuntime snap = contract.filters;

				if (snap.trigger == CombatReactionTrigger.EnemyKilled)
				{
					for (int k = 0; k < killEvents.Length; k++)
					{
						EnemyKilledEvent ke = killEvents[k];
						if (!MatchesSpellId(ke.spellId, in contract))
							continue;

						AttackEntitySpawnPayload payload = contract.templatePayload.WithSpellProvenanceForNewEntity(
							ke.spellId,
							ke.spellInvocationId);
						if (snap.damageMode == CombatReactionSpawnDamageMode.ScaleByTriggeringHitDamage)
						{
							CombatAttackSpawnReaction.ApplyEventScaledDamage(
								ref payload,
								ke.killingBlowDamage,
								snap.eventDamageCoefficient,
								snap.minScaledDamage);
						}

						spawnRequestsOut.Add(new CombatReactionSpawnRequest { payload = payload, origin = ke.position + snap.spawnOffsetWorld });
					}
				}
				else if (snap.trigger == CombatReactionTrigger.StatusAilmentApplied)
				{
					for (int a = 0; a < ailmentEvents.Length; a++)
					{
						StatusAilmentAppliedEvent ae = ailmentEvents[a];
						if (!MatchesSpellId(ae.spellId, in contract))
							continue;
						if (!MatchesAilmentFlag(snap.ailmentMaskFilter, ae.ailmentFlag))
							continue;

						AttackEntitySpawnPayload payload = contract.templatePayload.WithSpellProvenanceForNewEntity(
							ae.spellId,
							ae.spellInvocationId);
						if (snap.damageMode == CombatReactionSpawnDamageMode.ScaleByTriggeringHitDamage)
						{
							CombatAttackSpawnReaction.ApplyEventScaledDamage(
								ref payload,
								ae.triggeringHitDamage,
								snap.eventDamageCoefficient,
								snap.minScaledDamage);
						}

						spawnRequestsOut.Add(new CombatReactionSpawnRequest { payload = payload, origin = ae.position + snap.spawnOffsetWorld });
					}
				}
			}
		}

		static bool MatchesSpellId(int eventSpellId, in CombatSpawnContract contract)
		{
			if (contract.filters.spellDefinitionInstanceIdFilter != 0)
				return contract.definitionSpellResolved && eventSpellId == contract.definitionFilterSpellId;

			return true;
		}

		static bool MatchesAilmentFlag(StatusAilmentFlag maskFilter, StatusAilmentFlag eventFlag)
		{
			if (maskFilter == StatusAilmentFlag.None)
				return true;
			return (eventFlag & maskFilter) != 0;
		}
	}
}
