using UnityEngine;

public readonly struct EmitterPointerInput
{
    public readonly Vector2 LocalPosition;
    public readonly bool IsInside;
    public readonly bool Pressed;
    public readonly bool Held;
    public readonly bool Released;
    public readonly float ScrollDelta;

    public EmitterPointerInput(
        Vector2 localPosition,
        bool isInside,
        bool pressed,
        bool held,
        bool released,
        float scrollDelta)
    {
        LocalPosition = localPosition;
        IsInside = isInside;
        Pressed = pressed;
        Held = held;
        Released = released;
        ScrollDelta = scrollDelta;
    }
}

/// <summary>Converts pointer state into the placement data needed by one definition.</summary>
public interface IEmitterController
{
    bool HasPreview { get; }
    MapModifierPlacement Preview { get; }
    bool TryCommit(in EmitterPointerInput input, out MapModifierPlacement placement);
}

public sealed class PositionEmitterController : IEmitterController
{
    MapModifierPlacement _preview;
    public bool HasPreview { get; private set; }
    public MapModifierPlacement Preview => _preview;

    public bool TryCommit(in EmitterPointerInput input, out MapModifierPlacement placement)
    {
        HasPreview = input.IsInside;
        if (HasPreview)
            _preview = new MapModifierPlacement(input.LocalPosition, Vector2.right, 0f);

        placement = _preview;
        return input.IsInside && input.Pressed;
    }
}

public sealed class DirectedEmitterController : IEmitterController
{
    bool _hasOrigin;
    Vector2 _origin;
    MapModifierPlacement _preview;

    public bool HasPreview { get; private set; }
    public MapModifierPlacement Preview => _preview;

    public bool TryCommit(in EmitterPointerInput input, out MapModifierPlacement placement)
    {
        if (!_hasOrigin)
        {
            HasPreview = input.IsInside;
            if (HasPreview)
                _preview = new MapModifierPlacement(input.LocalPosition, Vector2.right, 0f);

            if (input.IsInside && input.Pressed)
            {
                _origin = input.LocalPosition;
                _hasOrigin = true;
                _preview = new MapModifierPlacement(_origin, Vector2.right, 0f);
            }

            placement = default;
            return false;
        }

        Vector2 direction = input.LocalPosition - _origin;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector2.right;

        HasPreview = true;
        _preview = new MapModifierPlacement(_origin, direction, 0f);

        if (!input.IsInside || !input.Pressed)
        {
            placement = default;
            return false;
        }

        _hasOrigin = false;
        placement = _preview;
        return true;
    }
}

public sealed class RegionEmitterController : IEmitterController
{
    const float MinRadius = 0.5f;
    const float ScrollRadiusStep = 2f;

    float _radius;
    MapModifierPlacement _preview;

    public bool HasPreview { get; private set; }
    public MapModifierPlacement Preview => _preview;

    public RegionEmitterController(float radius)
    {
        _radius = Mathf.Max(MinRadius, radius);
    }

    public bool TryCommit(in EmitterPointerInput input, out MapModifierPlacement placement)
    {
        if (input.IsInside && Mathf.Abs(input.ScrollDelta) > 0.01f)
            _radius = Mathf.Max(MinRadius, _radius + input.ScrollDelta * ScrollRadiusStep);

        HasPreview = input.IsInside;
        if (HasPreview)
            _preview = new MapModifierPlacement(input.LocalPosition, Vector2.right, _radius);

        placement = _preview;
        return input.IsInside && input.Pressed;
    }
}
