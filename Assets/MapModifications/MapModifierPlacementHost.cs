using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Shared placement/sim orchestration for standalone map-mod authoring and the session MapMod phase.
/// Does not own <see cref="GameSimulation"/> — the caller injects it with map config already applied.
/// </summary>
public sealed class MapModifierPlacementHost : MonoBehaviour
{
    [SerializeField] MapModifierController modifierController;

    readonly ModificationCatalog _emptyFrameModifications = new ModificationCatalog();
    GameSimulation _simulation;
    IReadOnlyList<MapModifierDefinition> _definitions;
    IReadOnlyList<MapModifier> _placedModifiers;
    Action<MapModifier> _onCommitted;
    Action _sessionComplete;
    MapModifierPlacementController _placementController;
    AwaitableCompletionSource _simTick;
    int _playOutVersion;
    int _remainingPlacements;
    bool _allowAuthoringCommands;
    bool _sessionActive;
    bool _isSettling;

    bool IsSettling => _isSettling;

    public MapModifierController ModifierController
    {
        get => modifierController;
        set => modifierController = value;
    }
    public MapModifierPlacementController PlacementController => _placementController;
    public bool SessionActive => _sessionActive;

    public void BeginSession(
        GameSimulation simulation,
        ISimulationZoneService zoneService,
        IReadOnlyList<MapModifier> placedModifiers,
        int newPlacementBudget,
        Action<MapModifier> onCommitted,
        Action sessionComplete,
        bool allowAuthoringCommands)
    {
        if (!(simulation.Spawner is NoiseSheetEnemySpawner))
        {
            throw new InvalidOperationException(
                "Map modifier placement requires a NoiseSheetEnemySpawner in the map configuration.");
        }

        _definitions = Resources.LoadAll<MapModifierDefinition>("MapModifiers");
        _simulation = simulation;
        _placedModifiers = placedModifiers;
        _onCommitted = onCommitted;
        _sessionComplete = sessionComplete;
        _remainingPlacements = newPlacementBudget;
        _allowAuthoringCommands = allowAuthoringCommands;
        _sessionActive = true;

        if (_placementController == null)
            _placementController = gameObject.AddComponent<MapModifierPlacementController>();

        _placementController.Initialize(zoneService, _placedModifiers, OnModifierCommitted);
        _placementController.SetPlacementEnabled(true);

        if (modifierController != null)
        {
            modifierController.gameObject.SetActive(true);
            modifierController.Bind(_definitions, ArmDefinition);
        }
    }

    public void EndSession()
    {
        _sessionActive = false;
        if (_placementController != null)
        {
            _placementController.Disarm();
            _placementController.SetPlacementEnabled(false);
        }

        if (modifierController != null)
            modifierController.gameObject.SetActive(false);

        CancelPlayOut();
        _simulation = null;
        _definitions = null;
        _placedModifiers = null;
        _onCommitted = null;
        _sessionComplete = null;
    }

    public void RebuildField()
    {
        CancelPlayOut();
        SetPlacementLocked(false);
        _simulation.ResetForNewRound();
        _simulation.FlushSpawnerNow();
        MapModifierApplicator.ApplyMapModifiers(_placedModifiers, _simulation);
        PublishCurrentState(simulationAdvanced: false, deltaTime: 0f);
    }

    public void Tick(float deltaTime)
    {
        CombatSimWaitRunner.StepOnce(_simulation, deltaTime);
        PublishCurrentState(simulationAdvanced: true, deltaTime: deltaTime);
        _simulation.ClearFrameCombatEvents();

        AwaitableCompletionSource tick = _simTick;
        _simTick = null;
        tick?.SetResult();
    }

    public void SetPointerBlocked(bool blocked)
    {
        if (_placementController != null)
            _placementController.SetPointerBlocked(blocked);
    }

    void OnGUI()
    {
        if (!_sessionActive)
            return;

        const float x = 10f;
        const float y = 10f;
        const float width = 300f;
        float height = Mathf.Min(Screen.height - 20f, 520f);
        GUI.Box(new Rect(x, y, width, height), GUIContent.none);

        GUILayout.BeginArea(new Rect(x + 10f, y + 10f, width - 20f, height - 20f));
        GUILayout.Label("Map Modifiers");

        if (_allowAuthoringCommands && GUILayout.Button("Reset + Reapply") && !IsSettling)
            RebuildField();

        if (!_allowAuthoringCommands)
            GUILayout.Label($"Placements remaining: {_remainingPlacements}");

        GUILayout.Space(8f);
        GUILayout.Label("Select, then place in the simulation zone:");
        IReadOnlyList<MapModifierDefinition> definitions = _definitions;
        for (int i = 0; i < definitions.Count; i++)
        {
            MapModifierDefinition definition = definitions[i];
            bool armed = _placementController.ActiveDefinition == definition;
            string prefix = armed ? "> " : string.Empty;
            if (GUILayout.Button(prefix + definition.DisplayName) && !IsSettling)
                ArmDefinition(definition);
        }

        if (_placementController.ActiveDefinition != null)
        {
            GUILayout.Label(
                _placementController.ActiveDefinition.PlacementMode == MapModifierPlacementMode.Directed
                    ? "Click LMB to place, then click again to set direction."
                    : _placementController.ActiveDefinition.PlacementMode == MapModifierPlacementMode.Region
                        ? "Scroll to resize; click LMB to place."
                        : "Click LMB to place.");
        }

        GUILayout.Space(10f);
        GUILayout.Label($"Placed ({_placedModifiers.Count})");
        for (int i = 0; i < _placedModifiers.Count; i++)
            GUILayout.Label(Describe(_placedModifiers[i]));

        if (_allowAuthoringCommands
            && !IsSettling
            && _placedModifiers is IList<MapModifier> mutable
            && mutable.Count > 0
            && GUILayout.Button("Clear Placed Modifiers"))
        {
            mutable.Clear();
            RebuildField();
        }

        GUILayout.EndArea();
    }

    void ArmDefinition(MapModifierDefinition definition)
    {
        if (IsSettling)
            return;

        _placementController.Arm(definition);
        if (modifierController != null)
            modifierController.SetArmed(definition);
    }

    void OnModifierCommitted(MapModifier modifier)
    {
        _onCommitted(modifier);
        bool exhaustBudget = false;
        if (_remainingPlacements != int.MaxValue)
        {
            _remainingPlacements--;
            exhaustBudget = _remainingPlacements <= 0;
        }

#pragma warning disable CS4014
        PlayOutAsync(modifier, exhaustBudget);
#pragma warning restore CS4014
    }

    async Awaitable PlayOutAsync(MapModifier modifier, bool completeSession)
    {
        int version = _playOutVersion;
        var context = new MapModifierContext(
            _simulation.AttackEntityManager,
            _simulation.EnemyManager,
            _simulation.State.Playfield);

        foreach (CombatSimWait wait in modifier.Apply(context))
        {
            if (version != _playOutVersion)
                return;
            if (wait.IsSatisfied(_simulation))
                continue;

            _isSettling = true;
            SetPlacementLocked(true);
            while (!wait.IsSatisfied(_simulation))
            {
                if (version != _playOutVersion)
                    return;
                await WaitForSimTick();
                if (version != _playOutVersion)
                    return;
                wait.NotifyTicked();
            }
        }

        if (version != _playOutVersion)
            return;

        _isSettling = false;
        SetPlacementLocked(false);
        if (completeSession)
            _sessionComplete();
    }

    Awaitable WaitForSimTick()
    {
        _simTick = new AwaitableCompletionSource();
        return _simTick.Awaitable;
    }

    void CancelPlayOut()
    {
        _playOutVersion++;
        _isSettling = false;
        AwaitableCompletionSource tick = _simTick;
        _simTick = null;
        tick?.SetResult();
    }

    void SetPlacementLocked(bool locked)
    {
        if (_placementController != null)
            _placementController.SetPlacementEnabled(!locked);
    }

    void PublishCurrentState(bool simulationAdvanced, float deltaTime)
    {
        Rect playfield = _simulation.State.Playfield;
        SharedGameEventBus.Bus.Raise(new SimulationCompleteEvent
        {
            simulationState = _simulation.State,
            deltaTime = deltaTime,
            simulationTime = _simulation.State.SimulationTime,
            simulationAdvanced = simulationAdvanced,
            spellCastResult = default,
            playerPosition = new float2(playfield.xMax - 10f, playfield.center.y),
            frameModifications = _emptyFrameModifications
        });
    }

    static string Describe(MapModifier modifier)
    {
        if (modifier is AttackEntitySpawnMapModifier attack)
        {
            Vector2 position = attack.Emitter.Position;
            return $"{modifier.index + 1}. {attack.AttackEntityData.name} " +
                $"({position.x:0.#}, {position.y:0.#})";
        }

        var combine = (EnemyCombineMapModifier)modifier;
        return $"{modifier.index + 1}. Combine r={combine.Radius:0.#} " +
            $"({combine.Center.x:0.#}, {combine.Center.y:0.#})";
    }
}
