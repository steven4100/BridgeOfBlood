using System.Collections.Generic;
using UnityEngine;

namespace BridgeOfBlood.Data.Spells
{
	[CreateAssetMenu(fileName = "SpellModificationsTest", menuName = "Bridge of Blood/Spell Modifications Test", order = 0)]
	public class SpellModificationsTestData : ScriptableObject
	{
		[Header("Modifiers (covers core params, damage scaling, penetration)")]
		public List<ParameterModifier> modifiers = new List<ParameterModifier>();

		[Header("Conversion")]
		public List<DamageConversion> conversions = new List<DamageConversion>();

		[Header("Extra damage as")]
		public List<ExtraDamageAs> extraDamageAs = new List<ExtraDamageAs>();

		public SpellModifications GetModifications()
		{
			var mods = new SpellModifications();

			if (modifiers != null)
			{
				for (int i = 0; i < modifiers.Count; i++)
				{
					if (modifiers[i] != null)
						mods.Add(modifiers[i].Clone());
				}
			}

			mods.conversions = conversions != null ? new List<DamageConversion>(conversions) : new List<DamageConversion>();
			mods.extraDamageAs = extraDamageAs != null ? new List<ExtraDamageAs>(extraDamageAs) : new List<ExtraDamageAs>();
			return mods;
		}
	}
}
