using System;
using Unity.Mathematics;
using UnityEngine;

namespace BridgeOfBlood.Effects
{
	/// <summary>
	/// Unity-serializable copy of an <see cref="AttackEntitySpawnPayload"/> for item combat reactions.
	/// Chain / rehit use serializable config only; runtime lists are created fresh in <see cref="ToAttackEntitySpawnPayload"/>.
	/// </summary>
	[Serializable]
	public struct CombatBakedSpawnPayload
	{
		public float physicalDamage;
		public float coldDamage;
		public float fireDamage;
		public float lightningDamage;
		public float critChance;
		public float critDamageMultiplier;
		public float knockbackStrength;
		public float2 velocity;
		public HitBoxData hitBoxData;
		public PiercePolicyRuntime pierce;
		public ExpirationPolicyRuntime expiration;
		public CombatBakedChainPolicy chain;
		public float rehitCooldownSeconds;
		public FrozenApplierRuntime frozenApplier;
		public IgnitedApplierRuntime ignitedApplier;
		public ShockedApplierRuntime shockedApplier;
		public PoisonedApplierRuntime poisonedApplier;
		public StunnedApplierRuntime stunnedApplier;
		public BleedApplierRuntime bleedApplier;
		public EntityVisual visual;
		public AudioUnitRuntime onDamageSound;
		public EffectSpriteConfigRuntime onHitEffect;
		public EffectSpriteConfigRuntime onKillEffect;

		public AttackEntitySpawnPayload ToAttackEntitySpawnPayload(int spellId, int spellInvocationId)
		{
			var rehit = RehitPolicyRuntime.Default();
			rehit.rehitCooldownSeconds = rehitCooldownSeconds;

			return new AttackEntitySpawnPayload
			{
				physicalDamage = physicalDamage,
				coldDamage = coldDamage,
				fireDamage = fireDamage,
				lightningDamage = lightningDamage,
				critChance = critChance,
				critDamageMultiplier = critDamageMultiplier,
				knockbackStrength = knockbackStrength,
				velocity = velocity,
				hitBoxData = hitBoxData,
				pierce = pierce,
				expiration = expiration,
				chain = chain.ToRuntime(),
				rehit = rehit,
				frozenApplier = frozenApplier,
				ignitedApplier = ignitedApplier,
				shockedApplier = shockedApplier,
				poisonedApplier = poisonedApplier,
				stunnedApplier = stunnedApplier,
				bleedApplier = bleedApplier,
				visual = visual,
				onDamageSound = onDamageSound,
				onHitEffect = onHitEffect,
				onKillEffect = onKillEffect,
				spellId = spellId,
				spellInvocationId = spellInvocationId
			};
		}
	}

	/// <summary>
	/// Serializable chain policy for baked spawns (no <see cref="FixedList32Bytes{T}"/> state).
	/// </summary>
	[Serializable]
	public struct CombatBakedChainPolicy
	{
		public bool isActive;
		public int chainCount;
		public float chainRange;
		public ChainTargetSelect targetSelect;
		public bool excludePreviouslyHit;
		public bool enabled;

		public ChainPolicyRuntime ToRuntime()
		{
			return new ChainPolicyRuntime
			{
				isActive = isActive,
				chainCount = chainCount,
				chainRange = chainRange,
				targetSelect = targetSelect,
				excludePreviouslyHit = excludePreviouslyHit,
				enabled = enabled,
				chainHitsSoFar = 0
			};
		}
	}
}
