using System.Collections.Generic;

namespace BridgeOfBlood.Data.Shared
{
	public static class WeightedSelection
	{
		/// <summary>
		/// Picks an element using cumulative weight distribution.
		/// <paramref name="roll"/> should be in [0, 1).
		/// Does not require pre-normalized weights.
		/// </summary>
		public static T Pick<T>(IReadOnlyList<T> elements, float roll) where T : IRandomElement
		{
			float total = TotalWeight(elements);
			float target = roll * total;
			float cumulative = 0f;

			for (int i = 0; i < elements.Count; i++)
			{
				cumulative += elements[i].Weight;
				if (target < cumulative)
					return elements[i];
			}

			return elements[elements.Count - 1];
		}

		public static float TotalWeight<T>(IReadOnlyList<T> elements) where T : IRandomElement
		{
			float total = 0f;
			for (int i = 0; i < elements.Count; i++)
				total += elements[i].Weight;
			return total;
		}

		/// <summary>
		/// Scales all weights so they sum to 1.
		/// </summary>
		public static void Normalize<T>(IList<T> elements) where T : IRandomElement
		{
			float total = 0f;
			for (int i = 0; i < elements.Count; i++)
				total += elements[i].Weight;

			if (total <= 0f) return;

			for (int i = 0; i < elements.Count; i++)
				elements[i].Weight /= total;
		}
	}
}
