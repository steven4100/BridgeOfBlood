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

    ISimulationZoneService _zoneService;
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
        _zoneService = ServiceLocator.Current.GetService<ISimulationZoneService>();
        var combat = ServiceLocator.Current.GetService<CombatSimulationController>();
        _brushSpawner = combat.Simulation.Spawner as BrushStrokeEnemySpawner;
    }

    void Update()
    {
        if (_brushSpawner == null || _zoneService == null || _zoneService.Zone == null)
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
        RectTransform zone = _zoneService.Zone;
        _hasHover = TryGetPlayfieldLocal(Input.mousePosition, out _hoverLocal)
            && zone.rect.Contains(_hoverLocal);
    }

    void HandlePaintInput()
    {
        RectTransform zone = _zoneService.Zone;
        if (!TryGetPlayfieldLocal(Input.mousePosition, out Vector2 local)
            || !zone.rect.Contains(local))
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

    bool TryGetPlayfieldLocal(Vector2 screenPosition, out Vector2 localPoint) =>
        SimulationZonePointer.TryGetLocalPoint(_zoneService.Zone, _zoneService.Camera, screenPosition, out localPoint);

    void OnDrawGizmos()
    {
        if (_zoneService?.Zone == null)
            return;
        DrawGizmos(_zoneService.Zone.transform);
    }

    public void DrawGizmos(Transform zoneTransform)
    {
        if (_brushSpawner == null || _zoneService?.Zone == null || !_hasHover)
            return;

        DrawBrushCircle(zoneTransform, _hoverLocal, _brushSpawner.BrushRadius);
    }

    void DrawBrushCircle(Transform zoneTransform, Vector2 localCenter, float radius)
    {
        Vector3 worldCenter = zoneTransform.TransformPoint(new Vector3(localCenter.x, localCenter.y, 0f));
        float scale = zoneTransform.lossyScale.x;
        float worldRadius = radius * scale;

        Gizmos.color = brushFillColor;
        DrawDiscGizmo(worldCenter, worldRadius, brushCircleSegments, filled: true);

        Gizmos.color = brushOutlineColor;
        DrawDiscGizmo(worldCenter, worldRadius, brushCircleSegments, filled: false);
    }

    void DrawRuntimeBrushPreview()
    {
        RectTransform zone = _zoneService.Zone;
        if (!_hasHover || zone == null)
            return;

        Transform t = zone.transform;
        Vector3 center = t.TransformPoint(new Vector3(_hoverLocal.x, _hoverLocal.y, 0f));
        float radius = _brushSpawner.BrushRadius * t.lossyScale.x;
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
