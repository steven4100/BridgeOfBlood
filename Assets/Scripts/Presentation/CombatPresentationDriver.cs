using BridgeOfBlood.Data.Shared;
using UnityEngine;

/// <summary>
/// Scene-bound owner of combat presentation: materials/atlas serialized on this component,
/// camera/zone/player renderer, and draw/update driven by <see cref="SimulationCompleteEvent"/>.
/// Simulation never references this type — wire in the scene (or a prefab) and call <see cref="Bind"/> after the
/// combat simulation is constructed.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class CombatPresentationDriver : MonoBehaviour
{
	[SerializeField] CombatPresentationResources resources = new CombatPresentationResources();
	[SerializeField] Camera renderCamera;
	[SerializeField] RectTransform simulationZone;
	[SerializeField] PlayerRenderer playerRenderer;

	CombatPresentationLayer _layer;

	void OnEnable()
	{
		EnsureLayer();
		SharedGameEventBus.Bus.SubscribeTo<SimulationCompleteEvent>(OnSimulationComplete);
	}

	void OnDisable()
	{
		SharedGameEventBus.Bus.UnsubscribeFrom<SimulationCompleteEvent>(OnSimulationComplete);
		_layer?.Dispose();
		_layer = null;
	}

	/// <summary>
	/// Wires attack-entity debug gizmos to the live manager (presentation → simulation).
	/// Call after constructing <see cref="CombatSimulationController"/> / on lab reset.
	/// </summary>
	public void Bind(AttackEntityManager attackEntityManager)
	{
		EnsureLayer();
		_layer.BindAttackEntities(attackEntityManager);
	}

	void OnSimulationComplete(ref SimulationCompleteEvent @event)
	{
		EnsureLayer();
		Camera cam = renderCamera != null ? renderCamera : Camera.main;
		_layer.HandleFrameComplete(ref @event, simulationZone, cam);
	}

	void OnDrawGizmos()
	{
		if (simulationZone == null || _layer == null)
			return;
		_layer.DrawGizmos(simulationZone);
	}

	void EnsureLayer()
	{
		if (_layer != null)
			return;

		_layer = new CombatPresentationLayer(resources);
		_layer.BindPlayerRenderer(playerRenderer);
	}
}
