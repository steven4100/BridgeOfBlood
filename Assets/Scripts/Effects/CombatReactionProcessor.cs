using System.Collections.Generic;
using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Collections;

namespace BridgeOfBlood.Effects
{
	/// <summary>
	/// Resolves managed <see cref="CombatSpawnContract"/> entries against this frame's kill/ailment events and
	/// spawns the matching attack entities directly via <see cref="AttackEntityManager.Spawn"/>.
	/// Run (via <see cref="GameSimulation"/>) before <see cref="GameSimulation.ClearFrameCombatEvents"/> so event lists are still populated.
	/// Main-thread only (contracts carry managed refs).
	/// </summary>
	public static class CombatReactionProcessor
	{
		public static void ProcessFrameCombatReactions(
			NativeArray<EnemyKilledEvent> killEvents,
			NativeArray<StatusAilmentAppliedEvent> ailmentEvents,
			IReadOnlyList<CombatSpawnContract> contracts,
			AttackEntityManager manager)
		{
			if (contracts == null)
				return;

			for (int c = 0; c < contracts.Count; c++)
			{
				CombatSpawnContract contract = contracts[c];
				CombatAttackSpawnReactionRuntime snap = contract.filters;

				if (snap.trigger == CombatReactionTrigger.EnemyKilled)
				{
					for (int k = 0; k < killEvents.Length; k++)
					{
						EnemyKilledEvent ke = killEvents[k];
						if (!MatchesSpellId(ke.spellId, contract))
							continue;

						float scaled = snap.damageMode == CombatReactionSpawnDamageMode.ScaleByTriggeringHitDamage
							? CombatAttackSpawnReaction.ResolveEventScaledDamage(ke.killingBlowDamage, snap.eventDamageCoefficient, snap.minScaledDamage)
							: 0f;

						AttackEntityBuildContext ctx = contract.BuildContext(
							ke.spellId, ke.spellInvocationId, ke.position + snap.spawnOffsetWorld, scaled);
						manager.Spawn(in ctx);
					}
				}
				else if (snap.trigger == CombatReactionTrigger.StatusAilmentApplied)
				{
					for (int a = 0; a < ailmentEvents.Length; a++)
					{
						StatusAilmentAppliedEvent ae = ailmentEvents[a];
						if (!MatchesSpellId(ae.spellId, contract))
							continue;
						if (!MatchesAilmentFlag(snap.ailmentMaskFilter, ae.ailmentFlag))
							continue;

						float scaled = snap.damageMode == CombatReactionSpawnDamageMode.ScaleByTriggeringHitDamage
							? CombatAttackSpawnReaction.ResolveEventScaledDamage(ae.triggeringHitDamage, snap.eventDamageCoefficient, snap.minScaledDamage)
							: 0f;

						AttackEntityBuildContext ctx = contract.BuildContext(
							ae.spellId, ae.spellInvocationId, ae.position + snap.spawnOffsetWorld, scaled);
						manager.Spawn(in ctx);
					}
				}
			}
		}

		static bool MatchesSpellId(int eventSpellId, CombatSpawnContract contract)
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
