using BridgeOfBlood.Data.Enemies;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Right-click in the game view to select the nearest enemy.
/// Reads enemies from a GameSimulation found via TestSceneManager.
/// </summary>
public class EnemySelector : MonoBehaviour
{
	public RectTransform simulationZone;
	public Camera renderCamera;

	public int SelectedEnemyId { get; private set; } = -1;

	void Update()
	{
		if (!Input.GetMouseButtonDown(1)) return;
		if (simulationZone == null) return;

		Camera cam = renderCamera != null ? renderCamera : Camera.main;
		if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
				simulationZone, Input.mousePosition, cam, out Vector2 localPoint))
			return;

		var sceneManager = GetComponentInParent<TestSceneManager>();
		if (sceneManager == null || sceneManager.Simulation == null) return;

		var enemies = sceneManager.Simulation.State.EnemyBuffers;
		if (!enemies.Motion.IsCreated || enemies.Length == 0) return;

		float bestDist = float.MaxValue;
		int bestId = -1;
		var click = new float2(localPoint.x, localPoint.y);

		for (int i = 0; i < enemies.Length; i++)
		{
			float dist = math.distancesq(click, enemies.Motion[i].position);
			if (dist < bestDist)
			{
				bestDist = dist;
				bestId = enemies.EntityIds[i];
			}
		}

		SelectedEnemyId = bestId;
	}
}
