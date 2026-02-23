using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Collections;
using Unity.Mathematics;
using BridgeOfBlood.Data.Spells;

/// <summary>
/// Editor window for previewing spell animations.
/// Accepts a SpellAuthoringData, lets you click to cast, and simulates attack entities in real time.
/// Simulation uses game coordinates (Y up) with origin at center of the preview rect.
/// </summary>
public class SpellAuthoringWindow : EditorWindow
{
    [MenuItem("BridgeOfBlood/Spell Authoring")]
    public static void ShowWindow()
    {
        var window = GetWindow<SpellAuthoringWindow>("Spell Authoring");
        window.minSize = new Vector2(400, 300);
    }

    private SpellAuthoringData _spellData;
    private AttackEntityManager _entityManager;
    private AttackEntityMovementSystem _movementSystem;
    private AttackEntityTimeSystem _timeSystem;

    private bool _isPlaying;
    private double _lastEditorTime;
    private float _simTime;
    private float _simSpeed = 1f;

    private struct ActiveCast
    {
        public float2 origin;
        public float startTime;
        public int nextKeyframeIndex;
    }
    private readonly List<ActiveCast> _activeCasts = new List<ActiveCast>();
    private readonly List<float2> _castMarkers = new List<float2>();

    private Rect _simRect;

    void OnEnable()
    {
        _entityManager = new AttackEntityManager();
        _movementSystem = new AttackEntityMovementSystem();
        _timeSystem = new AttackEntityTimeSystem();
        _lastEditorTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += OnEditorUpdate;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        _entityManager?.Dispose();
    }

    void OnEditorUpdate()
    {
        if (_isPlaying)
            Repaint();
    }

    void OnGUI()
    {
        DrawToolbar();
        LayoutSimulationRect();

        if (_isPlaying)
            TickSimulation();

        HandleInput();
        DrawBackground();
        DrawCastMarkers();
        DrawEntities();
    }

    // ───────────────────────── Toolbar ─────────────────────────

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        _spellData = (SpellAuthoringData)EditorGUILayout.ObjectField(
            _spellData, typeof(SpellAuthoringData), false, GUILayout.Width(200));

        if (GUILayout.Button(_isPlaying ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            _isPlaying = !_isPlaying;
            _lastEditorTime = EditorApplication.timeSinceStartup;
        }

        if (GUILayout.Button("Step", EditorStyles.toolbarButton, GUILayout.Width(40)))
            StepFrame();

        if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(45)))
            ClearSimulation();

        GUILayout.Label("Speed", EditorStyles.miniLabel, GUILayout.Width(36));
        _simSpeed = GUILayout.HorizontalSlider(_simSpeed, 0.1f, 5f, GUILayout.Width(80));
        GUILayout.Label($"{_simSpeed:F1}x", EditorStyles.miniLabel, GUILayout.Width(28));

        GUILayout.FlexibleSpace();
        GUILayout.Label($"Entities: {_entityManager.EntityCount}   t={_simTime:F2}s",
            EditorStyles.miniLabel);

        EditorGUILayout.EndHorizontal();
    }

    // ───────────────────────── Layout ─────────────────────────

    void LayoutSimulationRect()
    {
        _simRect = GUILayoutUtility.GetRect(
            GUIContent.none, GUIStyle.none,
            GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
    }

    // ───────────────────────── Simulation ─────────────────────────

    void TickSimulation()
    {
        double now = EditorApplication.timeSinceStartup;
        float dt = (float)(now - _lastEditorTime) * _simSpeed;
        _lastEditorTime = now;

        dt = Mathf.Min(dt, 0.1f);
        _simTime += dt;

        ProcessCasts();

        var entities = _entityManager.GetEntities();
        if (entities.Length > 0)
        {
            _timeSystem.Tick(entities, dt);
            _movementSystem.MoveEntities(_entityManager.GetEntities(), dt);
            _entityManager.RemoveExpired();
        }
    }

    void ProcessCasts()
    {
        if (_spellData == null || _spellData.SpellAnimation == null) return;
        var keyFrames = _spellData.SpellAnimation.keyFrames;
        if (keyFrames == null || keyFrames.Count == 0) return;

        for (int c = _activeCasts.Count - 1; c >= 0; c--)
        {
            var cast = _activeCasts[c];
            float elapsed = _simTime - cast.startTime;

            while (cast.nextKeyframeIndex < keyFrames.Count
                   && elapsed >= keyFrames[cast.nextKeyframeIndex].time)
            {
                SpawnKeyframeEntities(keyFrames[cast.nextKeyframeIndex], cast.origin);
                cast.nextKeyframeIndex++;
            }

            if (cast.nextKeyframeIndex >= keyFrames.Count)
                _activeCasts.RemoveAt(c);
            else
                _activeCasts[c] = cast;
        }
    }

    void SpawnKeyframeEntities(SpellKeyFrame keyFrame, float2 origin)
    {
        if (keyFrame.entitiesToSpawn == null) return;
        foreach (var data in keyFrame.entitiesToSpawn)
        {
            float2 spawnPos = origin;
            if (data.spawnType == AttackEntitySpawnType.RelativeToPlayer)
            {
                spawnPos += new float2(
                    data.relativeToPlayerSpawnCriteria.offsetFromPlayer.x,
                    data.relativeToPlayerSpawnCriteria.offsetFromPlayer.y);
            }
            _entityManager.Spawn(data, spawnPos);
        }
    }

    void StepFrame()
    {
        _isPlaying = false;
        float dt = (1f / 60f) * _simSpeed;
        _simTime += dt;

        ProcessCasts();

        var entities = _entityManager.GetEntities();
        if (entities.Length > 0)
        {
            _timeSystem.Tick(entities, dt);
            _movementSystem.MoveEntities(_entityManager.GetEntities(), dt);
            _entityManager.RemoveExpired();
        }
        Repaint();
    }

    void ClearSimulation()
    {
        _entityManager.Clear();
        _activeCasts.Clear();
        _castMarkers.Clear();
        _simTime = 0f;
        _isPlaying = false;
        Repaint();
    }

    // ───────────────────────── Input ─────────────────────────

    void HandleInput()
    {
        Event evt = Event.current;

        if (evt.type == EventType.MouseDown && evt.button == 0 && _simRect.Contains(evt.mousePosition))
        {
            if (_spellData != null
                && _spellData.SpellAnimation != null
                && _spellData.SpellAnimation.keyFrames != null
                && _spellData.SpellAnimation.keyFrames.Count > 0)
            {
                float2 simPos = ScreenToSim(evt.mousePosition);
                _activeCasts.Add(new ActiveCast
                {
                    origin = simPos,
                    startTime = _simTime,
                    nextKeyframeIndex = 0
                });
                _castMarkers.Add(simPos);

                if (!_isPlaying)
                {
                    _isPlaying = true;
                    _lastEditorTime = EditorApplication.timeSinceStartup;
                }
            }
            evt.Use();
        }
    }

    // ───────────────────────── Drawing ─────────────────────────

    void DrawBackground()
    {
        if (Event.current.type != EventType.Repaint) return;

        EditorGUI.DrawRect(_simRect, new Color(0.08f, 0.08f, 0.10f));

        Vector2 center = _simRect.center;
        Handles.color = new Color(1f, 1f, 1f, 0.07f);
        Handles.DrawLine(
            new Vector3(_simRect.xMin, center.y, 0),
            new Vector3(_simRect.xMax, center.y, 0));
        Handles.DrawLine(
            new Vector3(center.x, _simRect.yMin, 0),
            new Vector3(center.x, _simRect.yMax, 0));
    }

    void DrawCastMarkers()
    {
        if (Event.current.type != EventType.Repaint) return;

        Handles.color = new Color(1f, 1f, 1f, 0.25f);
        float crossSize = 6f;
        foreach (var marker in _castMarkers)
        {
            Vector2 s = SimToScreen(marker);
            Handles.DrawLine(
                new Vector3(s.x - crossSize, s.y, 0),
                new Vector3(s.x + crossSize, s.y, 0));
            Handles.DrawLine(
                new Vector3(s.x, s.y - crossSize, 0),
                new Vector3(s.x, s.y + crossSize, 0));
        }
    }

    void DrawEntities()
    {
        if (Event.current.type != EventType.Repaint) return;

        var entities = _entityManager.GetEntities();
        for (int i = 0; i < entities.Length; i++)
        {
            var e = entities[i];
            Vector2 screenPos = SimToScreen(e.position);

            if (!_simRect.Contains(screenPos)) continue;

            float scale = e.currentHitBoxScale;
            Color col = EntityColor(e);

            if (e.hitBox.isSphere)
            {
                float radius = e.hitBox.sphereRadius * scale;
                Handles.color = col;
                Handles.DrawSolidDisc(
                    new Vector3(screenPos.x, screenPos.y, 0),
                    Vector3.forward, radius);
            }
            else if (e.hitBox.isRect)
            {
                Vector2 size = e.hitBox.rectDimension * scale;
                Rect r = new Rect(
                    screenPos.x - size.x * 0.5f,
                    screenPos.y - size.y * 0.5f,
                    size.x, size.y);
                EditorGUI.DrawRect(r, col);
            }
            else
            {
                EditorGUI.DrawRect(
                    new Rect(screenPos.x - 3, screenPos.y - 3, 6, 6), col);
            }
        }
    }

    static Color EntityColor(AttackEntity e)
    {
        float lifeFrac = e.lifecycle.maxTimeAlive > 0f
            ? Mathf.Clamp01(1f - e.timeAlive / e.lifecycle.maxTimeAlive)
            : 1f;
        return Color.Lerp(
            new Color(0.8f, 0.15f, 0.05f, 0.25f),
            new Color(1f, 0.45f, 0.1f, 0.9f),
            lifeFrac);
    }

    // ───────────────────────── Coordinate conversion ─────────────────────────
    // Sim space: origin at center of _simRect, +X right, +Y up (game coords).
    // GUI space: origin at top-left of window, +X right, +Y down.

    Vector2 SimToScreen(float2 sim)
    {
        Vector2 center = _simRect.center;
        return new Vector2(center.x + sim.x, center.y - sim.y);
    }

    float2 ScreenToSim(Vector2 screen)
    {
        Vector2 center = _simRect.center;
        return new float2(screen.x - center.x, -(screen.y - center.y));
    }
}
