using System;
using UnityEngine;

namespace BridgeOfBlood.Effects
{
	[Serializable]
	public class ScriptableObjectValue<T> : IValue<T> where T : ScriptableObject
	{
		public T value;

		public T Resolve(EffectContext context) => value;
	}
}
