using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Enemies;
using UnityEngine;

public enum NoiseType
{
	Perlin,
	Value,
	Worley
}

public enum NoiseBlendMode
{
	Add,
	Multiply,
	Max,
	Min
}

[Serializable]
public class NoiseLayer
{
	public bool enabled = true;
	public NoiseType type = NoiseType.Perlin;
	public NoiseBlendMode blend = NoiseBlendMode.Add;
	[Min(0f)]
	public float weight = 1f;
	[Min(0.001f)]
	public float frequency = 4f;
	public Vector2 offset;
	public bool invert;

	public static NoiseLayer Default(NoiseType type) => new NoiseLayer
	{
		enabled = true,
		type = type,
		blend = NoiseBlendMode.Add,
		weight = 1f,
		frequency = 4f,
		offset = Vector2.zero,
		invert = false
	};
}

/// <summary>
/// Composes stacked noise layers into a [0,1] field sampled at UV coordinates.
/// Shared by runtime spawn sampling and the Inspector grid preview.
/// </summary>
public static class NoiseField
{
	public static float SampleComposed(IList<NoiseLayer> layers, float u, float v, uint seed)
	{
		if (layers == null || layers.Count == 0)
			return 0f;

		float composed = 0f;
		bool hasValue = false;

		for (int i = 0; i < layers.Count; i++)
		{
			NoiseLayer layer = layers[i];
			if (layer == null || !layer.enabled || layer.weight <= 0f)
				continue;

			float sample = SampleLayer(layer, u, v, seed + (uint)(i * 9973));
			if (layer.invert)
				sample = 1f - sample;
			sample *= layer.weight;

			if (!hasValue)
			{
				composed = sample;
				hasValue = true;
				continue;
			}

			composed = Blend(composed, sample, layer.blend);
		}

		return hasValue ? Mathf.Clamp01(composed) : 0f;
	}

	static float Blend(float a, float b, NoiseBlendMode mode)
	{
		switch (mode)
		{
			case NoiseBlendMode.Multiply: return a * b;
			case NoiseBlendMode.Max: return Mathf.Max(a, b);
			case NoiseBlendMode.Min: return Mathf.Min(a, b);
			default: return Mathf.Clamp01(a + b);
		}
	}

	static float SampleLayer(NoiseLayer layer, float u, float v, uint layerSeed)
	{
		float freq = Mathf.Max(0.001f, layer.frequency);
		float x = u * freq + layer.offset.x + SeedOffset(layerSeed, 0);
		float y = v * freq + layer.offset.y + SeedOffset(layerSeed, 1);

		switch (layer.type)
		{
			case NoiseType.Value: return ValueNoise(x, y);
			case NoiseType.Worley: return WorleyNoise(x, y);
			default: return Mathf.PerlinNoise(x, y);
		}
	}

	static float SeedOffset(uint seed, int channel)
	{
		uint h = seed ^ (uint)(channel * 374761393);
		h = (h ^ (h >> 16)) * 0x85ebca6bu;
		h ^= h >> 13;
		return (h & 0xFFFF) / 65535f * 256f;
	}

	static float ValueNoise(float x, float y)
	{
		int x0 = Mathf.FloorToInt(x);
		int y0 = Mathf.FloorToInt(y);
		float fx = x - x0;
		float fy = y - y0;
		fx = fx * fx * (3f - 2f * fx);
		fy = fy * fy * (3f - 2f * fy);

		float v00 = Hash01(x0, y0);
		float v10 = Hash01(x0 + 1, y0);
		float v01 = Hash01(x0, y0 + 1);
		float v11 = Hash01(x0 + 1, y0 + 1);

		float a = Mathf.Lerp(v00, v10, fx);
		float b = Mathf.Lerp(v01, v11, fx);
		return Mathf.Lerp(a, b, fy);
	}

	static float WorleyNoise(float x, float y)
	{
		int cellX = Mathf.FloorToInt(x);
		int cellY = Mathf.FloorToInt(y);
		float minDistSq = float.MaxValue;

		for (int oy = -1; oy <= 1; oy++)
		{
			for (int ox = -1; ox <= 1; ox++)
			{
				int cx = cellX + ox;
				int cy = cellY + oy;
				float px = cx + Hash01(cx, cy);
				float py = cy + Hash01(cx + 19, cy + 47);
				float dx = px - x;
				float dy = py - y;
				float dSq = dx * dx + dy * dy;
				if (dSq < minDistSq)
					minDistSq = dSq;
			}
		}

		return Mathf.Clamp01(Mathf.Sqrt(minDistSq));
	}

	static float Hash01(int x, int y)
	{
		uint h = (uint)(x * 374761393 + y * 668265263);
		h = (h ^ (h >> 13)) * 1274126177u;
		h ^= h >> 16;
		return (h & 0xFFFFFF) / 16777215f;
	}
}

/// <summary>
/// One-shot sheet spawner: samples a multi-layer noise field on a grid sized to the playfield
/// (same width/height as the simulation rect). Sheet center is playfield center plus
/// <see cref="centerOffsetNormalized"/> scaled by playfield size.
/// Emits all matching positions on the first collect after <see cref="Reset"/>.
/// </summary>
[Serializable]
public class NoiseSheetEnemySpawner : IEnemySpawner
{
	public EnemySpawnTable spawnTable;

	[Tooltip("Sheet center offset from playfield center, in normalized playfield units (1 = one full width/height). (0,0) overlaps the playfield; (-1,0) sits fully to the left.")]
	public Vector2 centerOffsetNormalized = new Vector2(-1f, 0f);

	[Tooltip("Grid spacing for noise samples / spawn positions.")]
	[Min(0.25f)]
	public float cellSize = 4f;

	[Tooltip("Spawn where composed noise is greater than or equal to this value.")]
	[Range(0f, 1f)]
	public float threshold = 0.5f;

	public uint seed = 1;

	public List<NoiseLayer> layers = new List<NoiseLayer>
	{
		NoiseLayer.Default(NoiseType.Perlin)
	};

	bool _spawned;

	public void Reset()
	{
		_spawned = false;
	}

	public List<EnemySpawnRequest> CollectSpawnRequests(float simulationTime, Rect playfield)
	{
		if (_spawned || spawnTable == null)
			return new List<EnemySpawnRequest>();

		_spawned = true;

		float sheetW = playfield.width;
		float sheetH = playfield.height;
		if (sheetW <= 0f || sheetH <= 0f)
			return new List<EnemySpawnRequest>();

		float cell = Mathf.Max(0.25f, cellSize);
		Vector2 sheetCenter = playfield.center + new Vector2(
			centerOffsetNormalized.x * sheetW,
			centerOffsetNormalized.y * sheetH);
		float xMin = sheetCenter.x - sheetW * 0.5f;
		float yMin = sheetCenter.y - sheetH * 0.5f;

		var byEnemy = new Dictionary<EnemyAuthoringData, List<Vector2>>();
		int cols = Mathf.Max(1, Mathf.FloorToInt(sheetW / cell));
		int rows = Mathf.Max(1, Mathf.FloorToInt(sheetH / cell));
		uint baseSeed = seed == 0 ? 1u : seed;

		for (int row = 0; row < rows; row++)
		{
			for (int col = 0; col < cols; col++)
			{
				float u = (col + 0.5f) / cols;
				float v = (row + 0.5f) / rows;
				if (NoiseField.SampleComposed(layers, u, v, baseSeed) < threshold)
					continue;

				uint pickSeed = baseSeed + (uint)(row * 73856093) + (uint)(col * 19349663);
				EnemySpawnPick pick = spawnTable.PickEnemyByWeight(pickSeed);
				if (pick.enemy == null)
					continue;

				float jitter = cell * 0.25f;
				var rng = Unity.Mathematics.Random.CreateFromIndex(pickSeed ^ 0x9E3779B9u);
				float x = xMin + (col + 0.5f) * cell + rng.NextFloat(-jitter, jitter);
				float y = yMin + (row + 0.5f) * cell + rng.NextFloat(-jitter, jitter);

				if (!byEnemy.TryGetValue(pick.enemy, out List<Vector2> positions))
				{
					positions = new List<Vector2>();
					byEnemy[pick.enemy] = positions;
				}
				positions.Add(new Vector2(x, y));
			}
		}

		var requests = new List<EnemySpawnRequest>(byEnemy.Count);
		foreach (var kvp in byEnemy)
		{
			requests.Add(new EnemySpawnRequest
			{
				enemy = kvp.Key,
				positions = kvp.Value
			});
		}
		return requests;
	}
}
