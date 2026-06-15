using UnityEngine;

/// <summary>
/// Drives <see cref="BrushStrokeEnemySpawner"/> from mouse input, adjusts brush size, and draws the brush preview.
/// Wire <see cref="simulationZone"/> and assign <see cref="brushSpawner"/> from the active <see cref="SimulationConfig"/>.
/// </summary>
[DefaultExecutionOrder(-100)]
public class BrushStrokeSpawnerController : MonoBehaviour, IDebugDrawable
{
    [SerializeField] RectTransform simulationZone;
    [SerializeField] Camera renderCamera;
    [SerializeField] BrushStrokeEnemySpawner brushSpawner;

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

    bool _hasHover;
    Vector2 _hoverLocal;
    bool _isPainting;

    public BrushStrokeEnemySpawner BrushSpawner => brushSpawner;

    public void SetBrushSpawner(BrushStrokeEnemySpawner spawner) => brushSpawner = spawner;

    public void Bind(RectTransform zone, Camera camera)
    {
        simulationZone = zone;
        renderCamera = camera;
    }

    void Update()
    {
        if (brushSpawner == null || simulationZone == null)
            return;

        HandleBrushSizeInput();
        UpdateHover();
        HandlePaintInput();
        DrawRuntimeBrushPreview();
    }

    void OnGUI()
    {
        if (brushSpawner == null)
            return;

        const int pad = 10;
        var rect = new Rect(pad, pad, 280f, 52f);
        GUI.Box(rect, GUIContent.none);
        GUI.Label(
            new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 20f),
            $"Brush radius: {brushSpawner.BrushRadius:0.#}");
        GUI.Label(
            new Rect(rect.x + 8f, rect.y + 26f, rect.width - 16f, 20f),
            "[ / ] or scroll — size   |   LMB drag — paint");
    }

    void HandleBrushSizeInput()
    {
        float step = brushRadiusStep;
        if (Input.GetKey(decreaseBrushKey))
            brushSpawner.BrushRadius -= step * Time.deltaTime * 10f;
        if (Input.GetKey(increaseBrushKey))
            brushSpawner.BrushRadius += step * Time.deltaTime * 10f;

        if (Input.GetKeyDown(decreaseBrushKey))
            brushSpawner.BrushRadius -= step;
        if (Input.GetKeyDown(increaseBrushKey))
            brushSpawner.BrushRadius += step;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
            brushSpawner.BrushRadius += scroll * scrollRadiusStep;

        brushSpawner.BrushRadius = Mathf.Clamp(brushSpawner.BrushRadius, minBrushRadius, maxBrushRadius);
    }

    void UpdateHover()
    {
        _hasHover = TryGetPlayfieldLocal(Input.mousePosition, out _hoverLocal)
            && simulationZone.rect.Contains(_hoverLocal);
    }

    void HandlePaintInput()
    {
        if (!TryGetPlayfieldLocal(Input.mousePosition, out Vector2 local)
            || !simulationZone.rect.Contains(local))
        {
            if (_isPainting)
            {
                brushSpawner.EndStroke();
                _isPainting = false;
            }
            return;
        }

        if (Input.GetMouseButtonDown(paintMouseButton))
        {
            brushSpawner.BeginStroke();
            brushSpawner.TryAddStrokeSample(local);
            _isPainting = true;
        }
        else if (Input.GetMouseButton(paintMouseButton) && _isPainting)
        {
            brushSpawner.TryAddStrokeSample(local);
        }
        else if (Input.GetMouseButtonUp(paintMouseButton) && _isPainting)
        {
            brushSpawner.EndStroke();
            _isPainting = false;
        }
    }

    bool TryGetPlayfieldLocal(Vector2 screenPosition, out Vector2 localPoint) =>
        SimulationZonePointer.TryGetLocalPoint(simulationZone, renderCamera, screenPosition, out localPoint);

    public void DrawGizmos(Transform zoneTransform)
    {
        if (brushSpawner == null || simulationZone == null || !_hasHover)
            return;

        DrawBrushCircle(zoneTransform, _hoverLocal, brushSpawner.BrushRadius);
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
        if (!_hasHover || simulationZone == null)
            return;

        Transform t = simulationZone.transform;
        Vector3 center = t.TransformPoint(new Vector3(_hoverLocal.x, _hoverLocal.y, 0f));
        float radius = brushSpawner.BrushRadius * t.lossyScale.x;
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
