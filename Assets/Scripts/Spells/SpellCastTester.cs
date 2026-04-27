using BridgeOfBlood.Data.Spells;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Dev/testing helper: spawns a spell wherever the user clicks in the simulation zone.
/// Uses its own SpellInvoker so click-spawned casts are independent of the loop.
/// Plain class — call Update() each frame from the game loop.
/// </summary>
public class SpellCastTester
{
    private readonly SpellInvoker _spellInvoker;
    private readonly SpellAuthoringData _testSpellData;
    private readonly Camera _camera;
    private readonly RectTransform _simulationZone;

    public SpellCastTester(
        SpellInvoker spellInvoker,
        SpellAuthoringData testSpellData,
        Camera camera,
        RectTransform simulationZone)
    {
        _spellInvoker = spellInvoker;
        _testSpellData = testSpellData;
        _camera = camera;
        _simulationZone = simulationZone;
    }

    /// <summary>
    /// Call each frame. On mouse click inside the simulation zone, starts a cast at the click position.
    /// Then advances the invoker's active casts (keyframes fire via callback).
    /// </summary>
    /// <param name="forward">Cast direction for emission (e.g. (1,0) for right).</param>
    public void Update(float simulationTime, float2 forward)
    {
        HandleClick(simulationTime);
        _spellInvoker?.Update(simulationTime, forward);
    }

    void HandleClick(float simulationTime)
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (_testSpellData == null || _testSpellData.SpellAnimation?.keyFrames == null
            || _testSpellData.SpellAnimation.keyFrames.Count == 0)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _simulationZone, Input.mousePosition, _camera, out Vector2 localPoint))
            return;

        if (!_simulationZone.rect.Contains(localPoint))
            return;

        var runtime = new RuntimeSpell(_testSpellData);
        _spellInvoker.StartCast(runtime, _testSpellData, new float2(localPoint.x, localPoint.y), simulationTime, runtime.spellId, spellInvocationId: 0);
    }
}
