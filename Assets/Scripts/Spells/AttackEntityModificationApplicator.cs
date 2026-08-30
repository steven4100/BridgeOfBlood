using BridgeOfBlood.Data.Shared;
using Unity.Mathematics;
using UnityEngine;
using EntityId = BridgeOfBlood.Data.Shared.EntityId;

namespace BridgeOfBlood.Data.Spells
{
	/// <summary>
	/// Shared stat math for attack entities. Rolls authoring ranges (with spell modifications baked into the
	/// ranges first), and applies per-property <see cref="ResolvedModifier"/>s to live damage/crit values.
	/// Used at spawn time (parameter modifiers) and at hit time (predicate conditionals) so the two paths
	/// share one set of formulas. No <c>Object.Instantiate</c> of authoring data.
	/// </summary>
	public static class AttackEntityModificationApplicator
	{
		/// <summary>
		/// Fills identity, transform, visual, and audio. Damage, crit, hit box, and knockback are written
		/// by authoring behaviors in <see cref="AttackEntityManager.Spawn"/>.
		/// </summary>
		public static AttackEntity BuildRolledEntity(in AttackEntityBuildContext ctx, EntityId entityId)
		{
			AttackEntityData data = ctx.data;

			uint seed = AttackEntityBuildRngSeed.Mix(ctx.spellId, ctx.spellInvocationId, ctx.keyframeIndex, data.GetInstanceID());
			uint visualSeed = seed ^ 0x9E3779B9u;
			uint audioSeed = seed ^ 0x7F4A7C15u;

			return new AttackEntity
			{
				entityId = entityId,
				position = ctx.position,
				velocity = ctx.velocity,
				timeAlive = 0f,
				framesAlive = 0,
				distanceTravelled = 0f,
				enemiesHit = 0,
				rehitCooldownSeconds = data.rehitCooldownSeconds,
				visual = data.visual != null ? data.visual.Resolve(visualSeed) : EntityVisual.None,
				onDamageSound = data.onDamageSound != null ? data.onDamageSound.ToRuntime(audioSeed) : AudioUnitRuntime.None,
				onHitEffect = EffectSpriteConfigRuntime.Default(),
				onKillEffect = EffectSpriteConfigRuntime.Default(),
				spellId = ctx.spellId,
				spellInvocationId = ctx.spellInvocationId
			};
		}

		/// <summary>
		/// Scales all damage types so their total equals <paramref name="scaledTotal"/>, preserving the per-type ratio.
		/// Used by combat reactions in <see cref="CombatReactionSpawnDamageMode.ScaleByTriggeringHitDamage"/>.
		/// </summary>
		public static void ApplyEventScaledDamage(
			ref PhysicalDamageRuntime physical,
			ref ColdDamageRuntime cold,
			ref FireDamageRuntime fire,
			ref LightningDamageRuntime lightning,
			float scaledTotal)
		{
			float sum = physical.amount + cold.amount + fire.amount + lightning.amount;
			if (sum <= 0f) return;
			float factor = scaledTotal / sum;
			physical.amount *= factor;
			cold.amount *= factor;
			fire.amount *= factor;
			lightning.amount *= factor;
		}

		/// <summary>
		/// Applies a single resolved modifier (by property) to live damage/crit scalars. Shared by spawn-time
		/// parameter modifiers and hit-time conditional modifiers. Non-damage/crit properties are no-ops here
		/// (chains/pierce/aoe/knockback are resolved into policies at spawn, not damage at hit time).
		/// </summary>
		public static void Apply(
			SpellModificationProperty prop,
			in ResolvedModifier mod,
			ref float physical,
			ref float cold,
			ref float fire,
			ref float lightning,
			ref float critChance,
			ref float critMult)
		{
			float mult = mod.Multiplier;
			switch (prop)
			{
				case SpellModificationProperty.DamageScaling:
					physical = Mathf.Max(0f, (physical + mod.flat) * mult);
					cold = Mathf.Max(0f, (cold + mod.flat) * mult);
					fire = Mathf.Max(0f, (fire + mod.flat) * mult);
					lightning = Mathf.Max(0f, (lightning + mod.flat) * mult);
					break;
				case SpellModificationProperty.PhysicalDamageScaling:
					physical = Mathf.Max(0f, (physical + mod.flat) * mult);
					break;
				case SpellModificationProperty.ColdDamageScaling:
					cold = Mathf.Max(0f, (cold + mod.flat) * mult);
					break;
				case SpellModificationProperty.FireDamageScaling:
					fire = Mathf.Max(0f, (fire + mod.flat) * mult);
					break;
				case SpellModificationProperty.LightningDamageScaling:
					lightning = Mathf.Max(0f, (lightning + mod.flat) * mult);
					break;
				case SpellModificationProperty.CritChance:
					critChance = Mathf.Clamp01(critChance * mult + mod.flat / 100f);
					break;
				case SpellModificationProperty.CritMult:
					critMult = Mathf.Max(1f, critMult * mult + mod.flat / 100f);
					break;
				default:
					break;
			}
		}

		public static FloatRange ResolveDamageRange(
			FloatRange source,
			SpellModificationProperty typeProperty,
			SpellModifications mods,
			SpellAttributeMask mask)
		{
			if (mods == null)
				return source;

			var dmgScaling = SpellModificationsApplicator.Resolve(mods, SpellModificationProperty.DamageScaling, mask);
			var typeMod = SpellModificationsApplicator.Resolve(mods, typeProperty, mask);
			return ApplyDamageRange(source, typeMod, dmgScaling);
		}

		/// <summary>
		/// Resolves mod-adjusted damage ranges from authoring behaviors (pre-roll). Shared by spawn-time
		/// rolling and by spell preview renderers so previews report the same numbers the simulation will roll from.
		/// </summary>
		public static void ResolveDamageRanges(
			AttackEntityData data,
			SpellModifications mods,
			SpellAttributeMask mask,
			out FloatRange physical,
			out FloatRange cold,
			out FloatRange fire,
			out FloatRange lightning)
		{
			physical = ResolveBehaviorDamage<PhysicalDamageBehavior>(data, SpellModificationProperty.PhysicalDamageScaling, mods, mask);
			cold = ResolveBehaviorDamage<ColdDamageBehavior>(data, SpellModificationProperty.ColdDamageScaling, mods, mask);
			fire = ResolveBehaviorDamage<FireDamageBehavior>(data, SpellModificationProperty.FireDamageScaling, mods, mask);
			lightning = ResolveBehaviorDamage<LightningDamageBehavior>(data, SpellModificationProperty.LightningDamageScaling, mods, mask);
		}

		static FloatRange ResolveBehaviorDamage<T>(
			AttackEntityData data,
			SpellModificationProperty typeProperty,
			SpellModifications mods,
			SpellAttributeMask mask) where T : DamageBehavior
		{
			T behavior = data.GetBehavior<T>();
			if (behavior == null)
				return default;
			return ResolveDamageRange(behavior.damageRange, typeProperty, mods, mask);
		}

		/// <summary>
		/// Applies AreaOfEffect modifications to an authoring hitbox. Shared by spawn-time entity building and
		/// by spell preview renderers so AoE previews match the hitbox the simulation will use.
		/// </summary>
		public static HitBoxData ResolveHitBox(HitBoxData hitBox, SpellModifications mods, SpellAttributeMask mask)
		{
			if (mods == null)
				return hitBox;

			var aoe = SpellModificationsApplicator.Resolve(mods, SpellModificationProperty.AreaOfEffect, mask);
			if (hitBox.isSphere) hitBox.sphereRadius = hitBox.sphereRadius * aoe.Multiplier + aoe.flat;
			if (hitBox.isRect) hitBox.rectDimension = hitBox.rectDimension * aoe.Multiplier + new Vector2(aoe.flat, aoe.flat);
			return hitBox;
		}

		public static HitBoxData ResolveHitBox(AttackEntityData data, SpellModifications mods, SpellAttributeMask mask)
		{
			if (data == null)
				return default;
			HitBoxBehavior behavior = data.GetBehavior<HitBoxBehavior>();
			if (behavior == null)
				return default;
			return ResolveHitBox(behavior.hitBoxData, mods, mask);
		}

		public static FloatRange ApplyDamageRange(FloatRange source, ResolvedModifier typeMod, ResolvedModifier dmgScaling)
		{
			float mult = typeMod.Multiplier * dmgScaling.Multiplier;
			var r = new FloatRange
			{
				min = Mathf.Max(0f, (source.min + typeMod.flat) * mult),
				max = Mathf.Max(0f, (source.max + typeMod.flat) * mult)
			};
			r.ClampOrder();
			return r;
		}
	}
}
