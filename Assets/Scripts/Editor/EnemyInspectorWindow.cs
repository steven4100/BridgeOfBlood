#if UNITY_EDITOR
using BridgeOfBlood.Data.Enemies;
using BridgeOfBlood.Data.Shared;
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

			var enemies = _sceneManager.Simulation.State.EnemyBuffers;
			if (!enemies.Motion.IsCreated || enemies.Length == 0)
			{
				EditorGUILayout.LabelField("No enemies alive.");
				return;
			}

			int idx = -1;
			for (int i = 0; i < enemies.Length; i++)
			{
				if (enemies.EntityIds[i] == selectedId)
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

			int entityId = enemies.EntityIds[idx];
			EnemyVitality vit = enemies.Vitality[idx];
			EnemyMotion motion = enemies.Motion[idx];
			EnemyCombatTraits traits = enemies.CombatTraits[idx];
			StatusAilmentFlag status = enemies.Status[idx];
			EntityVisual visual = enemies.Presentation[idx].visual;
			_scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

			EditorGUILayout.LabelField($"Enemy #{entityId}", EditorStyles.boldLabel);
			EditorGUILayout.Space(4);

			Section("Health");
			float pct = vit.maxHealth > 0f ? vit.health / vit.maxHealth : 0f;
			var hpRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
			EditorGUI.ProgressBar(hpRect, Mathf.Clamp01(pct),
				$"{vit.health:F1} / {vit.maxHealth:F1} ({pct:P0})");

			Section("Movement");
			Field("Position", $"({motion.position.x:F1}, {motion.position.y:F1})");
			Field("Move Speed", motion.moveSpeed.ToString("F2"));

			Section("Attributes");
			Field("Corruption", traits.corruptionFlag.ToString());
			Field("Elemental Weakness", traits.elementalWeakness.ToString());
			Field("Status Ailments", status != 0
				? status.ToString()
				: "None");

			Section("Visual");
			Field("Frame Index", visual.frameIndex.ToString());
			Field("Scale", visual.scale.ToString("F2"));

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
