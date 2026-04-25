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

	public struct EnemyKilledEvent
	{
		public int enemyEntityId;
		public int spellId;
		public int spellInvocationId;
		public float overkillDamage;
		public EnemyCorruptionFlag corruptionFlag;
		public StatusAilmentFlag finalStatusAilments;
	}
}
