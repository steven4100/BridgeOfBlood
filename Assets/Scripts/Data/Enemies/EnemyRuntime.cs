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

	/// <summary>Circle used by attack overlap. Zero radius is a point.</summary>
	public struct EnemyHurtbox
	{
		public float radius;
	}

	/// <summary>Per-slot identity. Generation rejects stale <see cref="EntityId"/> handles after reuse.</summary>
	public struct EnemyId
	{
		public uint Generation;
		public byte IsAlive;
		public byte IsMapObject;
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
		public NativeArray<EnemyId> Ids;
		public NativeArray<EnemyHurtbox> Hurtboxes;
		public NativeArray<EnemyCombatTraits> CombatTraits;
		public NativeArray<StatusAilmentFlag> Status;
		public NativeArray<EnemyPresentation> Presentation;
		public int AliveCount;

		public EnemyBuffers(
			NativeArray<EnemyMotion> motion,
			NativeArray<EnemyVitality> vitality,
			NativeArray<EnemyId> ids,
			NativeArray<EnemyHurtbox> hurtboxes,
			NativeArray<EnemyCombatTraits> combatTraits,
			NativeArray<StatusAilmentFlag> status,
			NativeArray<EnemyPresentation> presentation,
			int aliveCount)
		{
			Motion = motion;
			Vitality = vitality;
			Ids = ids;
			Hurtboxes = hurtboxes;
			CombatTraits = combatTraits;
			Status = status;
			Presentation = presentation;
			AliveCount = aliveCount;
		}

		public int SlotCount => Motion.Length;
		public int Length => SlotCount;

		public bool IsLive(int index) =>
			index >= 0 && index < Ids.Length && Ids[index].IsAlive != 0;

		public bool IsMapObject(int index) =>
			IsLive(index) && Ids[index].IsMapObject != 0;

		public bool IsValid(EntityId id) =>
			id.Index >= 0
			&& id.Index < Ids.Length
			&& Ids[id.Index].IsAlive != 0
			&& Ids[id.Index].Generation == id.Generation;

		public EntityId GetEntityId(int index) => new EntityId
		{
			Index = index,
			Generation = Ids[index].Generation
		};
	}

	public struct EnemyBleedStatus
	{
		public EntityId enemyId;
		public int spellId;
		public int spellInvocationId;
		public float timeApplied;
		public float lifetime;
		public float damagerPerTick;
		public float lastTimeTicked;
	}

	public struct EnemyPoisonStatus
	{
		public EntityId enemyId;
		public int spellId;
		public int spellInvocationId;
		public float timeApplied;
		public float lifetime;
		public float damagerPerTick;
		public float lastTimeTicked;
	}

	public struct EnemyIgniteStatus
	{
		public EntityId enemyId;
		public int spellId;
		public int spellInvocationId;
		public float timeApplied;
		public float lifetime;
		public float damagerPerTick;
		public float lastTimeTicked;
	}

	public struct EnemyFrozenStatus
	{
		public EntityId enemyId;
		public int spellId;
		public int spellInvocationId;
		public float timeApplied;
		public float lifetime;
	}

	public struct EnemyStunnedStatus
	{
		public EntityId enemyId;
		public int spellId;
		public int spellInvocationId;
		public float timeApplied;
		public float lifetime;
	}

	public struct EnemyShockedStatus
	{
		public EntityId enemyId;
		public int spellId;
		public int spellInvocationId;
		public float timeApplied;
		public float lifetime;
		public float damagerMultiplier;
	}

	public struct EnemyHitEvent
	{
		public EntityId enemyEntityId;
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
		public EntityId enemyEntityId;
		public int spellId;
		public int spellInvocationId;
		public float2 position;
		/// <summary>Damage from the hit or tick that killed the enemy (before overkill). Used for proc scaling.</summary>
		public float killingBlowDamage;
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
