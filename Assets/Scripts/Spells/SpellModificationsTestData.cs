using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using UnityEngine;

namespace BridgeOfBlood.Data.Spells
{
	[CreateAssetMenu(fileName = "SpellModificationsTest", menuName = "Bridge of Blood/Spell Modifications Test", order = 0)]
	public class SpellModificationsTestData : ScriptableObject
	{
		[Header("Typed modifications")]
		[SerializeReference, SerializeInterface]
		public List<IModificationData> modifiers = new List<IModificationData>();

		[Header("Conversion")]
		public List<DamageConversion> conversions = new List<DamageConversion>();

		[Header("Extra damage as")]
		public List<ExtraDamageAs> extraDamageAs = new List<ExtraDamageAs>();

		public ModificationSet GetModifications()
		{
			var mods = new ModificationSet();

			if (modifiers != null)
			{
				for (int i = 0; i < modifiers.Count; i++)
				{
					if (modifiers[i] != null)
						mods.Add(modifiers[i].Bake(null));
				}
			}

			if (conversions != null)
			{
				for (int i = 0; i < conversions.Count; i++)
				{
					mods.Add(new DamageConversionModificationData
					{
						conversion = conversions[i]
					});
				}
			}

			if (extraDamageAs != null)
			{
				for (int i = 0; i < extraDamageAs.Count; i++)
				{
					mods.Add(new ExtraDamageAsModificationData
					{
						extraDamageAs = extraDamageAs[i]
					});
				}
			}
			return mods;
		}
	}
}
