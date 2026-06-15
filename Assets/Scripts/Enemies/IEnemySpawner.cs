using System.Collections.Generic;
using BridgeOfBlood.Data.Enemies;
using UnityEngine;

/// <summary>
/// Produces fully resolved enemy spawn requests for the simulation spawn step.
/// Each implementation owns its <see cref="EnemySpawnTable"/> and decides whether to apply <see cref="SpawnPattern"/> assets.
/// </summary>
public interface IEnemySpawner
{
	/// <summary>
	/// Returns spawn requests accumulated since the last spawn step (playfield-local positions, ready for <see cref="EnemyManager.CreateEnemies"/>).
	/// </summary>
	List<EnemySpawnRequest> CollectSpawnRequests(float simulationTime, Rect playfield);

	/// <summary>Resets spawn tracking (e.g. on new round).</summary>
	void Reset();
}

/// <summary>Resolved batch: one enemy type and final spawn positions in playfield-local space.</summary>
public struct EnemySpawnRequest
{
	public EnemyAuthoringData enemy;
	public List<Vector2> positions;
}
