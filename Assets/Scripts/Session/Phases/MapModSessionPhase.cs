using System;
using BridgeOfBlood.Data.Shared;
using EZServiceLocation;
using UnityEngine;

/// <summary>
/// Shop-nested placement visit: injects session map config and existing modifiers,
/// then returns to <see cref="SessionState.Shop"/> after the purchased placement budget.
/// </summary>
public sealed class MapModSessionPhase : SessionPhaseBase<MapModSessionPhase.Context>
{
	public readonly struct Context
	{
		public readonly int PlacementCount;

		public Context(int placementCount)
		{
			if (placementCount <= 0)
				throw new ArgumentOutOfRangeException(
					nameof(placementCount),
					"Map mod entry requires a positive placement count.");
			PlacementCount = placementCount;
		}
	}

	readonly CombatSimulationController _combatSimulation;
	readonly MapModifierPlacementHost _placementHost;

	IMapModifierSessionService _mapModifiers;

	public override SessionState State => SessionState.MapMod;

	public MapModSessionPhase(
		SessionFlowController controller,
		CombatSimulationController combatSimulation,
		MapModifierPlacementHost placementHost)
		: base(controller)
	{
		_combatSimulation = combatSimulation;
		_placementHost = placementHost;
	}

	protected override void OnEnter(Context context)
	{
		_mapModifiers = ServiceLocator.Current.GetService<IMapModifierSessionService>();
		int placementCount = context.PlacementCount;

		ISimulationZoneService zoneService = ServiceLocator.Current.GetService<ISimulationZoneService>();
		_combatSimulation.ResetForNewRoundWithMapModifiers(_mapModifiers.PlacedModifiers);
		_placementHost.BeginSession(
			_combatSimulation.Simulation,
			zoneService,
			_mapModifiers.PlacedModifiers,
			placementCount,
			_mapModifiers.AddPlaced,
			OnPlacementBudgetExhausted,
			allowAuthoringCommands: false);

		SharedGameEventBus.Bus.Raise(new MapModEnterEvent
		{
			state = SessionState.MapMod,
			remainingPlacements = placementCount,
			playfield = _combatSimulation.Simulation.State.Playfield
		});
	}

	protected override void OnExit(Context context)
	{
		_placementHost.EndSession();
		SharedGameEventBus.Bus.Raise(new MapModExitEvent
		{
			state = SessionState.MapMod
		});
	}

	protected override void OnTick(Context context, float deltaTime)
	{
		bool overPanel =
			Input.mousePosition.x <= 320f
			&& Input.mousePosition.y >= Screen.height - 540f;
		_placementHost.SetPointerBlocked(overPanel);
		_placementHost.Tick(deltaTime);
	}

	void OnPlacementBudgetExhausted()
	{
		controller.EnterShop(new ShopSessionPhase.Context());
	}
}
