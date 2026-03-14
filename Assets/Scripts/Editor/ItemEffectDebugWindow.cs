#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BridgeOfBlood.Editor
{
	public class ItemEffectDebugWindow : EditorWindow
	{
		private TestSceneManager _sceneManager;
		private Vector2 _scrollPos;

		[MenuItem("Window/Bridge of Blood/Item Effect Debug")]
		public static void Open()
		{
			var w = GetWindow<ItemEffectDebugWindow>("Item Effects");
			w.minSize = new Vector2(300, 200);
		}

		void OnEnable()
		{
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		}

		void OnDisable()
		{
			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
		}

		void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			_sceneManager = null;
		}

		void Update()
		{
			if (EditorApplication.isPlaying)
				Repaint();
		}

		void OnGUI()
		{
			if (!EditorApplication.isPlaying)
			{
				EditorGUILayout.HelpBox("Enter Play mode to see item effect results.", MessageType.Info);
				return;
			}

			if (_sceneManager == null)
				_sceneManager = Object.FindObjectOfType<TestSceneManager>();

			if (_sceneManager == null)
			{
				EditorGUILayout.HelpBox("No TestSceneManager found in scene.", MessageType.Warning);
				return;
			}

			var results = _sceneManager.LastItemResults;
			if (results == null || results.Count == 0)
			{
				EditorGUILayout.LabelField("No items configured.");
				return;
			}

			_scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

			EditorGUILayout.LabelField("Item Effect Results", EditorStyles.boldLabel);
			EditorGUILayout.Space(4);

			for (int i = 0; i < results.Count; i++)
			{
				var result = results[i];
				var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);

				float indicatorSize = 12f;
				var indicatorRect = new Rect(rect.x, rect.y + 2f, indicatorSize, indicatorSize);
				EditorGUI.DrawRect(indicatorRect, result.applied ? Color.green : Color.gray);

				var labelRect = new Rect(rect.x + indicatorSize + 6f, rect.y, rect.width - indicatorSize - 6f, rect.height);
				EditorGUI.LabelField(labelRect, result.itemName);
			}

			EditorGUILayout.EndScrollView();
		}
	}
}
#endif
