using System.Threading;
using System.Threading.Tasks;
using BridgeOfBlood.Data.Spells;
using EZServiceLocation;
using UnityEngine;

public class LoopedSpellCasterController : IInputReciever
{
    readonly LoopedSpellCaster _loopedCaster;
    CancellationTokenSource _cancellationTokenSource;
    bool _focused;
    SpellInputContext _pending;
    bool _hasPending;

    public LoopedSpellCasterController(LoopedSpellCaster loopedCaster)
    {
        _loopedCaster = loopedCaster;
    }

    public bool TryConsumeCast(out SpellInputContext context)
    {
        context = _pending;
        if (!_hasPending)
            return false;

        _hasPending = false;
        _pending = null;
        return true;
    }

    public void ClearPending()
    {
        _hasPending = false;
        _pending = null;
    }

    async Task GatherInputForSpell(CancellationTokenSource cancellationTokenSource, InputActionMap actionMap)
    {
        try
        {
            Vector2 origin = await GetSpellCastOrigin(cancellationTokenSource.Token, actionMap);
            RuntimeSpell spell = _loopedCaster.PeekNextSpell();

            Vector2 direction = Vector2.zero;
            if (spell != null && spell.Definition.requiresDirection)
            {
                Vector2 aimPoint = await GetSpellCastDirection(cancellationTokenSource.Token, actionMap);
                Vector2 delta = aimPoint - origin;
                if (delta.sqrMagnitude > 0.0001f)
                    direction = delta.normalized;
            }

            if (cancellationTokenSource.IsCancellationRequested)
                return;

            if (spell != null)
            {
                _pending = new SpellInputContext(origin, direction);
                _hasPending = true;
            }

            if (_focused)
                StartGather(actionMap);
        }
        catch (System.OperationCanceledException)
        {
        }
        finally
        {
            cancellationTokenSource.Dispose();
        }
    }

    void StartGather(InputActionMap actionMap)
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _ = GatherInputForSpell(_cancellationTokenSource, actionMap);
    }

    void CancelGather()
    {
        CancellationTokenSource gather = _cancellationTokenSource;
        _cancellationTokenSource = null;
        gather?.Cancel();
    }

    private async Task<Vector2> GetSpellCastOrigin(CancellationToken cancellationToken, InputActionMap actionMap)
    {
        return await WaitForSimulationPoint(cancellationToken, actionMap);
    }

    private async Task<Vector2> GetSpellCastDirection(CancellationToken cancellationToken, InputActionMap actionMap)
    {
        return await WaitForSimulationPoint(cancellationToken, actionMap);
    }

    async Task<Vector2> WaitForSimulationPoint(CancellationToken cancellationToken, InputActionMap actionMap)
    {
        TaskCompletionSource<Vector2> tcs = new TaskCompletionSource<Vector2>();

        void OnClick(InputContext context)
        {
            if (TryTransformScreenToSimulation(out Vector2 local))
                tcs.TrySetResult(local);
        }

        actionMap.OnClick += OnClick;

        using var registration = cancellationToken.Register(
            () => tcs.TrySetCanceled(cancellationToken));

        try
        {
            return await tcs.Task;
        }
        finally
        {
            actionMap.OnClick -= OnClick;
        }
    }

    bool TryTransformScreenToSimulation(out Vector2 local)
    {
        IMapService maps = ServiceLocator.Current.GetService<IMapService>();
        return maps.TryScreenPositionToMapPosition(Map.MapCoordinateSystem.CENTER_LEFT, out local);
    }

    public bool IsHover(in Vector2 position)
    {
        return true;
    }

    public void OnGainedInputFocus(InputActionMap inputMap)
    {
        _focused = true;
        CancelGather();
        StartGather(inputMap);
    }

    public void OnLostInputFocus(InputActionMap input)
    {
        _focused = false;
        CancelGather();
    }
}
