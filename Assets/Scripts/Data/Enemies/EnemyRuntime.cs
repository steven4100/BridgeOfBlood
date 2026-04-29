using BridgeOfBlood.Data.Shared;
using Unity.Collections;
using Unity.Mathematics;

namespace BridgeOfBlood.Data.Enemies
{
	/// <summary>Position and horizontal motion; hot path for movement and spatial queries.</summary>
	public struct EnemyMotion
	{
		public float2 position;
		public float moveSpeed;
		public float2 knockbackVelocity;
	}

	/// <summary>Health pool; hot path for damage and death checks.</summary>
	public struct EnemyVitality
	{
		public float health;
		public float maxHealth;
	}

	/// <summary>Damage resolution metadata.</summary>
	public struct EnemyCombatTraits
	{
		public EnemyCorruptionFlag corruptionFlag;
		public DamageType elementalWeakness;
	}

	/// <summary>Flipbook and DoT flash presentation.</summary>
	public struct EnemyPresentation
	{
		public EntityVisual visual;
		public AudioUnitRuntime onDeathSound;
		public float visualTime;
		public float ailmentFlashTimer;
		public TickDamageSource ailmentFlashSource;
	}

	/// <summary>Parallel column views into <see cref="EnemyManager"/> storage; valid until next list mutation.</summary>
	public struct EnemyBuffers
	{
		public NativeArray<EnemyMotion> Motion;
		public NativeArray<EnemyVitality> Vitality;
		public NativeArray<int> EntityIds;
		public NativeArray<EnemyCombatTraits> CombatTraits;
		public NativeArray<StatusAilmentFlag> Status;
		public NativeArray<EnemyPresentation> Presentation;

		public EnemyBuffers(
			NativeArray<EnemyMotion> motion,
			NativeArray<EnemyVitality> vitality,
			NativeArray<int> entityIds,
			NativeArray<EnemyCombatTraits> combatTraits,
			NativeArray<StatusAilmentFlag> status,
			NativeArray<EnemyPresentation> presentation)
		{
			Motion = motion;
			Vitality = vitality;
			EntityIds = entityIds;
			CombatTraits = combatTraits;
			Status = status;
			Presentation = presentation;
		}

		public int Length => Motion.Length;
	}

	public struct EnemyBleedStatus
	{
		public int entityID;
		public int spellId;
		public int spellInvocationId;
		public float timeApplied;
		public float lifetime;
		public float damagerPerTick;
		public float lastTimeTicked;
	}

	public struct EnemyPoisonStatus
	{
		public int entityID;
		public int spellId;
		public int spellInvocationId;
		public float timeApplied;
		public float lifetime;
		public float damagerPerTick;
		public float lastTimeTicked;
	}

	public struct EnemyIgniteStatus
	{
		public int entityID;
		public int spellId;
		public int spellInvocationId;
		public float timeApplied;
		public float lifetime;
		public float damagerPerTick;
		public float lastTimeTicked;
	}

	public struct EnemyFrozenStatus
	{
		public int entityID;
		public int spellId;
		public int spellInvocationId;
		public float timeApplied;
		public float lifetime;
	}

	public struct EnemyStunnedStatus
	{
		public int entityID;
		public int spellId;
		public int spellInvocationId;
		public float timeApplied;
		public float lifetime;
	}

	public struct EnemyShockedStatus
	{
		public int entityID;
		public int spellId;
		public int spellInvocationId;
		public float timeApplied;
		public float lifetime;
		public float damagerMultiplier;
	}

	public struct EnemyHitEvent
	{
		public int enemyEntityId;
		public int spellId;
		public int spellInvocationId;
		public StatusAilmentFlag statusAilmentsApplied;
		public float damageDealt;
		public DamageType damageType;
	}

	/// <summary>Immutable combat context for item/reaction code. Ailment paths resolve this from live enemy column views when combat reactions run, not in the ailment application hot path.</summary>
	public struct EnemyCombatSnapshot
	{
		public float maxHealth;
		public float health;
		public EnemyCombatTraits traits;
	}

	public struct EnemyKilledEvent
	{
		public int enemyEntityId;
		public int spellId;
		public int spellInvocationId;
		public float2 position;
		public float overkillDamage;
		public AudioUnitRuntime onDeathSound;
		public StatusAilmentFlag finalStatusAilments;
	}

	public static class EnemyCombatSnapshotUtil
	{
		public static EnemyCombatSnapshot FromEnemyIndex(
			int enemyIndex,
			NativeArray<EnemyVitality> vitality,
			NativeArray<EnemyCombatTraits> combatTraits)
		{
			return new EnemyCombatSnapshot
			{
				maxHealth = vitality[enemyIndex].maxHealth,
				health = vitality[enemyIndex].health,
				traits = combatTraits[enemyIndex],
			};
		}

		public static EnemyCombatSnapshot FromBuffers(in EnemyBuffers enemies, int enemyIndex) =>
			FromEnemyIndex(enemyIndex, enemies.Vitality, enemies.CombatTraits);
	}
}
