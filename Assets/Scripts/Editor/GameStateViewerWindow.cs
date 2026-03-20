using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using BridgeOfBlood.Data.Shared;

namespace BridgeOfBlood.Editor
{
	public class GameStateViewerWindow : EditorWindow
	{
		private TestSceneManager _sceneManager;
		private Vector2 _scrollPos;

		[MenuItem("Window/Bridge of Blood/Game State")]
		public static void Open()
		{
			var w = GetWindow<GameStateViewerWindow>("Game State");
			w.minSize = new Vector2(280, 320);
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
			Repaint();
		}

		void Update()
		{
			if (Application.isPlaying && _sceneManager == null)
			{
				_sceneManager = Object.FindObjectOfType<TestSceneManager>();
				if (_sceneManager != null)
					Repaint();
			}
			if (Application.isPlaying && _sceneManager != null)
				Repaint();
		}

		void OnGUI()
		{
			_scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

			if (!Application.isPlaying)
			{
				EditorGUILayout.HelpBox("Enter Play mode to view game state.", MessageType.Info);
				EditorGUILayout.EndScrollView();
				return;
			}

			if (_sceneManager == null)
			{
				_sceneManager = Object.FindObjectOfType<TestSceneManager>();
				if (_sceneManager == null)
				{
					EditorGUILayout.HelpBox("No TestSceneManager in scene.", MessageType.Warning);
					EditorGUILayout.EndScrollView();
					return;
				}
			}

			GameState gs = _sceneManager.CurrentGameState;

			foreach (FieldInfo field in typeof(GameState).GetFields(BindingFlags.Public | BindingFlags.Instance))
			{
				if (field.Name == "roundMetrics")
					continue;
				object value = field.GetValue(gs);
				string label = FormatFieldName(field.Name);
				string valueStr = FormatValue(value, field.FieldType);
				EditorGUILayout.LabelField(label, valueStr);
			}

			EditorGUILayout.EndScrollView();
		}

		static string FormatFieldName(string fieldName)
		{
			var sb = new StringBuilder();
			for (int i = 0; i < fieldName.Length; i++)
			{
				if (i > 0 && char.IsUpper(fieldName[i]))
					sb.Append(' ');
				sb.Append(i == 0 ? char.ToUpperInvariant(fieldName[i]) : fieldName[i]);
			}
			return sb.ToString();
		}

		static string FormatValue(object value, System.Type type)
		{
			if (value == null) return "null";
			if (type == typeof(float)) return ((float)value).ToString("F2");
			return value.ToString();
		}
	}
}
