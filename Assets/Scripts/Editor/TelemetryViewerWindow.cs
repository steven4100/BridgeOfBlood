using UnityEditor;
using UnityEngine;
using BridgeOfBlood.Data.Shared;

namespace BridgeOfBlood.Editor
{
	public class TelemetryViewerWindow : EditorWindow
	{
		private TestSceneManager _sceneManager;
		private Vector2 _scrollPos;
		private bool _foldFrame = true;
		private bool _foldSpellCast = true;
		private bool _foldSpellLoop = true;
		private bool _foldRound = true;
		private bool _foldGame = true;

		[MenuItem("Window/Bridge of Blood/Telemetry Viewer")]
		public static void Open()
		{
			var w = GetWindow<TelemetryViewerWindow>("Telemetry");
			w.minSize = new Vector2(360, 400);
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
				EditorGUILayout.HelpBox("Enter Play mode to view live telemetry.", MessageType.Info);
				EditorGUILayout.EndScrollView();
				return;
			}

			if (_sceneManager == null)
			{
				_sceneManager = Object.FindObjectOfType<TestSceneManager>();
				if (_sceneManager == null)
				{
					EditorGUILayout.HelpBox("No TestSceneManager in scene. Start a scene that has the runner.", MessageType.Warning);
					EditorGUILayout.EndScrollView();
					return;
				}
			}

			TelemetryAggregator agg = _sceneManager.TelemetryAggregator;
			if (agg == null)
			{
				EditorGUILayout.HelpBox("Telemetry not ready yet (Start may not have run).", MessageType.Warning);
				EditorGUILayout.EndScrollView();
				return;
			}

			DrawSection("Frame", _foldFrame, () =>
			{
				var f = agg.CurrentFrame;
				DrawMetrics(f.aggregate);
				EditorGUILayout.LabelField("Delta Time", f.deltaTime.ToString("F4"));
				EditorGUILayout.LabelField("Simulation Time", f.simulationTime.ToString("F2"));
			}, ref _foldFrame);

			DrawSection("Spell Cast (current window)", _foldSpellCast, () =>
			{
				var s = agg.CurrentSpellCast;
				DrawMetrics(s.aggregate);
				EditorGUILayout.LabelField("Spell Id", s.spellId.ToString());
				EditorGUILayout.LabelField("Invocation Id", s.invocationId.ToString());
			}, ref _foldSpellCast);

			DrawSection("Spell Loop (current)", _foldSpellLoop, () =>
			{
				var s = agg.CurrentSpellLoop;
				DrawMetrics(s.aggregate);
				EditorGUILayout.LabelField("Loop Index", s.loopIndex.ToString());
				DrawPerSpell(s.perSpell);
			}, ref _foldSpellLoop);

			DrawSection("Round (current)", _foldRound, () =>
			{
				var r = agg.CurrentRound;
				DrawMetrics(r.aggregate);
				EditorGUILayout.LabelField("Round Number", r.roundNumber.ToString());
				EditorGUILayout.LabelField("Loops Completed", r.loopsCompleted.ToString());
				DrawPerSpell(r.perSpell);
			}, ref _foldRound);

			DrawSection("Game (total)", _foldGame, () =>
			{
				var g = agg.Game;
				DrawMetrics(g.aggregate);
				EditorGUILayout.LabelField("Rounds Completed", g.roundsCompleted.ToString());
				DrawPerSpell(g.perSpell);
			}, ref _foldGame);

			EditorGUILayout.EndScrollView();
		}

		static void DrawSection(string title, bool folded, System.Action content, ref bool fold)
		{
			fold = EditorGUILayout.BeginFoldoutHeaderGroup(folded, title);
			EditorGUILayout.EndFoldoutHeaderGroup();
			if (fold)
			{
				EditorGUI.indentLevel++;
				content();
				EditorGUI.indentLevel--;
			}
		}

		static void DrawMetrics(CombatMetrics m)
		{
			EditorGUILayout.LabelField("Hits", m.hits.ToString());
			EditorGUILayout.LabelField("Kills", m.kills.ToString());
			EditorGUILayout.LabelField("Crits", m.crits.ToString());
			EditorGUILayout.LabelField("Total Damage", m.totalDamage.ToString("F1"));
			EditorGUILayout.LabelField("Physical", m.physicalDamage.ToString("F1"));
			EditorGUILayout.LabelField("Fire", m.fireDamage.ToString("F1"));
			EditorGUILayout.LabelField("Cold", m.coldDamage.ToString("F1"));
			EditorGUILayout.LabelField("Lightning", m.lightningDamage.ToString("F1"));
			EditorGUILayout.LabelField("Overkill", m.overkillDamage.ToString("F1"));
			EditorGUILayout.LabelField("Attack Entities Expired", m.attackEntitiesExpired.ToString());

			int totalAilments = m.TotalAilmentsApplied;
			EditorGUILayout.LabelField("Ailments Applied", totalAilments.ToString());
			EditorGUI.indentLevel++;
			EditorGUILayout.LabelField("Frozen", m.frozenApplied.ToString());
			EditorGUILayout.LabelField("Ignited", m.ignitedApplied.ToString());
			EditorGUILayout.LabelField("Shocked", m.shockedApplied.ToString());
			EditorGUILayout.LabelField("Poisoned", m.poisonedApplied.ToString());
			EditorGUILayout.LabelField("Stunned", m.stunnedApplied.ToString());
			EditorGUI.indentLevel--;

			EditorGUILayout.LabelField("Duration (s)", m.duration.ToString("F2"));
			EditorGUILayout.LabelField("DPS", m.DPS.ToString("F1"));
		}

		static void DrawPerSpell(SpellCombatMetrics[] perSpell)
		{
			if (perSpell == null || perSpell.Length == 0)
			{
				EditorGUILayout.LabelField("(no per-spell data)");
				return;
			}
			for (int i = 0; i < perSpell.Length; i++)
			{
				var p = perSpell[i];
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
				EditorGUILayout.LabelField($"Spell {p.spellId} (invocations: {p.invocationCount})", EditorStyles.boldLabel);
				DrawMetrics(p.metrics);
				EditorGUILayout.EndVertical();
			}
		}
	}
}
