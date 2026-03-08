using BridgeOfBlood.Data.Shared;
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

