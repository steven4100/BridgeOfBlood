using BridgeOfBlood.Data.Spells;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Dev/testing helper: spawns a spell wherever the user clicks in the simulation zone.
/// Emits through the supplied handler so click-spawned casts are independent of the loop.
/// Plain class — call Update() each frame from the game loop.
/// </summary>
public class SpellCastTester
{
    private readonly SpellEmissionHandler _emissionHandler;
    private readonly SpellAuthoringData _testSpellData;
    private readonly Camera _camera;
    private readonly RectTransform _simulationZone;

    public SpellCastTester(
        SpellEmissionHandler emissionHandler,
        SpellAuthoringData testSpellData,
        Camera camera,
        RectTransform simulationZone)
    {
        _emissionHandler = emissionHandler;
        _testSpellData = testSpellData;
        _camera = camera;
        _simulationZone = simulationZone;
    }

    /// <summary>
    /// Call each frame. On mouse click inside the simulation zone, starts a cast at the click position.
    /// </summary>
    /// <param name="forward">Cast direction for emission (e.g. (1,0) for right).</param>
    public void Update(float simulationTime, float2 forward)
    {
        HandleClick(simulationTime, forward);
    }

    void HandleClick(float simulationTime, float2 forward)
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (_testSpellData == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _simulationZone, Input.mousePosition, _camera, out Vector2 localPoint))
            return;

        if (!_simulationZone.rect.Contains(localPoint))
            return;

        var runtime = new RuntimeSpell(_testSpellData);
        _emissionHandler.Emit(runtime, new float2(localPoint.x, localPoint.y), forward, simulationTime, runtime.spellId, spellInvocationId: 0);
    }
}
