using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using EZServiceLocation;
using UnityEngine;

/// <summary>
/// Standalone map-modifier authoring scene. Owns a local <see cref="GameSimulation"/> and
/// delegates placement/tick/rebuild to <see cref="MapModifierController"/>.
/// </summary>
[DefaultExecutionOrder(-110)]
public sealed class MapModScene : MonoBehaviour
{
    [Header("Session")]
    [SerializeField] GameConfig gameConfig;
    [SerializeField] RectTransform simulationZone;
    [SerializeField] Camera renderCamera;
    [SerializeField] CombatPresentationDriver presentationDriver;

    [Header("Placement")]
    [SerializeField] MapModifierController modifierController;

    readonly List<MapModifier> _placedModifiers = new List<MapModifier>();

    GameSimulation _simulation;
    MapService _mapService;
    MapModifierController _controller;

    public IReadOnlyList<MapModifier> PlacedModifiers => _placedModifiers;

    void Awake()
    {
        CreateSimulation();
    }

    void Start()
    {
        _controller.RebuildField();
    }

    void Update()
    {
        bool overPanel =
            Input.mousePosition.x <= 320f
            && Input.mousePosition.y >= Screen.height - 540f;
        _controller.SetPointerBlocked(overPanel);
        _controller.Tick(Time.deltaTime);
    }

    void CreateSimulation()
    {

        _simulation = new GameSimulation(gameConfig.simulationConfig);
        _simulation.ApplyRoundConfig(gameConfig, 1);

        _mapService = new MapService(
            new Map(gameConfig.simulationSize.x, gameConfig.simulationSize.y),
            renderCamera);
        ServiceLocator.Current.RegisterInstance<IMapService>(_mapService);
        simulationZone
            .GetComponent<SimulationRectRenderer>()
            .SyncFrom(_simulation.State.Playfield);
        presentationDriver.InitializeStandalone(
            _mapService,
            _simulation.AttackEntityManager);

        _controller = modifierController;
        if (_controller == null)
            _controller = gameObject.GetComponent<MapModifierController>();
        if (_controller == null)
            _controller = gameObject.AddComponent<MapModifierController>();

        _controller.BeginSession(
            _simulation,
            _mapService,
            _placedModifiers,
            int.MaxValue,
            _placedModifiers.Add,
            sessionComplete: null,
            allowAuthoringCommands: true);
    }

    void OnDestroy()
    {
        if (_controller != null)
            _controller.EndSession();
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
