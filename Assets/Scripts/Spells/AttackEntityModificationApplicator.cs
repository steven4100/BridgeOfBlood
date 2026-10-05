using System;
using BridgeOfBlood.Data.Shared;
using Unity.Mathematics;
using UnityEngine;
using EntityId = BridgeOfBlood.Data.Shared.EntityId;

namespace BridgeOfBlood.Data.Spells
{
	/// <summary>
	/// Shared stat math for attack entities. Rolls authoring ranges (with spell modifications baked into the
	/// ranges first), and applies per-property <see cref="ResolvedModifier"/>s to live damage/crit values.
		/// Used at spawn time (parameter modifiers) and at hit time (ConditionalHitModifier) so the two paths
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
				direction = ctx.direction,
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
		/// parameter modifiers and hit-time ConditionalHitModifier. Non-damage/crit properties are no-ops here
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

		public static FloatRange ResolveDamageRange<TModification>(
			FloatRange source,
			ModificationSet mods,
			SpellAttributeMask mask,
			Func<TModification, ParameterModification> getParameters,
			Func<TModification, SpellAttributeMask> getFilter)
			where TModification : IModificationData
		{
			if (mods == null)
				return source;

			var dmgScaling = ParameterModificationMerger.Merge(
				mods.Get<DamageScalingModificationData>(),
				modification => modification.parameters,
				modification => modification.filter,
				mask);
			var typeMod = ParameterModificationMerger.Merge(
				mods.Get<TModification>(), getParameters, getFilter, mask);
			return ApplyDamageRange(source, typeMod, dmgScaling);
		}

		/// <summary>
		/// Resolves mod-adjusted damage ranges from authoring behaviors (pre-roll). Shared by spawn-time
		/// rolling and by spell preview renderers so previews report the same numbers the simulation will roll from.
		/// </summary>
		public static void ResolveDamageRanges(
			AttackEntityData data,
			ModificationSet mods,
			SpellAttributeMask mask,
			out FloatRange physical,
			out FloatRange cold,
			out FloatRange fire,
			out FloatRange lightning)
		{
			physical = ResolveBehaviorDamage<PhysicalDamageBehavior, PhysicalDamageScalingModificationData>(
				data, mods, mask, modification => modification.parameters, modification => modification.filter);
			cold = ResolveBehaviorDamage<ColdDamageBehavior, ColdDamageScalingModificationData>(
				data, mods, mask, modification => modification.parameters, modification => modification.filter);
			fire = ResolveBehaviorDamage<FireDamageBehavior, FireDamageScalingModificationData>(
				data, mods, mask, modification => modification.parameters, modification => modification.filter);
			lightning = ResolveBehaviorDamage<LightningDamageBehavior, LightningDamageScalingModificationData>(
				data, mods, mask, modification => modification.parameters, modification => modification.filter);
		}

		static FloatRange ResolveBehaviorDamage<TBehavior, TModification>(
			AttackEntityData data,
			ModificationSet mods,
			SpellAttributeMask mask,
			Func<TModification, ParameterModification> getParameters,
			Func<TModification, SpellAttributeMask> getFilter)
			where TBehavior : DamageBehavior
			where TModification : IModificationData
		{
			TBehavior behavior = data.GetBehavior<TBehavior>();
			if (behavior == null)
				return default;
			return ResolveDamageRange(
				behavior.damageRange, mods, mask, getParameters, getFilter);
		}

		/// <summary>
		/// Applies AreaOfEffect modifications to an authoring hitbox. Shared by spawn-time entity building and
		/// by spell preview renderers so AoE previews match the hitbox the simulation will use.
		/// </summary>
		public static HitBoxData ResolveHitBox(HitBoxData hitBox, ModificationSet mods, SpellAttributeMask mask)
		{
			if (mods == null)
				return hitBox;

			var aoe = ParameterModificationMerger.Merge(
				mods.Get<AreaOfEffectModificationData>(),
				modification => modification.parameters,
				modification => modification.filter,
				mask);
			if (hitBox.isSphere) hitBox.sphereRadius = hitBox.sphereRadius * aoe.Multiplier + aoe.flat;
			if (hitBox.isRect) hitBox.rectDimension = hitBox.rectDimension * aoe.Multiplier + new Vector2(aoe.flat, aoe.flat);
			return hitBox;
		}

		public static HitBoxData ResolveHitBox(AttackEntityData data, ModificationSet mods, SpellAttributeMask mask)
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
