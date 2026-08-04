using BridgeOfBlood.Data.Shared;
using EZServiceLocation;
using UnityEngine;

/// <summary>
/// Scene-bound owner of combat presentation: materials/atlas serialized on this component,
/// player renderer, and draw/update driven by <see cref="SimulationCompleteEvent"/>.
/// Zone/camera and attack-entity debug bind from <see cref="ServiceLocator"/> on
/// <see cref="ServicesRegisteredEvent"/> — simulation never references this type.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class CombatPresentationDriver : MonoBehaviour
{
	[SerializeField] CombatPresentationResources resources = new CombatPresentationResources();
	[SerializeField] PlayerRenderer playerRenderer;

	CombatPresentationLayer _layer;
	ISimulationZoneService _zoneService;

	void OnEnable()
	{
		EnsureLayer();
		SharedGameEventBus.Bus.SubscribeTo<SimulationCompleteEvent>(OnSimulationComplete);
		ServicesRegisteredEvent.SubscribeAndCatchUp(OnServicesRegistered);
	}

	void OnDisable()
	{
		SharedGameEventBus.Bus.UnsubscribeFrom<SimulationCompleteEvent>(OnSimulationComplete);
		ServicesRegisteredEvent.Unsubscribe(OnServicesRegistered);
		_layer?.Dispose();
		_layer = null;
		_zoneService = null;
	}

	void OnServicesRegistered(ref ServicesRegisteredEvent _)
	{
		EnsureLayer();
		_zoneService = ServiceLocator.Current.GetService<ISimulationZoneService>();
		var combat = ServiceLocator.Current.GetService<CombatSimulationController>();
		_layer.BindAttackEntities(combat.Simulation.AttackEntityManager);
	}

	void OnSimulationComplete(ref SimulationCompleteEvent @event)
	{
		EnsureLayer();
		RectTransform zone = _zoneService.Zone;
		Camera cam = _zoneService.Camera != null ? _zoneService.Camera : Camera.main;
		_layer.HandleFrameComplete(ref @event, zone, cam);
	}

	void OnDrawGizmos()
	{
		if (_zoneService?.Zone == null || _layer == null)
			return;
		_layer.DrawGizmos(_zoneService.Zone);
	}

	void EnsureLayer()
	{
		if (_layer != null)
			return;

		_layer = new CombatPresentationLayer(resources);
		_layer.BindPlayerRenderer(playerRenderer);
	}
}
