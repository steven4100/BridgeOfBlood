using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using UnityEngine;

/// <summary>
/// Standalone map-modifier authoring scene. Owns a local <see cref="GameSimulation"/> and
/// delegates placement/tick/rebuild to <see cref="MapModifierPlacementHost"/>.
/// </summary>
[DefaultExecutionOrder(-110)]
public sealed class MapModScene : MonoBehaviour
{
    [Header("Session")]
    [SerializeField] GameConfig gameConfig;
    [SerializeField] RectTransform simulationZone;
    [SerializeField] Camera renderCamera;
    [SerializeField] CombatPresentationDriver presentationDriver;

    [Header("Optional catalog UI")]
    [SerializeField] MapModifierController modifierController;

    readonly List<MapModifier> _placedModifiers = new List<MapModifier>();

    GameSimulation _simulation;
    ISimulationZoneService _zoneService;
    MapModifierPlacementHost _host;

    public IReadOnlyList<MapModifier> PlacedModifiers => _placedModifiers;

    void Awake()
    {
        CreateSimulation();
    }

    void Start()
    {
        _host.RebuildField();
    }

    void Update()
    {
        bool overPanel =
            Input.mousePosition.x <= 320f
            && Input.mousePosition.y >= Screen.height - 540f;
        _host.SetPointerBlocked(overPanel);
        _host.Tick(Time.deltaTime);
    }

    void CreateSimulation()
    {
        _simulation = new GameSimulation(gameConfig.simulationConfig);
        _simulation.ApplyRoundConfig(gameConfig, 1);

        _zoneService = new SimulationZoneService(simulationZone, renderCamera);
        simulationZone
            .GetComponent<SimulationRectRenderer>()
            .SyncFrom(_simulation.State.Playfield);
        presentationDriver.InitializeStandalone(
            _zoneService,
            _simulation.AttackEntityManager);

        _host = gameObject.GetComponent<MapModifierPlacementHost>();
        if (_host == null)
            _host = gameObject.AddComponent<MapModifierPlacementHost>();

        _host.ModifierController = modifierController;
        _host.BeginSession(
            _simulation,
            _zoneService,
            _placedModifiers,
            int.MaxValue,
            _placedModifiers.Add,
            sessionComplete: null,
            allowAuthoringCommands: true);
    }

    void OnDestroy()
    {
        if (_host != null)
            _host.EndSession();
        _simulation?.Dispose();
        _simulation = null;
    }

    void OnDrawGizmos()
    {
        if (simulationZone == null || _simulation == null)
            return;

        IReadOnlyList<IDebugDrawable> drawables = _simulation.GetDebugDrawables();
        for (int i = 0; i < drawables.Count; i++)
            drawables[i].DrawGizmos(simulationZone);
    }
}
