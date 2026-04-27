using System;
using Unity.Mathematics;

namespace BridgeOfBlood.Data.Shared
{
	/// <summary>
	/// Inclusive min/max authoring range. Use <see cref="ClampOrder"/> before resolve if values may be inverted.
	/// </summary>
	[Serializable]
	public struct FloatRange
	{
		public float min;
		public float max;

		public void ClampOrder()
		{
			if (min > max)
				(min, max) = (max, min);
		}

		/// <summary>Uniform sample in [min, max] after ordering endpoints.</summary>
		public float ResolveUniform(ref Unity.Mathematics.Random rng)
		{
			ClampOrder();
			if (min == max)
				return min;
			return rng.NextFloat(min, max);
		}
	}

	/// <summary>Deterministic seed for rolling attack entity stats once per keyframe.</summary>
	public static class AttackEntityBuildRngSeed
	{
		public static uint Mix(int spellId, int spellInvocationId, int keyframeIndex, int attackDataInstanceId)
		{
			unchecked
			{
				uint h = 2166136261u;
				h = (h ^ (uint)spellId) * 16777619u;
				h = (h ^ (uint)spellInvocationId) * 16777619u;
				h = (h ^ (uint)keyframeIndex) * 16777619u;
				h = (h ^ (uint)attackDataInstanceId) * 16777619u;
				return h == 0u ? 1u : h;
			}
		}
	}
}
