using BridgeOfBlood.Data.Shared;
using Unity.Mathematics;
using UnityEngine;

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
		/// Rolls a fresh <see cref="AttackEntity"/> from <paramref name="ctx"/>: resolves mod-adjusted ranges,
		/// rolls them deterministically (seed from spell/keyframe/data id), and fills scalar fields.
		/// Policies (chain/pierce/appliers) and effect scalars are applied separately by behaviors.
		/// </summary>
		public static AttackEntity BuildRolledEntity(in AttackEntityBuildContext ctx, int entityId)
		{
			AttackEntityData data = ctx.data;
			SpellModifications mods = ctx.modifications;
			SpellAttributeMask mask = ctx.attributeMask;

			uint seed = AttackEntityBuildRngSeed.Mix(ctx.spellId, ctx.spellInvocationId, ctx.keyframeIndex, data.GetInstanceID());
			var rng = Unity.Mathematics.Random.CreateFromIndex(seed);

			FloatRange physR = data.physicalDamageRange;
			FloatRange coldR = data.coldDamageRange;
			FloatRange fireR = data.fireDamageRange;
			FloatRange ltngR = data.lightningDamageRange;
			FloatRange critChanceR = data.critChanceRange;
			FloatRange critMultR = data.critDamageMultiplierRange;
			HitBoxData hitBox = data.hitBoxData;
			float knockback = Mathf.Max(0f, data.knockbackStrength);

			if (mods != null)
			{
				var dmgScaling = SpellModificationsApplicator.Resolve(mods, SpellModificationProperty.DamageScaling, mask);
				var typePhys = SpellModificationsApplicator.Resolve(mods, SpellModificationProperty.PhysicalDamageScaling, mask);
				var typeCold = SpellModificationsApplicator.Resolve(mods, SpellModificationProperty.ColdDamageScaling, mask);
				var typeFire = SpellModificationsApplicator.Resolve(mods, SpellModificationProperty.FireDamageScaling, mask);
				var typeLtng = SpellModificationsApplicator.Resolve(mods, SpellModificationProperty.LightningDamageScaling, mask);

				physR = ApplyDamageRange(data.physicalDamageRange, typePhys, dmgScaling);
				coldR = ApplyDamageRange(data.coldDamageRange, typeCold, dmgScaling);
				fireR = ApplyDamageRange(data.fireDamageRange, typeFire, dmgScaling);
				ltngR = ApplyDamageRange(data.lightningDamageRange, typeLtng, dmgScaling);

				var critChance = SpellModificationsApplicator.Resolve(mods, SpellModificationProperty.CritChance, mask);
				critChanceR = new FloatRange
				{
					min = Mathf.Clamp01(data.critChanceRange.min * critChance.Multiplier + critChance.flat / 100f),
					max = Mathf.Clamp01(data.critChanceRange.max * critChance.Multiplier + critChance.flat / 100f)
				};
				critChanceR.ClampOrder();

				var critMult = SpellModificationsApplicator.Resolve(mods, SpellModificationProperty.CritMult, mask);
				critMultR = new FloatRange
				{
					min = Mathf.Max(1f, data.critDamageMultiplierRange.min * critMult.Multiplier + critMult.flat / 100f),
					max = Mathf.Max(1f, data.critDamageMultiplierRange.max * critMult.Multiplier + critMult.flat / 100f)
				};
				critMultR.ClampOrder();

				var aoe = SpellModificationsApplicator.Resolve(mods, SpellModificationProperty.AreaOfEffect, mask);
				if (hitBox.isSphere) hitBox.sphereRadius = hitBox.sphereRadius * aoe.Multiplier + aoe.flat;
				if (hitBox.isRect) hitBox.rectDimension = hitBox.rectDimension * aoe.Multiplier + new Vector2(aoe.flat, aoe.flat);

				var knock = SpellModificationsApplicator.Resolve(mods, SpellModificationProperty.KnockbackStrength, mask);
				knockback = Mathf.Max(0f, data.knockbackStrength * knock.Multiplier + knock.flat);
			}

			float physicalDamage = physR.ResolveUniform(ref rng);
			float coldDamage = coldR.ResolveUniform(ref rng);
			float fireDamage = fireR.ResolveUniform(ref rng);
			float lightningDamage = ltngR.ResolveUniform(ref rng);
			float critChanceRolled = Mathf.Clamp01(critChanceR.ResolveUniform(ref rng));
			float critMultRolled = Mathf.Max(1f, critMultR.ResolveUniform(ref rng));

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
				physicalDamage = physicalDamage,
				coldDamage = coldDamage,
				fireDamage = fireDamage,
				lightningDamage = lightningDamage,
				critChance = critChanceRolled,
				critDamageMultiplier = critMultRolled,
				knockbackStrength = knockback,
				hitBox = hitBox,
				currentHitBoxScale = 1f,
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
		public static void ApplyEventScaledDamage(ref AttackEntity e, float scaledTotal)
		{
			float sum = e.physicalDamage + e.coldDamage + e.fireDamage + e.lightningDamage;
			if (sum <= 0f) return;
			float factor = scaledTotal / sum;
			e.physicalDamage *= factor;
			e.coldDamage *= factor;
			e.fireDamage *= factor;
			e.lightningDamage *= factor;
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

		static FloatRange ApplyDamageRange(FloatRange source, ResolvedModifier typeMod, ResolvedModifier dmgScaling)
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
