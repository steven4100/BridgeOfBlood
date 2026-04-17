using BridgeOfBlood.Data.Shared;
using Unity.Collections;
using Unity.Mathematics;

namespace BridgeOfBlood.Data.Enemies
{
	public struct Enemy
	{
		public float2 position;
		public float moveSpeed;
		public float health;
		public float maxHealth;
		public EnemyCorruptionFlag corruptionFlag;
		public DamageType elementalWeakness;
		public StatusAilmentFlag statusAilmentFlag;
		public int entityId;
		public EntityVisual visual;
		public float visualTime;
	}

	public struct EnemyBleedStatus
	{
		public int entityID;
		public int spellId;
		public int spellInvocationId;
		public float timeApplied;
		public float lifetime;
		public float damagerPerTick;
		public float damagePerUnitWalked;
	}

	public struct EnemyPoisonStatus
	{
		public int entityID;
		public int spellId;
		public int spellInvocationId;
		public float timeApplied;
		public float lifetime;
		public float damagerPerTick;
	}

	public struct EnemyIgniteStatus
	{
		public int entityID;
		public int spellId;
		public int spellInvocationId;
		public float timeApplied;
		public float lifetime;
		public float damagerPerTick;
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
		public int spellId; // Which spell caused the hit
		public int spellInvocationId; // Which invocation of that spell
		public StatusAilmentFlag statusAilmentsApplied;
		public float damageDealt;
		public DamageType damageType;
	}
	
	public struct EnemyKilledEvent
	{
		public int enemyEntityId;
		public int spellId; // Which spell caused the kill
		public int spellInvocationId; // Which invocation of that spell
		public float overkillDamage;
		public EnemyCorruptionFlag corruptionFlag;
		public StatusAilmentFlag finalStatusAilments;
	}
}

