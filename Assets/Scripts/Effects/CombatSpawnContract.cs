using Unity.Mathematics;

namespace BridgeOfBlood.Effects
{
	/// <summary>
	/// Output from <see cref="CombatReactionProcessor"/> — consumption calls <see cref="AttackEntityManager.Spawn"/>.
	/// </summary>
	public struct CombatReactionSpawnRequest
	{
		public AttackEntitySpawnPayload payload;
		public float2 origin;
	}

	/// <summary>
	/// Baked item combat-reaction entry for struct-only processing.
	/// Spell filtering: legacy id on <see cref="filters"/>; or at most one runtime spell id when using a definition filter.
	/// </summary>
	public struct CombatSpawnContract
	{
		public CombatAttackSpawnReactionRuntime filters;
		public AttackEntitySpawnPayload templatePayload;

		/// <summary>
		/// When <see cref="CombatAttackSpawnReactionRuntime.spellDefinitionInstanceIdFilter"/> is set: true if the spell loop had a matching slot (see <see cref="definitionFilterSpellId"/>); false means no slot matched.
		/// </summary>
		public bool definitionSpellResolved;

		/// <summary>
		/// When <see cref="definitionSpellResolved"/>, the single runtime spell id for that definition (first match in loop order).
		/// </summary>
		public int definitionFilterSpellId;
	}
}
