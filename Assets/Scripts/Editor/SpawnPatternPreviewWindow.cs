#if UNITY_EDITOR
using System.Collections.Generic;
using BridgeOfBlood.Data.Enemies;
using UnityEditor;
using UnityEngine;

namespace BridgeOfBlood.Editor
{
	public class SpawnPatternPreviewWindow : EditorWindow
	{
		[SerializeField] private SpawnPattern _pattern;
		[SerializeField] private Vector2 _previewOrigin;
		private readonly List<Vector2> _points = new List<Vector2>();
		private const uint PreviewSeed = 12345u;

		[MenuItem("Window/Bridge of Blood/Spawn Pattern Preview")]
		public static void OpenFromMenu() => ShowWindow(null);

		public static void ShowWindow(SpawnPattern pattern = null)
		{
			var w = GetWindow<SpawnPatternPreviewWindow>("Spawn Pattern Preview");
			w.minSize = new Vector2(320, 360);
			if (pattern != null)
				w._pattern = pattern;
			w.RefreshPoints();
		}

		void OnGUI()
		{
			EditorGUILayout.LabelField("Spawn Pattern Preview", EditorStyles.boldLabel);
			EditorGUI.BeginChangeCheck();
			_pattern = (SpawnPattern)EditorGUILayout.ObjectField("Pattern", _pattern, typeof(SpawnPattern), false);
			if (EditorGUI.EndChangeCheck())
				RefreshPoints();

			if (_pattern == null)
			{
				EditorGUILayout.HelpBox("Assign a Spawn Pattern asset to preview.", MessageType.Info);
				return;
			}

			_previewOrigin = EditorGUILayout.Vector2Field("Preview origin", _previewOrigin);
			if (GUILayout.Button("Refresh"))
				RefreshPoints();

			EditorGUILayout.LabelField("Point count", _points.Count.ToString());

			Rect rect = GUILayoutUtility.GetRect(256, 256);
			if (rect.width > 0 && rect.height > 0 && Event.current.type == EventType.Repaint)
				DrawPreview(rect);
		}

		void RefreshPoints()
		{
			_points.Clear();
			if (_pattern != null)
				_pattern.GetPositions(_previewOrigin, _points, PreviewSeed);
		}

		void GetWorldBounds(out float minX, out float minY, out float maxX, out float maxY)
		{
			minX = minY = float.MaxValue;
			maxX = maxY = float.MinValue;
			Vector2 o = _previewOrigin;
			if (_pattern.fillShapes != null)
				foreach (var s in _pattern.fillShapes)
				{
					s.GetBounds(out float a, out float b, out float c, out float d);
					minX = Mathf.Min(minX, o.x + a); minY = Mathf.Min(minY, o.y + b);
					maxX = Mathf.Max(maxX, o.x + c); maxY = Mathf.Max(maxY, o.y + d);
				}
			if (_pattern.omissionZones != null)
				foreach (var s in _pattern.omissionZones)
				{
					s.GetBounds(out float a, out float b, out float c, out float d);
					minX = Mathf.Min(minX, o.x + a); minY = Mathf.Min(minY, o.y + b);
					maxX = Mathf.Max(maxX, o.x + c); maxY = Mathf.Max(maxY, o.y + d);
				}
			foreach (var p in _points)
			{
				minX = Mathf.Min(minX, p.x); minY = Mathf.Min(minY, p.y);
				maxX = Mathf.Max(maxX, p.x); maxY = Mathf.Max(maxY, p.y);
			}
			if (minX == float.MaxValue) { minX = -10f; minY = -10f; maxX = 10f; maxY = 10f; }
			float rangeX = maxX - minX; float rangeY = maxY - minY;
			if (rangeX < 2f) { minX -= 1f; maxX += 1f; }
			if (rangeY < 2f) { minY -= 1f; maxY += 1f; }
		}

		void DrawPreview(Rect rect)
		{
			GetWorldBounds(out float minX, out float minY, out float maxX, out float maxY);
			float rangeX = maxX - minX;
			float rangeY = maxY - minY;
			if (rangeX <= 0f || rangeY <= 0f) return;

			// Draw in rect: world (minX, minY)-(maxX, maxY) maps to (0,0)-(width, height), Y flipped
			GUI.BeginGroup(rect);
			Vector3 scale = new Vector3(rect.width / rangeX, -rect.height / rangeY, 1f);
			Vector3 translation = new Vector3(-minX * scale.x, rect.height - minY * scale.y, 0f);
			Matrix4x4 oldMatrix = Handles.matrix;
			Handles.BeginGUI();
			Handles.matrix = Matrix4x4.TRS(translation, Quaternion.identity, scale);

			Vector2 o = _previewOrigin;

			// Fill shapes (green wire)
			Handles.color = new Color(0.2f, 0.85f, 0.2f, 1f);
			if (_pattern.fillShapes != null)
				foreach (var s in _pattern.fillShapes)
					DrawShape(s, o);

			// Omission zones (red wire)
			Handles.color = new Color(0.9f, 0.25f, 0.25f, 1f);
			if (_pattern.omissionZones != null)
				foreach (var s in _pattern.omissionZones)
					DrawShape(s, o);

			// Points (cyan discs)
			Handles.color = new Color(0.3f, 0.85f, 1f, 1f);
			float dotRadius = Mathf.Max(0.5f, Mathf.Min(rangeX, rangeY) * 0.03f);
			foreach (var p in _points)
				Handles.DrawSolidDisc(new Vector3(p.x, p.y, 0f), Vector3.forward, dotRadius);

			Handles.matrix = oldMatrix;
			Handles.EndGUI();
			GUI.EndGroup();
		}

		void DrawShape(SpawnShape shape, Vector2 origin)
		{
			Vector3 o3 = new Vector3(origin.x, origin.y, 0f);
			switch (shape.type)
			{
				case SpawnShapeType.Circle:
					Handles.DrawWireDisc(o3 + new Vector3(shape.center.x, shape.center.y, 0f), Vector3.forward, shape.size.x);
					break;
				case SpawnShapeType.Rectangle:
				case SpawnShapeType.Triangle:
					int n = shape.type == SpawnShapeType.Rectangle ? 4 : 3;
					Vector2[] corners = new Vector2[n];
					shape.GetCorners(corners);
					for (int i = 0; i < n; i++)
					{
						Vector3 a = o3 + new Vector3(corners[i].x, corners[i].y, 0f);
						Vector3 b = o3 + new Vector3(corners[(i + 1) % n].x, corners[(i + 1) % n].y, 0f);
						Handles.DrawLine(a, b);
					}
					break;
			}
		}
	}
}
#endif
