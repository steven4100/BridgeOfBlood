using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Arms a modifier definition, maps LMB input through the simulation-zone transform, and appends
/// committed runtime modifiers through the supplied callback.
/// </summary>
public sealed class MapModifierPlacementController : MonoBehaviour
{
    const int CircleSegments = 48;
    const float LineWidth = 0.8f;

    static readonly Color PreviewColor = new Color(0.25f, 0.8f, 1f, 1f);
    static readonly Color BlockedColor = new Color(1f, 0.25f, 0.2f, 1f);
    static readonly Color PlacedAttackColor = new Color(0.25f, 0.65f, 1f, 1f);
    static readonly Color PlacedCombineColor = new Color(1f, 0.25f, 0.2f, 1f);
    static readonly Color PlacedMapObjectColor = new Color(0.75f, 0.55f, 1f, 1f);

    Mesh _circleMesh;
    Mesh _quadMesh;
    Material _previewMaterial;
    MaterialPropertyBlock _previewProps;

    ISimulationZoneService _zoneService;
    IReadOnlyList<MapModifier> _placedModifiers;
    Action<MapModifier> _onCommitted;
    Func<Vector2, float, bool> _footprintOverlapsEnemy;
    MapModifierDefinition _activeDefinition;
    IEmitterController _activeEmitterController;
    bool _placementEnabled;
    bool _pointerBlocked;
    bool _previewBlocked;

    public MapModifierDefinition ActiveDefinition => _activeDefinition;

    public void Initialize(
        ISimulationZoneService zoneService,
        IReadOnlyList<MapModifier> placedModifiers,
        Action<MapModifier> onCommitted,
        Func<Vector2, float, bool> footprintOverlapsEnemy)
    {
        _zoneService = zoneService;
        _placedModifiers = placedModifiers;
        _onCommitted = onCommitted;
        _footprintOverlapsEnemy = footprintOverlapsEnemy;
        _placementEnabled = true;
    }

    public void SetPlacementEnabled(bool enabled)
    {
        _placementEnabled = enabled;
    }

    public void SetPointerBlocked(bool blocked)
    {
        _pointerBlocked = blocked;
    }

    public void Disarm()
    {
        _activeDefinition = null;
        _activeEmitterController = null;
    }

    public void Arm(MapModifierDefinition definition)
    {
        _activeDefinition = definition;
        switch (definition.PlacementMode)
        {
            case MapModifierPlacementMode.Directed:
                _activeEmitterController = new DirectedEmitterController();
                break;
            case MapModifierPlacementMode.Region:
                _activeEmitterController = new RegionEmitterController(definition.DefaultRadius);
                break;
            default:
                _activeEmitterController = new PositionEmitterController();
                break;
        }
    }

    void Update()
    {
        if (!_placementEnabled)
            return;

        if (_activeEmitterController != null)
            HandlePlacementInput();

        EnsurePreviewResources();
        DrawPlacedModifiers();
        DrawActivePreview();
    }

    void HandlePlacementInput()
    {
        RectTransform zone = _zoneService.Zone;
        bool mapped = SimulationZonePointer.TryGetLocalPoint(
            zone,
            _zoneService.Camera,
            Input.mousePosition,
            out Vector2 local);
        bool inside = !_pointerBlocked && mapped && zone.rect.Contains(local);
        var input = new EmitterPointerInput(
            local,
            inside,
            Input.GetMouseButtonDown(0),
            Input.GetMouseButton(0),
            Input.GetMouseButtonUp(0),
            Input.mouseScrollDelta.y);

        bool blocked = false;
        if (inside && _activeDefinition is MapObjectMapModifierDefinition mapObject)
            blocked = _footprintOverlapsEnemy(local, mapObject.Radius);
        _previewBlocked = blocked;

        if (_activeEmitterController.TryCommit(in input, out MapModifierPlacement placement) && !blocked)
            _onCommitted(_activeDefinition.CreateRuntime(in placement));
    }

    void DrawPlacedModifiers()
    {
        for (int i = 0; i < _placedModifiers.Count; i++)
        {
            MapModifier modifier = _placedModifiers[i];
            if (modifier is EnemyCombineMapModifier combine)
            {
                DrawLocalCircle(combine.Center, combine.Radius, PlacedCombineColor);
            }
            else if (modifier is AttackEntitySpawnMapModifier attack)
            {
                DrawAttackModifier(attack, PlacedAttackColor);
            }
            else if (modifier is MapObjectMapModifier mapObject)
            {
                DrawLocalCircle(mapObject.Position, mapObject.Radius, PlacedMapObjectColor);
            }
        }
    }

    void DrawActivePreview()
    {
        if (_activeDefinition == null
            || _activeEmitterController == null
            || !_activeEmitterController.HasPreview)
        {
            return;
        }

        MapModifierPlacement preview = _activeEmitterController.Preview;
        if (_activeDefinition is MapObjectMapModifierDefinition mapObjectDefinition)
        {
            DrawLocalCircle(
                preview.Origin,
                mapObjectDefinition.Radius,
                _previewBlocked ? BlockedColor : PreviewColor);
        }
        else if (_activeDefinition is AttackEntitySpawnMapModifierDefinition attackDefinition)
        {
            DrawAttackFootprint(
                preview.Origin,
                attackDefinition.AttackEntityData,
                PreviewColor,
                preview.Direction);
        }

        if (_activeDefinition.PlacementMode == MapModifierPlacementMode.Directed)
            DrawLocalArrow(preview.Origin, preview.Direction, PreviewColor);
        else if (_activeDefinition.PlacementMode == MapModifierPlacementMode.Region)
            DrawLocalCircle(preview.Origin, preview.Radius, PreviewColor);
    }

    void DrawAttackModifier(AttackEntitySpawnMapModifier modifier, Color color)
    {
        if (modifier.Emitter is DirectedAttackEntityEmitter directed)
        {
            DrawAttackFootprint(directed.Position, modifier.AttackEntityData, color, directed.Direction);
            DrawLocalArrow(directed.Position, directed.Direction, color);
            return;
        }

        DrawAttackFootprint(modifier.Emitter.Position, modifier.AttackEntityData, color, Vector2.right);
    }

    void DrawAttackFootprint(Vector2 origin, AttackEntityData data, Color color, Vector2 direction)
    {
        HitBoxBehavior hitBox = data.GetBehavior<HitBoxBehavior>();
        if (hitBox == null)
        {
            DrawLocalCircle(origin, 1f, color);
            return;
        }

        HitBoxData shape = hitBox.hitBoxData;
        if (shape.isSphere)
            DrawLocalCircle(origin, shape.sphereRadius, color);
        else if (shape.isRect)
            DrawLocalRectangle(origin, shape.rectDimension, direction, color);
    }

    void DrawLocalArrow(Vector2 origin, Vector2 direction, Color color)
    {
        Vector2 normalized = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector2.right;
        float length = Mathf.Max(4f, direction.magnitude);
        Vector2 end = origin + normalized * length;
        DrawLocalLine(origin, end, color);
        DrawLocalLine(end, end - Rotate(normalized, 30f) * 2f, color);
        DrawLocalLine(end, end - Rotate(normalized, -30f) * 2f, color);
    }

    void DrawLocalCircle(Vector2 center, float radius, Color color)
    {
        Color fill = color;
        fill.a = 0.22f;
        DrawMesh(
            _circleMesh,
            center,
            Quaternion.identity,
            new Vector3(radius * 2f, radius * 2f, 1f),
            fill);

        float step = 2f * Mathf.PI / CircleSegments;
        Vector2 previous = center + Vector2.right * radius;
        for (int i = 1; i <= CircleSegments; i++)
        {
            float angle = step * i;
            Vector2 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            DrawLocalLine(previous, next, color);
            previous = next;
        }
    }

    void DrawLocalRectangle(Vector2 center, Vector2 size, Vector2 direction, Color color)
    {
        Vector2 facing = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

        Color fill = color;
        fill.a = 0.22f;
        DrawMesh(_quadMesh, center, rotation, new Vector3(size.x, size.y, 1f), fill);

        Vector2 half = size * 0.5f;
        Vector2 right = facing * half.x;
        Vector2 up = new Vector2(-facing.y, facing.x) * half.y;
        Vector2 a = center - right - up;
        Vector2 b = center + right - up;
        Vector2 c = center + right + up;
        Vector2 d = center - right + up;
        DrawLocalLine(a, b, color);
        DrawLocalLine(b, c, color);
        DrawLocalLine(c, d, color);
        DrawLocalLine(d, a, color);
    }

    void DrawLocalLine(Vector2 from, Vector2 to, Color color)
    {
        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length <= 0.0001f)
            return;

        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        DrawMesh(
            _quadMesh,
            (from + to) * 0.5f,
            Quaternion.Euler(0f, 0f, angle),
            new Vector3(length, LineWidth, 1f),
            color);
    }

    void DrawMesh(Mesh mesh, Vector2 localPosition, Quaternion localRotation, Vector3 localScale, Color color)
    {
        EnsurePreviewResources();
        RectTransform zone = _zoneService.Zone;
        Camera camera = _zoneService.Camera;
        Matrix4x4 localToWorld = zone.localToWorldMatrix;
        localToWorld *= Matrix4x4.Translate(-zone.forward * 0.05f);
        Matrix4x4 matrix = localToWorld * Matrix4x4.TRS(
            new Vector3(localPosition.x, localPosition.y, 0f),
            localRotation,
            localScale);
        _previewProps.SetColor("_Color", color);
        Graphics.DrawMesh(
            mesh,
            matrix,
            _previewMaterial,
            0,
            camera,
            0,
            _previewProps,
            ShadowCastingMode.Off,
            false);
    }

    void EnsurePreviewResources()
    {
        if (_previewMaterial != null)
            return;

        _circleMesh = CreateCircleMesh();
        _quadMesh = CreateQuadMesh();
        _previewProps = new MaterialPropertyBlock();

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("UI/Default");
        _previewMaterial = new Material(shader)
        {
            name = "MapModifierPreview",
            color = Color.white
        };
    }

    void OnDestroy()
    {
        if (_circleMesh != null)
            Destroy(_circleMesh);
        if (_quadMesh != null)
            Destroy(_quadMesh);
        if (_previewMaterial != null)
            Destroy(_previewMaterial);
        _circleMesh = null;
        _quadMesh = null;
        _previewMaterial = null;
    }

    static Mesh CreateCircleMesh()
    {
        var mesh = new Mesh { name = "MapModifierPreviewCircle" };
        int vertCount = CircleSegments + 1;
        var verts = new Vector3[vertCount];
        var tris = new int[CircleSegments * 3];

        verts[0] = Vector3.zero;
        for (int i = 0; i < CircleSegments; i++)
        {
            float angle = (i / (float)CircleSegments) * Mathf.PI * 2f;
            verts[i + 1] = new Vector3(Mathf.Cos(angle) * 0.5f, Mathf.Sin(angle) * 0.5f, 0f);
        }

        for (int i = 0; i < CircleSegments; i++)
        {
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = (i + 1) % CircleSegments + 1;
        }

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh CreateQuadMesh()
    {
        var mesh = new Mesh { name = "MapModifierPreviewQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }

    static Vector2 Rotate(Vector2 value, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(
            value.x * cos - value.y * sin,
            value.x * sin + value.y * cos);
    }
}
