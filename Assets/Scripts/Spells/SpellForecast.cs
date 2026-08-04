using System;
using BridgeOfBlood.Data.Shared;
using UnityEngine;

namespace BridgeOfBlood.Data.Spells
{
	/// <summary>
	/// Resolved "what would this spell do if cast right now" snapshot for one <see cref="RuntimeSpell"/> slot,
	/// built against a frame's <see cref="SpellModifications"/>. Drives diegetic previews (spine counts, AoE
	/// scale) and cast-animation sync (cast durations, attack entity spawn time).
	///
	/// Spells currently have a single cast emission, so this is flat rather than a per-keyframe list.
	/// </summary>
	public struct SpellCastForecast : IEquatable<SpellCastForecast>
	{
		public int spellId;

		/// <summary>Presentation cast wind-up, from <see cref="SpellAuthoringData.castTime"/>.</summary>
		public float castTime;

		/// <summary>Seconds before the next spell in the loop may cast, from <see cref="SpellAuthoringData.castCompletionDuration"/>.</summary>
		public float castCompletionDuration;

		/// <summary>Seconds from cast start until attack entities spawn, from the spell's <see cref="SpellKeyFrame.time"/>.</summary>
		public float spawnTime;

		/// <summary>
		/// Where entities spawn relative to the player, from the emitter's <see cref="RelativeToPlayerSpawnCriteria"/>.
		/// Cast origins are always the player position, so presentation must add the player position to place a preview.
		/// </summary>
		public Vector2 originOffset;

		/// <summary>Attack entities emitted, after Projectiles modifications.</summary>
		public int emitCount;

		public float spreadDegrees;

		/// <summary>Seconds over which the <see cref="emitCount"/> entities spawn (0 = all at <see cref="spawnTime"/>).</summary>
		public float emitDuration;

		public float speed;

		/// <summary>Hitbox after AreaOfEffect modifications.</summary>
		public HitBoxData hitBox;

		public FloatRange physicalDamage;
		public FloatRange coldDamage;
		public FloatRange fireDamage;
		public FloatRange lightningDamage;

		public bool Equals(SpellCastForecast other)
		{
			return spellId == other.spellId
				&& castTime == other.castTime
				&& castCompletionDuration == other.castCompletionDuration
				&& spawnTime == other.spawnTime
				&& originOffset == other.originOffset
				&& emitCount == other.emitCount
				&& spreadDegrees == other.spreadDegrees
				&& emitDuration == other.emitDuration
				&& speed == other.speed
				&& HitBoxEquals(hitBox, other.hitBox)
				&& RangeEquals(physicalDamage, other.physicalDamage)
				&& RangeEquals(coldDamage, other.coldDamage)
				&& RangeEquals(fireDamage, other.fireDamage)
				&& RangeEquals(lightningDamage, other.lightningDamage);
		}

		public override bool Equals(object obj) => obj is SpellCastForecast other && Equals(other);

		public override int GetHashCode()
		{
			var hash = new HashCode();
			hash.Add(spellId);
			hash.Add(castTime);
			hash.Add(castCompletionDuration);
			hash.Add(spawnTime);
			hash.Add(originOffset);
			hash.Add(emitCount);
			hash.Add(spreadDegrees);
			hash.Add(emitDuration);
			hash.Add(speed);
			hash.Add(hitBox.isSphere);
			hash.Add(hitBox.isRect);
			hash.Add(hitBox.sphereRadius);
			hash.Add(hitBox.rectDimension);
			hash.Add(hitBox.scaleGrowthRate);
			hash.Add(physicalDamage.min);
			hash.Add(physicalDamage.max);
			hash.Add(coldDamage.min);
			hash.Add(coldDamage.max);
			hash.Add(fireDamage.min);
			hash.Add(fireDamage.max);
			hash.Add(lightningDamage.min);
			hash.Add(lightningDamage.max);
			return hash.ToHashCode();
		}

		static bool HitBoxEquals(in HitBoxData a, in HitBoxData b)
		{
			return a.isSphere == b.isSphere
				&& a.isRect == b.isRect
				&& a.sphereRadius == b.sphereRadius
				&& a.rectDimension == b.rectDimension
				&& a.scaleGrowthRate == b.scaleGrowthRate;
		}

		static bool RangeEquals(in FloatRange a, in FloatRange b) => a.min == b.min && a.max == b.max;
	}
}
