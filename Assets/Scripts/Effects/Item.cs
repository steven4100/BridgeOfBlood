using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using UnityEngine;

namespace BridgeOfBlood.Effects
{
	[CreateAssetMenu(fileName = "NewItem", menuName = "Bridge of Blood/Item")]
	public class Item : ScriptableObject, IEffect
	{
		[SerializeReference, SerializeInterface]
		public List<IEffect> effects;

		public bool Apply(EffectContext context)
		{
			if (effects == null) return false;

			bool anyApplied = false;
			foreach (var effect in effects)
				anyApplied |= effect.Apply(context);

			return anyApplied;
		}
	}
}
