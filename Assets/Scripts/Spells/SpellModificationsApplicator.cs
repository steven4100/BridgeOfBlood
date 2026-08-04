using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using UnityEngine;

namespace BridgeOfBlood.Data.Spells
{
	[System.Serializable]
	public struct ResolvedModifier
	{
		public float flat;
		public float percentIncreased;
		public float moreCombined;

		public float Multiplier => (1f + percentIncreased / 100f) * moreCombined;

		public static readonly ResolvedModifier Identity = new() { moreCombined = 1f };
	}

	public static class SpellModificationsApplicator
	{
		public static ResolvedModifier Resolve(SpellModifications mods, SpellModificationProperty prop, SpellAttributeMask mask)
		{
			if (!mods.modifiers.TryGetValue(prop, out var list))
				return ResolvedModifier.Identity;

			float flat = 0f, pct = 0f, more = 1f;
			foreach (var m in list)
			{
				if (m.filter != SpellAttributeMask.None && (mask & m.filter) == 0) continue;
				flat += m.GetFlat();
				pct += m.GetPercent();
				float mv = m.GetMore();
				if (mv != 0f) more *= (1f + mv / 100f);
			}
			return new ResolvedModifier { flat = flat, percentIncreased = pct, moreCombined = more };
		}

		/// <summary>
		/// Applies Projectiles modifications to an emitter's base count. Shared by the emission handler (spawn)
		/// and the spell forecast (preview) so both report the same number.
		/// </summary>
		public static int ResolveEmitCount(SpellModifications mods, int baseCount, SpellAttributeMask mask)
		{
			if (mods != null)
			{
				ResolvedModifier resolved = Resolve(mods, SpellModificationProperty.Projectiles, mask);
				baseCount = Mathf.Max(1, (int)(baseCount * resolved.Multiplier) + (int)resolved.flat);
			}
			return baseCount < 1 ? 1 : baseCount;
		}
	}
}
