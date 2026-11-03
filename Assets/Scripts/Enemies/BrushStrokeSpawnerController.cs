using BridgeOfBlood.Data.Shared;
using EZServiceLocation;
using UnityEngine;

/// <summary>
/// Drives <see cref="BrushStrokeEnemySpawner"/> from mouse input, adjusts brush size, and draws the brush preview.
/// Binds zone and the simulation-owned brush spawner from <see cref="ServiceLocator"/> on <see cref="ServicesRegisteredEvent"/>.
/// </summary>
[DefaultExecutionOrder(-100)]
public class BrushStrokeSpawnerController : MonoBehaviour, IDebugDrawable
{
    [Header("Brush size")]
    [SerializeField] float minBrushRadius = 2f;
    [SerializeField] float maxBrushRadius = 80f;
    [SerializeField] float brushRadiusStep = 2f;
    [SerializeField] float scrollRadiusStep = 4f;

    [Header("Input")]
    [SerializeField] KeyCode decreaseBrushKey = KeyCode.LeftBracket;
    [SerializeField] KeyCode increaseBrushKey = KeyCode.RightBracket;
    [SerializeField] int paintMouseButton = 0;

    [Header("Preview")]
    [SerializeField] Color brushOutlineColor = new Color(1f, 0.35f, 0.2f, 0.9f);
    [SerializeField] Color brushFillColor = new Color(1f, 0.35f, 0.2f, 0.12f);
    [SerializeField] int brushCircleSegments = 48;

    IMapService _zoneService;
    BrushStrokeEnemySpawner _brushSpawner;
    bool _hasHover;
    Vector2 _hoverLocal;
    bool _isPainting;

    public BrushStrokeEnemySpawner BrushSpawner => _brushSpawner;

    void OnEnable()
    {
        ServicesRegisteredEvent.SubscribeAndCatchUp(OnServicesRegistered);
    }

    void OnDisable()
    {
        ServicesRegisteredEvent.Unsubscribe(OnServicesRegistered);
        _isPainting = false;
        _hasHover = false;
    }

    void OnServicesRegistered(ref ServicesRegisteredEvent _)
    {
        _zoneService = ServiceLocator.Current.GetService<IMapService>();
        var combat = ServiceLocator.Current.GetService<CombatSimulationController>();
        _brushSpawner = combat.Simulation.Spawner as BrushStrokeEnemySpawner;
    }

    void Update()
    {
        if (_brushSpawner == null || _zoneService == null || !_zoneService.HasPlayfieldTransform)
            return;

        HandleBrushSizeInput();
        UpdateHover();
        HandlePaintInput();
        DrawRuntimeBrushPreview();
    }

    void OnGUI()
    {
        if (_brushSpawner == null)
            return;

        const int pad = 10;
        var rect = new Rect(pad, pad, 280f, 52f);
        GUI.Box(rect, GUIContent.none);
        GUI.Label(
            new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 20f),
            $"Brush radius: {_brushSpawner.BrushRadius:0.#}");
        GUI.Label(
            new Rect(rect.x + 8f, rect.y + 26f, rect.width - 16f, 20f),
            "[ / ] or scroll — size   |   LMB drag — paint");
    }

    void HandleBrushSizeInput()
    {
        float step = brushRadiusStep;
        if (Input.GetKey(decreaseBrushKey))
            _brushSpawner.BrushRadius -= step * Time.deltaTime * 10f;
        if (Input.GetKey(increaseBrushKey))
            _brushSpawner.BrushRadius += step * Time.deltaTime * 10f;

        if (Input.GetKeyDown(decreaseBrushKey))
            _brushSpawner.BrushRadius -= step;
        if (Input.GetKeyDown(increaseBrushKey))
            _brushSpawner.BrushRadius += step;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
            _brushSpawner.BrushRadius += scroll * scrollRadiusStep;

        _brushSpawner.BrushRadius = Mathf.Clamp(_brushSpawner.BrushRadius, minBrushRadius, maxBrushRadius);
    }

    void UpdateHover()
    {
        _hasHover = TryGetPlayfieldLocal(out _hoverLocal);
    }

    void HandlePaintInput()
    {
        if (!TryGetPlayfieldLocal(out Vector2 local))
        {
            if (_isPainting)
            {
                _brushSpawner.EndStroke();
                _isPainting = false;
            }
            return;
        }

        if (Input.GetMouseButtonDown(paintMouseButton))
        {
            _brushSpawner.BeginStroke();
            _brushSpawner.TryAddStrokeSample(local);
            _isPainting = true;
        }
        else if (Input.GetMouseButton(paintMouseButton) && _isPainting)
        {
            _brushSpawner.TryAddStrokeSample(local);
        }
        else if (Input.GetMouseButtonUp(paintMouseButton) && _isPainting)
        {
            _brushSpawner.EndStroke();
            _isPainting = false;
        }
    }

    bool TryGetPlayfieldLocal(out Vector2 localPoint)
    {
        localPoint = default;
        return _zoneService.TryScreenPositionToMapPosition(Map.MapCoordinateSystem.CENTER_LEFT, out localPoint)
            && _zoneService.Map.Contains(Map.MapCoordinateSystem.CENTER_LEFT, localPoint);
    }

    void OnDrawGizmos()
    {
        if (_zoneService == null || !_zoneService.HasPlayfieldTransform)
            return;
        DrawGizmos(_zoneService.LocalToWorld);
    }

    public void DrawGizmos(Transform zoneTransform)
    {
        if (zoneTransform == null)
            return;
        DrawGizmos(zoneTransform.localToWorldMatrix);
    }

    void DrawGizmos(Matrix4x4 playfieldLocalToWorld)
    {
        if (_brushSpawner == null || !_hasHover)
            return;

        DrawBrushCircle(playfieldLocalToWorld, _hoverLocal, _brushSpawner.BrushRadius);
    }

    void DrawBrushCircle(Matrix4x4 playfieldLocalToWorld, Vector2 localCenter, float radius)
    {
        Vector3 worldCenter = playfieldLocalToWorld.MultiplyPoint3x4(new Vector3(localCenter.x, localCenter.y, 0f));
        float scale = playfieldLocalToWorld.GetColumn(0).magnitude;
        float worldRadius = radius * scale;

        Gizmos.color = brushFillColor;
        DrawDiscGizmo(worldCenter, worldRadius, brushCircleSegments, filled: true);

        Gizmos.color = brushOutlineColor;
        DrawDiscGizmo(worldCenter, worldRadius, brushCircleSegments, filled: false);
    }

    void DrawRuntimeBrushPreview()
    {
        if (!_hasHover || !_zoneService.HasPlayfieldTransform)
            return;

        Matrix4x4 playfieldLocalToWorld = _zoneService.LocalToWorld;
        Vector3 center = playfieldLocalToWorld.MultiplyPoint3x4(new Vector3(_hoverLocal.x, _hoverLocal.y, 0f));
        float radius = _brushSpawner.BrushRadius * playfieldLocalToWorld.GetColumn(0).magnitude;
        DrawWorldCircle(center, radius, brushCircleSegments, brushOutlineColor);
    }

    static void DrawWorldCircle(Vector3 center, float radius, int segments, Color color)
    {
        if (segments < 8)
            segments = 8;

        float step = 2f * Mathf.PI / segments;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float a = step * i;
            Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            Debug.DrawLine(prev, next, color);
            prev = next;
        }
    }

    static void DrawDiscGizmo(Vector3 center, float radius, int segments, bool filled)
    {
        if (segments < 8)
            segments = 8;

        float step = 2f * Mathf.PI / segments;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float a = step * i;
            Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            if (filled && i > 1)
                Gizmos.DrawLine(center, next);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
