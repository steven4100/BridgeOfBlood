using System;
using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
using Unity.Mathematics;
using UnityEngine;

namespace BridgeOfBlood.Effects
{
	/// <summary>
	/// Authoring entry: trigger + filters + baked spawn payload (no ScriptableObject references).
	/// Spell provenance on spawn uses the triggering event's spell id / invocation id.
	/// </summary>
	[Serializable]
	public class CombatAttackSpawnReaction
	{
		public const int SpellIdMatchAny = 0;

		[Header("Trigger")]
		[Tooltip("Kill vs ailment application.")]
		public CombatReactionTrigger trigger;

		[Header("Filters")]
		[Tooltip("Only react when event spell id equals this runtime spell id. Use 0 to match any spell.")]
		public int spellIdFilter;

		[Tooltip("For StatusAilmentApplied: require (event flag & mask) != 0. Use None to match any ailment application.")]
		public StatusAilmentFlag ailmentMaskFilter;

		[Header("Spawn")]
		[Tooltip("Baked AttackEntitySpawnPayload (spell id / invocation on the asset are ignored; overwritten from the combat event when spawning).")]
		public AttackEntitySpawnPayload spawnPayload;

		[Tooltip("Added to the victim position when spawning.")]
		public Vector2 spawnOffsetWorld;

		[Tooltip("Optional last-mile tweaks after baking.")]
		[SerializeReference]
		public IAttackSpawnModifier spawnModifier;

		internal bool MatchesSpell(int eventSpellId)
		{
			if (spellIdFilter == SpellIdMatchAny)
				return true;
			return spellIdFilter == eventSpellId;
		}

		internal bool MatchesAilmentFlag(StatusAilmentFlag eventFlag)
		{
			if (ailmentMaskFilter == StatusAilmentFlag.None)
				return true;
			return (eventFlag & ailmentMaskFilter) != 0;
		}

		internal void TrySpawnFromKill(in EnemyKilledEvent evt, AttackEntityManager mgr)
		{
			if (trigger != CombatReactionTrigger.EnemyKilled)
				return;
			if (!MatchesSpell(evt.spellId))
				return;

			AttackEntitySpawnPayload payload = bakedSpawnPayload.ToAttackEntitySpawnPayload(evt.spellId, evt.spellInvocationId);
			spawnModifier?.ModifyKillSpawn(in evt, ref payload);
			float2 origin = evt.position + new float2(spawnOffsetWorld.x, spawnOffsetWorld.y);
			mgr.Spawn(payload, origin);
		}

		internal void TrySpawnFromAilment(in StatusAilmentAppliedEvent evt, in EnemyCombatSnapshot combatSnapshot, AttackEntityManager mgr)
		{
			if (trigger != CombatReactionTrigger.StatusAilmentApplied)
				return;
			if (!MatchesSpell(evt.spellId))
				return;
			if (!MatchesAilmentFlag(evt.ailmentFlag))
				return;

			AttackEntitySpawnPayload payload = bakedSpawnPayload.ToAttackEntitySpawnPayload(evt.spellId, evt.spellInvocationId);
			spawnModifier?.ModifyAilmentSpawn(in evt, in combatSnapshot, ref payload);
			float2 origin = evt.position + new float2(spawnOffsetWorld.x, spawnOffsetWorld.y);
			mgr.Spawn(payload, origin);
		}
	}
}
