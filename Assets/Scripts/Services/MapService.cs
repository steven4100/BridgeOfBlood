using UnityEngine;

/// <summary>
/// Plain holder for <see cref="IMapService"/> registered at session bootstrap.
/// Screen hits are camera rays against the playfield plane published by <see cref="SimulationRectRenderer"/>.
/// </summary>
public sealed class MapService : IMapService
{
    Matrix4x4 _localToWorld = Matrix4x4.identity;
    Vector3 _playfieldForward = Vector3.forward;
    bool _hasPlayfieldTransform;

    public Map Map { get; }
    public Camera Camera { get; }
    public Matrix4x4 LocalToWorld => _localToWorld;
    public Vector3 PlayfieldForward => _playfieldForward;
    public bool HasPlayfieldTransform => _hasPlayfieldTransform;

    public Bounds WorldBounds
    {
        get
        {
            float halfHeight = Map.height * 0.5f;
            Vector3 bottomLeft = _localToWorld.MultiplyPoint3x4(new Vector3(0f, -halfHeight, 0f));
            Vector3 bottomRight = _localToWorld.MultiplyPoint3x4(new Vector3(Map.width, -halfHeight, 0f));
            Vector3 topRight = _localToWorld.MultiplyPoint3x4(new Vector3(Map.width, halfHeight, 0f));
            Vector3 topLeft = _localToWorld.MultiplyPoint3x4(new Vector3(0f, halfHeight, 0f));
            var bounds = new Bounds(bottomLeft, Vector3.zero);
            bounds.Encapsulate(bottomRight);
            bounds.Encapsulate(topRight);
            bounds.Encapsulate(topLeft);
            bounds.Expand(50f);
            return bounds;
        }
    }

    public MapService(Map map, Camera camera)
    {
        Map = map;
        Camera = camera;
    }

    public void SetPlayfieldTransform(Matrix4x4 localToWorld, Vector3 forward)
    {
        _localToWorld = localToWorld;
        _playfieldForward = forward.sqrMagnitude > 1e-8f ? forward.normalized : Vector3.forward;
        _hasPlayfieldTransform = true;
    }

    public bool TryScreenPositionToMapPosition(Map.MapCoordinateSystem mapCoordinateSystem, out Vector2 pos)
    {
        pos = default;
        if (Camera == null || !_hasPlayfieldTransform)
            return false;

        if (!TryScreenToPlayfield(Input.mousePosition, out Vector2 playfield))
            return false;

        pos = Map.FromPlayfield(mapCoordinateSystem, playfield);
        return true;
    }

    bool TryScreenToPlayfield(Vector2 screenPosition, out Vector2 playfield)
    {
        playfield = default;
        Ray ray = Camera.ScreenPointToRay(screenPosition);
        Vector3 origin = _localToWorld.MultiplyPoint3x4(Vector3.zero);
        Vector3 normal = _playfieldForward;
        if (!new Plane(normal, origin).Raycast(ray, out float enter)
            && !new Plane(-normal, origin).Raycast(ray, out enter))
        {
            return false;
        }

        Vector3 world = ray.GetPoint(enter);
        Vector3 local = _localToWorld.inverse.MultiplyPoint3x4(world);
        playfield = new Vector2(local.x, local.y);
        return true;
    }
}

public class Map
{
    public float width;
    public float height;

    public enum MapCoordinateSystem
    {
        /// <summary>Origin at the center. +X right, +Y up.</summary>
        CENTER,
        /// <summary>Origin at the left edge, vertically centered. +X right, +Y up. This is simulation playfield space.</summary>
        CENTER_LEFT,
        /// <summary>Origin at the bottom-left corner. +X right, +Y up.</summary>
        BOTTOM_LEFT,
        /// <summary>Origin at the bottom-right corner. +X right (left edge is -width), +Y up.</summary>
        BOTTOM_RIGHT,
    }

    public Map(float width, float height)
    {
        this.width = width;
        this.height = height;
    }

    public Vector2 TransformPoint(MapCoordinateSystem from, MapCoordinateSystem to, Vector2 position)
    {
        if (from == to)
            return position;

        Vector2 center = ToCenter(from, position);
        return FromCenter(to, center);
    }

    /// <summary>
    /// Simulation playfield space: <see cref="MapCoordinateSystem.CENTER_LEFT"/>.
    /// </summary>
    public Vector2 ToPlayfield(MapCoordinateSystem from, Vector2 position)
    {
        return TransformPoint(from, MapCoordinateSystem.CENTER_LEFT, position);
    }

    public Vector2 FromPlayfield(MapCoordinateSystem to, Vector2 playfieldPoint)
    {
        return TransformPoint(MapCoordinateSystem.CENTER_LEFT, to, playfieldPoint);
    }

    /// <summary>
    /// Half-open playfield bounds, matching <see cref="Rect.Contains"/> on the simulation rect.
    /// </summary>
    public bool Contains(MapCoordinateSystem system, Vector2 position)
    {
        Vector2 playfield = ToPlayfield(system, position);
        float halfHeight = height * 0.5f;
        return playfield.x >= 0f
            && playfield.x < width
            && playfield.y >= -halfHeight
            && playfield.y < halfHeight;
    }

    Vector2 ToCenter(MapCoordinateSystem from, Vector2 position)
    {
        switch (from)
        {
            case MapCoordinateSystem.CENTER_LEFT:
                return new Vector2(position.x - width * 0.5f, position.y);
            case MapCoordinateSystem.BOTTOM_LEFT:
                return new Vector2(position.x - width * 0.5f, position.y - height * 0.5f);
            case MapCoordinateSystem.BOTTOM_RIGHT:
                return new Vector2(position.x + width * 0.5f, position.y - height * 0.5f);
            default:
                return position;
        }
    }

    Vector2 FromCenter(MapCoordinateSystem to, Vector2 center)
    {
        switch (to)
        {
            case MapCoordinateSystem.CENTER_LEFT:
                return new Vector2(center.x + width * 0.5f, center.y);
            case MapCoordinateSystem.BOTTOM_LEFT:
                return new Vector2(center.x + width * 0.5f, center.y + height * 0.5f);
            case MapCoordinateSystem.BOTTOM_RIGHT:
                return new Vector2(center.x - width * 0.5f, center.y + height * 0.5f);
            default:
                return center;
        }
    }
}
