#if UNITY_EDITOR
using System.Collections.Generic;
using BridgeOfBlood.Data.Shared;
using UnityEditor;
using UnityEngine;

namespace BridgeOfBlood.Editor
{
	public class ProbabilityEditorWindow : EditorWindow
	{
		DefaultAsset _folder;
		Vector2 _scrollPos;
		readonly List<ScriptableObject> _assets = new();
		readonly List<IRandomElement> _elements = new();

		[MenuItem("Tools/Bridge of Blood/Probability Editor")]
		public static void Open()
		{
			var w = GetWindow<ProbabilityEditorWindow>("Probability Editor");
			w.minSize = new Vector2(400, 300);
		}

		void OnGUI()
		{
			EditorGUILayout.Space(4);

			EditorGUI.BeginChangeCheck();
			_folder = (DefaultAsset)EditorGUILayout.ObjectField("Folder", _folder, typeof(DefaultAsset), false);
			if (EditorGUI.EndChangeCheck())
				RefreshAssets();

			if (_folder == null)
			{
				EditorGUILayout.HelpBox("Drag a folder from the Project view to edit weights.", MessageType.Info);
				return;
			}

			if (GUILayout.Button("Refresh"))
				RefreshAssets();

			if (_elements.Count == 0)
			{
				EditorGUILayout.HelpBox("No ScriptableObjects implementing IRandomElement found in this folder.", MessageType.Warning);
				return;
			}

			float totalWeight = WeightedSelection.TotalWeight(_elements);

			EditorGUILayout.Space(8);
			EditorGUILayout.LabelField($"Items: {_elements.Count}    Total Weight: {totalWeight:F4}", EditorStyles.boldLabel);
			EditorGUILayout.Space(4);

			_scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

			for (int i = 0; i < _elements.Count; i++)
			{
				EditorGUILayout.BeginHorizontal();

				EditorGUI.BeginDisabledGroup(true);
				EditorGUILayout.ObjectField(_assets[i], typeof(ScriptableObject), false, GUILayout.Width(200));
				EditorGUI.EndDisabledGroup();

				float oldWeight = _elements[i].Weight;
				float newWeight = EditorGUILayout.FloatField(oldWeight, GUILayout.Width(80));
				if (!Mathf.Approximately(oldWeight, newWeight))
				{
					_elements[i].Weight = Mathf.Max(0f, newWeight);
					EditorUtility.SetDirty(_assets[i]);
				}

				float pct = totalWeight > 0f ? (_elements[i].Weight / totalWeight) * 100f : 0f;
				EditorGUILayout.LabelField($"{pct:F1}%", GUILayout.Width(60));

				EditorGUILayout.EndHorizontal();
			}

			EditorGUILayout.EndScrollView();

			EditorGUILayout.Space(8);

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Normalize Weights"))
			{
				WeightedSelection.Normalize(_elements);
				for (int i = 0; i < _assets.Count; i++)
					EditorUtility.SetDirty(_assets[i]);
			}
			if (GUILayout.Button("Save"))
			{
				AssetDatabase.SaveAssets();
			}
			EditorGUILayout.EndHorizontal();
		}

		void RefreshAssets()
		{
			_assets.Clear();
			_elements.Clear();

			if (_folder == null) return;

			string folderPath = AssetDatabase.GetAssetPath(_folder);
			if (string.IsNullOrEmpty(folderPath)) return;

			string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { folderPath });

			for (int i = 0; i < guids.Length; i++)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[i]);
				var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
				if (so is IRandomElement element)
				{
					_assets.Add(so);
					_elements.Add(element);
				}
			}
		}
	}
}
#endif
