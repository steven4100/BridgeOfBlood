using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace BridgeOfBlood.Data.Enemies
{
	/// <summary>
	/// How spawn points are distributed inside fill shapes.
	/// </summary>
	public enum SpawnDistribution : byte
	{
		Random = 0,
		Grid = 1
	}

	/// <summary>
	/// ScriptableObject that defines a spawn pattern: one or more fill shapes, density (points per unit area),
	/// distribution (random or grid), and optional omission zones. Points are generated at runtime
	/// from the shapes; no stored point list.
	/// </summary>
	[CreateAssetMenu(fileName = "SpawnPattern", menuName = "Bridge of Blood/Enemies/Spawn Pattern", order = 1)]
	public class SpawnPattern : ScriptableObject
	{
		[Header("Fill shapes")]
		[Tooltip("Shapes define the spawn region (union). A point is kept if it lies inside any of these shapes. Overlaps are not double-counted.")]
		public List<SpawnShape> fillShapes = new List<SpawnShape>
		{
			new SpawnShape { type = SpawnShapeType.Circle, size = new Vector2(5f, 0f) }
		};

		[Tooltip("Points per unit area. Count N = total fill area * density (rounded).")]
		[Min(0.01f)]
		public float spawnDensity = 0.5f;

		public SpawnDistribution distribution = SpawnDistribution.Random;

		[Tooltip("Grid only: random offset per point as a fraction of cell step. 0 = strict grid.")]
		[Min(0f)]
		public float gridJitter = 0.4f;

		[Header("Omission zones")]
		[Tooltip("Points inside any of these shapes are discarded.")]
		public List<SpawnShape> omissionZones = new List<SpawnShape>();

		/// <summary>
		/// Fills the list with world positions for this spawn event. Clears the list first.
		/// </summary>
		public void GetPositions(Vector2 origin, List<Vector2> outPositions)
		{
			var rng = Unity.Mathematics.Random.CreateFromIndex(
				(uint)(origin.x.GetHashCode() ^ (origin.y.GetHashCode() << 16) ^ (int)(Time.realtimeSinceStartup * 1000f)));
			GetPositionsInternal(origin, outPositions, rng);
		}

		/// <summary>
		/// Deterministic version using a seed for reproducible spawns.
		/// </summary>
		public void GetPositions(Vector2 origin, List<Vector2> outPositions, uint seed)
		{
			var rng = Unity.Mathematics.Random.CreateFromIndex(seed);
			GetPositionsInternal(origin, outPositions, rng);
		}

		void GetPositionsInternal(Vector2 origin, List<Vector2> outPositions, Unity.Mathematics.Random rng)
		{
			outPositions.Clear();
			if (fillShapes == null || fillShapes.Count == 0)
			{
				outPositions.Add(origin);
				return;
			}

			float totalArea = 0f;
			for (int i = 0; i < fillShapes.Count; i++)
				totalArea += Mathf.Max(0f, fillShapes[i].GetArea());

			if (totalArea <= 0f)
			{
				outPositions.Add(origin);
				return;
			}

			int totalCount = Mathf.Max(1, Mathf.RoundToInt(totalArea * spawnDensity));

			if (distribution == SpawnDistribution.Random)
				GenerateRandom(origin, totalCount, totalArea, outPositions, rng);
			else
				GenerateGrid(origin, totalArea, outPositions, rng);

			if (outPositions.Count == 0)
				outPositions.Add(origin);
		}

		void GenerateRandom(Vector2 origin, int totalCount, float totalArea, List<Vector2> outPositions, Unity.Mathematics.Random rng)
		{
			GetUnionBounds(out float minX, out float minY, out float maxX, out float maxY);
			float w = maxX - minX;
			float h = maxY - minY;
			if (w <= 0f || h <= 0f) return;

			int maxAttempts = totalCount * 25;
			int attempts = 0;
			while (outPositions.Count < totalCount && attempts < maxAttempts)
			{
				attempts++;
				Vector2 pointInPatternSpace = new Vector2(
					minX + rng.NextFloat() * w,
					minY + rng.NextFloat() * h);
				if (IsInAnyFillShape(pointInPatternSpace) && !IsInAnyOmission(pointInPatternSpace))
					outPositions.Add(origin + pointInPatternSpace);
			}
		}

		void GenerateGrid(Vector2 origin, float totalArea, List<Vector2> outPositions, Unity.Mathematics.Random rng)
		{
			GetUnionBounds(out float minX, out float minY, out float maxX, out float maxY);
			float w = maxX - minX;
			float h = maxY - minY;
			if (w <= 0f || h <= 0f) return;

			int totalCount = Mathf.Max(1, Mathf.RoundToInt(totalArea * spawnDensity));
			float unionArea = w * h;
			float step = (unionArea > 0f && totalCount > 0) ? Mathf.Sqrt(unionArea / totalCount) : 1f;
			int nx = Mathf.Max(1, Mathf.RoundToInt(w / step));
			int ny = Mathf.Max(1, Mathf.RoundToInt(h / step));
			float jitter = step * gridJitter;

			for (int iy = 0; iy < ny; iy++)
			{
				for (int ix = 0; ix < nx; ix++)
				{
					float tx = (ix + 0.5f) / nx;
					float ty = (iy + 0.5f) / ny;
					float px = minX + w * tx + (rng.NextFloat() - 0.5f) * 2f * jitter;
					float py = minY + h * ty + (rng.NextFloat() - 0.5f) * 2f * jitter;
					Vector2 pointInPatternSpace = new Vector2(px, py);
					if (IsInAnyFillShape(pointInPatternSpace) && !IsInAnyOmission(pointInPatternSpace))
						outPositions.Add(origin + pointInPatternSpace);
				}
			}
		}

		void GetUnionBounds(out float minX, out float minY, out float maxX, out float maxY)
		{
			minX = minY = float.MaxValue;
			maxX = maxY = float.MinValue;
			for (int i = 0; i < fillShapes.Count; i++)
			{
				fillShapes[i].GetBounds(out float a, out float b, out float c, out float d);
				if (a < minX) minX = a;
				if (b < minY) minY = b;
				if (c > maxX) maxX = c;
				if (d > maxY) maxY = d;
			}
			if (minX == float.MaxValue) { minX = minY = maxX = maxY = 0f; }
		}

		bool IsInAnyFillShape(Vector2 pointInPatternSpace)
		{
			for (int i = 0; i < fillShapes.Count; i++)
			{
				if (fillShapes[i].Contains(pointInPatternSpace))
					return true;
			}
			return false;
		}

		bool IsInAnyOmission(Vector2 pointInPatternSpace)
		{
			if (omissionZones == null) return false;
			for (int i = 0; i < omissionZones.Count; i++)
			{
				if (omissionZones[i].Contains(pointInPatternSpace))
					return true;
			}
			return false;
		}
	}
}
