#if UNITY_EDITOR
using BridgeOfBlood.Data.Enemies;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace BridgeOfBlood.Editor
{
	public class EnemyInspectorWindow : EditorWindow
	{
		private EnemySelector _selector;
		private TestSceneManager _sceneManager;
		private Vector2 _scrollPos;

		[MenuItem("Window/Bridge of Blood/Enemy Inspector")]
		public static void Open()
		{
			var w = GetWindow<EnemyInspectorWindow>("Enemy Inspector");
			w.minSize = new Vector2(320, 300);
		}

		void OnEnable() => EditorApplication.playModeStateChanged += OnPlayModeChanged;
		void OnDisable() => EditorApplication.playModeStateChanged -= OnPlayModeChanged;

		void OnPlayModeChanged(PlayModeStateChange _)
		{
			_selector = null;
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
				EditorGUILayout.HelpBox("Enter Play mode and right-click an enemy to inspect it.", MessageType.Info);
				return;
			}

			if (_selector == null)
				_selector = Object.FindObjectOfType<EnemySelector>();
			if (_sceneManager == null)
				_sceneManager = Object.FindObjectOfType<TestSceneManager>();

			if (_selector == null)
			{
				EditorGUILayout.HelpBox("No EnemySelector found. Add one to the scene.", MessageType.Warning);
				return;
			}

			if (_sceneManager == null || _sceneManager.Simulation == null)
			{
				EditorGUILayout.HelpBox("No TestSceneManager or simulation found.", MessageType.Warning);
				return;
			}

			int selectedId = _selector.SelectedEnemyId;
			if (selectedId < 0)
			{
				EditorGUILayout.HelpBox("Right-click an enemy in the game view to select it.", MessageType.Info);
				return;
			}

			var enemies = _sceneManager.Simulation.GetEnemies();
			if (!enemies.IsCreated || enemies.Length == 0)
			{
				EditorGUILayout.LabelField("No enemies alive.");
				return;
			}

			int idx = -1;
			for (int i = 0; i < enemies.Length; i++)
			{
				if (enemies[i].entityId == selectedId)
				{
					idx = i;
					break;
				}
			}

			if (idx < 0)
			{
				EditorGUILayout.LabelField($"Enemy #{selectedId} no longer exists. Right-click to select another.");
				return;
			}

			var enemy = enemies[idx];
			_scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

			EditorGUILayout.LabelField($"Enemy #{enemy.entityId}", EditorStyles.boldLabel);
			EditorGUILayout.Space(4);

			Section("Health");
			float pct = enemy.maxHealth > 0f ? enemy.health / enemy.maxHealth : 0f;
			var hpRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
			EditorGUI.ProgressBar(hpRect, Mathf.Clamp01(pct),
				$"{enemy.health:F1} / {enemy.maxHealth:F1} ({pct:P0})");

			Section("Movement");
			Field("Position", $"({enemy.position.x:F1}, {enemy.position.y:F1})");
			Field("Move Speed", enemy.moveSpeed.ToString("F2"));

			Section("Attributes");
			Field("Corruption", enemy.corruptionFlag.ToString());
			Field("Elemental Weakness", enemy.elementalWeakness.ToString());
			Field("Status Ailments", enemy.statusAilmentFlag != 0
				? enemy.statusAilmentFlag.ToString()
				: "None");

			Section("Visual");
			Field("Frame Index", enemy.visual.frameIndex.ToString());
			Field("Scale", enemy.visual.scale.ToString("F2"));

			EditorGUILayout.EndScrollView();
		}

		static void Section(string title)
		{
			EditorGUILayout.Space(4);
			EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
		}

		static void Field(string label, string value)
		{
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.PrefixLabel(label);
			EditorGUILayout.SelectableLabel(value, GUILayout.Height(EditorGUIUtility.singleLineHeight));
			EditorGUILayout.EndHorizontal();
		}
	}
}
#endif
