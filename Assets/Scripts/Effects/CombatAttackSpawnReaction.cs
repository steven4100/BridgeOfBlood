using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using Unity.Mathematics;
using UnityEngine;

namespace BridgeOfBlood.Effects
{
	/// <summary>
	/// Authoring entry: trigger, filters, spell / attack template references, damage mode.
	/// Runtime bake is <see cref="CombatAttackSpawnReactionRuntime"/>; spawn rolls + applies spell mods via <see cref="AttackEntityManager.Spawn"/>.
	/// </summary>
	[System.Serializable]
	public class CombatAttackSpawnReaction
	{
		[Header("Trigger")]
		[Tooltip("Kill vs ailment application.")]
		public CombatReactionTrigger trigger;

		[Header("Filters")]
		[Tooltip("Only react when the event spell maps to this definition. Leave empty to match any spell.")]
		public SpellAuthoringData spellFilter;

		[Tooltip("Used for SpellModificationsApplicator when spellFilter is null; typically the mask for this proc.")]
		public SpellAttributeMask modificationAttributeMask;

		[Tooltip("For StatusAilmentApplied: require (event flag & mask) != 0. Use None to match any ailment application.")]
		public StatusAilmentFlag ailmentMaskFilter;

		[Header("Spawn")]
		[Tooltip("Projectile/effect template; rolled + modified at spawn time by AttackEntityManager.")]
		public AttackEntityData attackEntity;

		[Header("Damage")]
		public CombatReactionSpawnDamageMode damageMode;

		[Tooltip("For ScaleByTriggeringHitDamage: multiplied by event killing-blow / triggering-hit damage.")]
		public float eventDamageCoefficient = 1f;

		[Tooltip("Floor for scaled proc damage.")]
		public float minScaledDamage;

		[Tooltip("Added to the victim position when spawning.")]
		public Vector2 spawnOffsetWorld;

		public CombatAttackSpawnReactionRuntime BakeRuntimeSnapshot()
		{
			int defFilter = spellFilter != null ? spellFilter.GetInstanceID() : 0;
			SpellAttributeMask mask = spellFilter != null ? spellFilter.attributeMask : modificationAttributeMask;
			return new CombatAttackSpawnReactionRuntime
			{
				trigger = this.trigger,
				ailmentMaskFilter = this.ailmentMaskFilter,
				spellDefinitionInstanceIdFilter = defFilter,
				spawnOffsetWorld = new float2(this.spawnOffsetWorld.x, this.spawnOffsetWorld.y),
				modificationMask = mask,
				damageMode = this.damageMode,
				eventDamageCoefficient = this.eventDamageCoefficient,
				minScaledDamage = this.minScaledDamage,
			};
		}

		public static bool MatchesSpellFilter(RuntimeSpell runtimeSpell, in CombatAttackSpawnReactionRuntime snap)
		{
			if (snap.spellDefinitionInstanceIdFilter != 0)
				return runtimeSpell.Definition.GetInstanceID() == snap.spellDefinitionInstanceIdFilter;

			return true;
		}

		/// <summary>Computes the target total damage for <see cref="CombatReactionSpawnDamageMode.ScaleByTriggeringHitDamage"/>.</summary>
		internal static float ResolveEventScaledDamage(float eventDamage, float coefficient, float minScaled)
		{
			return Mathf.Max(minScaled, eventDamage * coefficient);
		}
	}

	/// <summary>
	/// How proc spawn damage is derived from <see cref="AttackEntityData"/> vs the triggering combat event.
	/// </summary>
	public enum CombatReactionSpawnDamageMode : byte
	{
		/// <summary>Rolled damage and crit from template + spell modifications.</summary>
		StandaloneAttackTemplate = 0,

		/// <summary>Template defines shape/behaviors; magnitude scales from event damage × coefficient.</summary>
		ScaleByTriggeringHitDamage = 1,
	}

	/// <summary>
	/// Value-only bake of <see cref="CombatAttackSpawnReaction"/> for hot-path spawn logic (no UnityEngine.Object refs).
	/// </summary>
	public struct CombatAttackSpawnReactionRuntime
	{
		public CombatReactionTrigger trigger;
		public StatusAilmentFlag ailmentMaskFilter;

		/// <summary>SpellAuthoringData.GetInstanceID() when filtering by asset; 0 = match any spell.</summary>
		public int spellDefinitionInstanceIdFilter;

		public float2 spawnOffsetWorld;
		public SpellAttributeMask modificationMask;
		public CombatReactionSpawnDamageMode damageMode;
		public float eventDamageCoefficient;
		public float minScaledDamage;
	}
}
