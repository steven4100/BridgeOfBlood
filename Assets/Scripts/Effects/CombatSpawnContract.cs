using BridgeOfBlood.Data.Spells;
using Unity.Mathematics;

namespace BridgeOfBlood.Effects
{
	/// <summary>
	/// Managed item combat-reaction entry. Holds the attack template + the frame's spell modifications so the
	/// processor can build an <see cref="AttackEntityBuildContext"/> per matching event (no pre-rolled payload).
	/// Spell filtering: legacy id on <see cref="filters"/>; or at most one runtime spell id when using a definition filter.
	/// <para>Managed (not <c>NativeArray</c>) because it carries managed refs; the processor runs on the main thread.</para>
	/// </summary>
	public class CombatSpawnContract
	{
		public CombatAttackSpawnReactionRuntime filters;

		/// <summary>Attack template authoring data; rolled + modified at spawn.</summary>
		public AttackEntityData attackData;

		/// <summary>Frame spell modifications applied at spawn (may be null).</summary>
		public SpellModifications modifications;

		/// <summary>
		/// When <see cref="CombatAttackSpawnReactionRuntime.spellDefinitionInstanceIdFilter"/> is set: true if the spell loop had a matching slot (see <see cref="definitionFilterSpellId"/>); false means no slot matched.
		/// </summary>
		public bool definitionSpellResolved;

		/// <summary>
		/// When <see cref="definitionSpellResolved"/>, the single runtime spell id for that definition (first match in loop order).
		/// </summary>
		public int definitionFilterSpellId;

		/// <summary>
		/// Builds the spawn context for one matching event. <paramref name="eventScaledDamage"/> &gt; 0 scales total
		/// damage to that value (ScaleByTriggeringHitDamage); &lt;= 0 leaves rolled damage untouched.
		/// </summary>
		public AttackEntityBuildContext BuildContext(int spellId, int spellInvocationId, float2 position, float eventScaledDamage)
		{
			float2 velocity = new float2(attackData.entityVelocity.x, attackData.entityVelocity.y);
			return new AttackEntityBuildContext(
				attackData, spellId, spellInvocationId, 0,
				modifications, filters.modificationMask, position, velocity, eventScaledDamage);
		}
	}
}
