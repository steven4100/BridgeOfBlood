using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Effects;
using EZServiceLocation;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace BridgeOfBlood.Editor
{
	public class TelemetryViewerWindow : EditorWindow
	{
		CombatSimulationController _combatSimulation;
		private Vector2 _scrollPos;
		private bool _foldFrame = true;
		private bool _foldSpellCast = true;
		private bool _foldSpellLoop = true;
		private bool _foldRound = true;
		private bool _foldGame = true;
		readonly List<KeyValuePair<string, float>> _values = new List<KeyValuePair<string, float>>();

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
			_combatSimulation = null;
			Repaint();
		}

		void Update()
		{
			if (!Application.isPlaying)
				return;

			if (_combatSimulation == null)
				_combatSimulation = ResolveCombatSimulation();

			if (_combatSimulation != null)
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

			if (_combatSimulation == null)
				_combatSimulation = ResolveCombatSimulation();

			if (_combatSimulation == null)
			{
				EditorGUILayout.HelpBox(
					"CombatSimulationController is not registered on ServiceLocator yet. Start Test Scene or Labbing Scene.",
					MessageType.Warning);
				EditorGUILayout.EndScrollView();
				return;
			}

			TelemetryAggregator agg = _combatSimulation.TelemetryAggregator;
			if (agg == null)
			{
				EditorGUILayout.HelpBox("Telemetry not ready yet (Start may not have run).", MessageType.Warning);
				EditorGUILayout.EndScrollView();
				return;
			}

			DrawSection("Frame", _foldFrame, () =>
			{
				var f = agg.CurrentFrame;
				DrawValues(agg, MetricsScope.Frame);
				EditorGUILayout.LabelField("Delta Time", f.deltaTime.ToString("F4"));
				EditorGUILayout.LabelField("Simulation Time", f.simulationTime.ToString("F2"));
			}, ref _foldFrame);

			DrawSection("Last Cast (loop totals)", _foldSpellCast, () =>
			{
				var s = agg.CurrentSpellCast;
				DrawValuesForSpell(agg, MetricsScope.SpellLoop, s.spellId);
				EditorGUILayout.LabelField("Spell Id", s.spellId.ToString());
				EditorGUILayout.LabelField("Invocation Id", s.invocationId.ToString());
			}, ref _foldSpellCast);

			DrawSection("Spell Loop (current)", _foldSpellLoop, () =>
			{
				var s = agg.CurrentSpellLoop;
				DrawValues(agg, MetricsScope.SpellLoop);
				EditorGUILayout.LabelField("Loop Index", s.loopIndex.ToString());
				DrawPerSpell(agg, MetricsScope.SpellLoop, s.perSpell);
			}, ref _foldSpellLoop);

			DrawSection("Round (current)", _foldRound, () =>
			{
				var r = agg.CurrentRound;
				DrawValues(agg, MetricsScope.Round);
				EditorGUILayout.LabelField("Round Number", r.roundNumber.ToString());
				EditorGUILayout.LabelField("Loops Completed", r.loopsCompleted.ToString());
				DrawPerSpell(agg, MetricsScope.Round, r.perSpell);
			}, ref _foldRound);

			DrawSection("Game (total)", _foldGame, () =>
			{
				var g = agg.Game;
				DrawValues(agg, MetricsScope.Game);
				EditorGUILayout.LabelField("Rounds Completed", g.roundsCompleted.ToString());
				DrawPerSpell(agg, MetricsScope.Game, g.perSpell);
			}, ref _foldGame);

			EditorGUILayout.EndScrollView();
		}

		static CombatSimulationController ResolveCombatSimulation() =>
			ServiceLocator.Current.GetService<CombatSimulationController>(throwError: false);

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

		void DrawValues(TelemetryAggregator agg, MetricsScope scope)
		{
			agg.FillSubscriptionValues(scope, _values);
			DrawValueRows();
		}

		void DrawValuesForSpell(TelemetryAggregator agg, MetricsScope scope, int spellId)
		{
			agg.FillSubscriptionValuesForSpell(scope, spellId, _values);
			DrawValueRows();
		}

		void DrawValueRows()
		{
			for (int i = 0; i < _values.Count; i++)
			{
				KeyValuePair<string, float> row = _values[i];
				EditorGUILayout.LabelField(row.Key, row.Value.ToString("F2"));
			}
		}

		void DrawPerSpell(TelemetryAggregator agg, MetricsScope scope, SpellTelemetrySnapshot[] perSpell)
		{
			if (perSpell == null || perSpell.Length == 0)
			{
				EditorGUILayout.LabelField("(no per-spell data)");
				return;
			}

			for (int i = 0; i < perSpell.Length; i++)
			{
				SpellTelemetrySnapshot p = perSpell[i];
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
				EditorGUILayout.LabelField($"Spell {p.spellId} (invocations: {p.invocationCount})", EditorStyles.boldLabel);
				DrawValuesForSpell(agg, scope, p.spellId);
				EditorGUILayout.EndVertical();
			}
		}
	}
}
