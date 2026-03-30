#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BridgeOfBlood.Editor
{
	/// <summary>
	/// Object picker limited to <see cref="ScriptableObject"/> assets whose type implements a given interface.
	/// Unity's default object selector cannot filter by interface.
	/// </summary>
	public sealed class ScriptableObjectImplementsPickerWindow : EditorWindow
	{
		const float RowHeight = 20f;

		static readonly Dictionary<Type, List<ScriptableObject>> AssetCache = new();

		SerializedObject _serializedObject;
		string _propertyPath;
		Type _interfaceType;
		string _search = "";
		Vector2 _scroll;
		List<ScriptableObject> _filtered = new();

		[InitializeOnLoadMethod]
		static void RegisterProjectChange()
		{
			EditorApplication.projectChanged += ClearCache;
		}

		static void ClearCache()
		{
			AssetCache.Clear();
		}

		public static void Show(SerializedProperty property, Type interfaceType)
		{
			if (property == null || interfaceType == null || !interfaceType.IsInterface)
				return;

			var win = CreateInstance<ScriptableObjectImplementsPickerWindow>();
			win._serializedObject = property.serializedObject;
			win._propertyPath = property.propertyPath;
			win._interfaceType = interfaceType;
			win.titleContent = new GUIContent($"Pick {interfaceType.Name}");
			win.minSize = new Vector2(360, 420);
			win.ShowUtility();
			win.RefreshFilter();
		}

		void OnEnable()
		{
			RefreshFilter();
		}

		void OnGUI()
		{
			if (_serializedObject == null || string.IsNullOrEmpty(_propertyPath))
			{
				EditorGUILayout.HelpBox("Invalid selection context.", MessageType.Error);
				return;
			}

			EditorGUILayout.LabelField($"Interface: {_interfaceType.Name}", EditorStyles.miniLabel);

			EditorGUI.BeginChangeCheck();
			_search = EditorGUILayout.TextField("Search", _search);
			if (EditorGUI.EndChangeCheck())
				RefreshFilter();

			EditorGUILayout.Space(4);

			_scroll = EditorGUILayout.BeginScrollView(_scroll);
			for (int i = 0; i < _filtered.Count; i++)
			{
				var so = _filtered[i];
				if (so == null) continue;

				var rowRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.label, GUILayout.Height(RowHeight));
				if (Event.current.type == EventType.Repaint)
					EditorGUI.DrawRect(rowRect, i % 2 == 0 ? new Color(0f, 0f, 0f, 0.03f) : new Color(1f, 1f, 1f, 0.02f));

				var icon = AssetPreview.GetMiniThumbnail(so);
				var labelRect = new Rect(rowRect.x + 2f, rowRect.y, rowRect.width - 4f, rowRect.height);
				if (icon != null)
				{
					GUI.DrawTexture(new Rect(labelRect.x, labelRect.y + 2f, 16f, 16f), icon, ScaleMode.ScaleToFit);
					labelRect.xMin += 20f;
				}
				EditorGUI.LabelField(labelRect, new GUIContent(so.name, AssetDatabase.GetAssetPath(so)), EditorStyles.label);

				if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
					Apply(so);
			}
			EditorGUILayout.EndScrollView();

			EditorGUILayout.Space(4);
			EditorGUILayout.LabelField($"{_filtered.Count} asset(s)", EditorStyles.miniLabel);
		}

		void Apply(ScriptableObject asset)
		{
			var prop = _serializedObject.FindProperty(_propertyPath);
			if (prop != null)
			{
				prop.objectReferenceValue = asset;
				_serializedObject.ApplyModifiedProperties();
			}
			Close();
		}

		void RefreshFilter()
		{
			if (_interfaceType == null)
				return;

			var all = GetAssetsImplementing(_interfaceType);
			_filtered.Clear();

			string q = _search?.Trim() ?? "";
			bool filter = q.Length > 0;

			for (int i = 0; i < all.Count; i++)
			{
				var so = all[i];
				if (so == null) continue;
				if (!filter)
				{
					_filtered.Add(so);
					continue;
				}

				string path = AssetDatabase.GetAssetPath(so);
				if (so.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
				    path.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
					_filtered.Add(so);
			}
		}

		static List<ScriptableObject> GetAssetsImplementing(Type iface)
		{
			if (AssetCache.TryGetValue(iface, out var cached))
				return cached;

			var result = new List<ScriptableObject>();

			// Do not use AssetDatabase.FindAssets("t:TypeName") for discovery: the t: filter is
			// name-based and can return unrelated assets. The only reliable check is the loaded
			// object's runtime type vs. the interface.
			string[] guids = AssetDatabase.FindAssets("", new[] { "Assets" });
			var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			for (int i = 0; i < guids.Length; i++)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[i]);
				if (string.IsNullOrEmpty(path) || !seenPaths.Add(path))
					continue;

				UnityEngine.Object main = AssetDatabase.LoadMainAssetAtPath(path);
				if (main is not ScriptableObject so)
					continue;
				if (!AssetDatabase.IsMainAsset(main))
					continue;
				if (!iface.IsAssignableFrom(so.GetType()))
					continue;

				result.Add(so);
			}

			result.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
			AssetCache[iface] = result;
			return result;
		}
	}
}
#endif
