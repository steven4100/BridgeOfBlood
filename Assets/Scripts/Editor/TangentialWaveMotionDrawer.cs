#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BridgeOfBlood.Editor
{
	/// <summary>
	/// Inspector for <see cref="TangentialWaveMotionBehavior"/>: preview graph of the composed
	/// tangential force plus add/remove controls for the wave stack.
	/// </summary>
	[CustomPropertyDrawer(typeof(TangentialWaveMotionBehavior))]
	public class TangentialWaveMotionDrawer : PropertyDrawer
	{
		const float GraphHeight = 90f;
		const float Spacing = 2f;
		const float RowHeight = 20f;
		const float PreviewSeconds = 2f;
		const int GraphSamples = 160;

		static readonly Color GraphBackground = new Color(0.13f, 0.13f, 0.13f);
		static readonly Color CompositeColor = new Color(1f, 0.55f, 0.1f);
		static readonly Color ZeroLineColor = new Color(1f, 1f, 1f, 0.25f);
		static readonly Color[] WaveColors =
		{
			new Color(0.35f, 0.7f, 1f, 0.45f),
			new Color(0.5f, 1f, 0.5f, 0.45f),
			new Color(1f, 0.5f, 0.8f, 0.45f),
			new Color(1f, 1f, 0.4f, 0.45f),
			new Color(0.6f, 0.5f, 1f, 0.45f),
			new Color(0.4f, 1f, 0.9f, 0.45f),
			new Color(1f, 0.7f, 0.4f, 0.45f),
			new Color(0.8f, 0.8f, 0.8f, 0.45f)
		};

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			var wavesProp = property.FindPropertyRelative("waves");

			float h = RowHeight + Spacing;                    // isActive
			h += GraphHeight + Spacing;                       // graph
			h += wavesProp.arraySize * (RowHeight * 2f + Spacing); // wave rows (2 lines each)
			h += RowHeight + Spacing;                         // add button / cap hint
			return h;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var isActiveProp = property.FindPropertyRelative("isActive");
			var wavesProp = property.FindPropertyRelative("waves");

			var r = new Rect(position.x, position.y, position.width, RowHeight);
			EditorGUI.PropertyField(r, isActiveProp);
			r.y += RowHeight + Spacing;

			var graphRect = new Rect(r.x, r.y, position.width, GraphHeight);
			DrawGraph(graphRect, wavesProp);
			r.y += GraphHeight + Spacing;

			for (int i = 0; i < wavesProp.arraySize; i++)
			{
				var wave = wavesProp.GetArrayElementAtIndex(i);
				var typeProp = wave.FindPropertyRelative("type");

				var headerRect = new Rect(r.x, r.y, position.width - 64f, RowHeight);
				var left = new Rect(headerRect.x, headerRect.y, 60f, RowHeight);
				Color old = GUI.color;
				GUI.color = WaveColors[i % WaveColors.Length];
				EditorGUI.LabelField(left, $"Wave {i + 1}", EditorStyles.boldLabel);
				GUI.color = old;

				var typeRect = new Rect(headerRect.x + 64f, headerRect.y, headerRect.width - 64f, RowHeight);
				EditorGUI.PropertyField(typeRect, typeProp, GUIContent.none);

				var removeRect = new Rect(position.xMax - 60f, r.y, 60f, RowHeight - 2f);
				if (GUI.Button(removeRect, "Remove"))
				{
					wavesProp.DeleteArrayElementAtIndex(i);
					property.serializedObject.ApplyModifiedProperties();
					GUIUtility.ExitGUI();
					return;
				}
				r.y += RowHeight;

				var fieldsRect = new Rect(r.x, r.y, position.width, RowHeight);
				DrawWaveFields(fieldsRect, wave);
				r.y += RowHeight + Spacing;
			}

			bool atCap = wavesProp.arraySize >= TangentialWaveMotionBehavior.MaxWaves;
			var addRect = new Rect(r.x, r.y, position.width, RowHeight);
			if (atCap)
			{
				EditorGUI.LabelField(addRect, $"Wave cap reached ({TangentialWaveMotionBehavior.MaxWaves}).", EditorStyles.miniLabel);
			}
			else if (GUI.Button(addRect, "Add Wave..."))
			{
				ShowAddMenu(property.serializedObject, wavesProp.propertyPath);
			}
		}

		static void DrawWaveFields(Rect rect, SerializedProperty wave)
		{
			var ampProp = wave.FindPropertyRelative("amplitude");
			var freqProp = wave.FindPropertyRelative("frequency");
			var phaseProp = wave.FindPropertyRelative("phase");
			var pulseWidthProp = wave.FindPropertyRelative("pulseWidth");
			var offsetProp = wave.FindPropertyRelative("offset");
			bool isSquare = wave.FindPropertyRelative("type").enumValueIndex == (int)MotionWaveType.Square;

			int columns = isSquare ? 5 : 4;
			float col = rect.width / columns;
			float oldLabelWidth = EditorGUIUtility.labelWidth;
			int c = 0;

			EditorGUIUtility.labelWidth = 34f;
			EditorGUI.PropertyField(new Rect(rect.x + col * c++, rect.y, col - 4f, EditorGUIUtility.singleLineHeight),
				ampProp, new GUIContent("Amp"));
			EditorGUI.PropertyField(new Rect(rect.x + col * c++, rect.y, col - 4f, EditorGUIUtility.singleLineHeight),
				freqProp, new GUIContent("Freq"));
			EditorGUIUtility.labelWidth = 44f;
			EditorGUI.PropertyField(new Rect(rect.x + col * c++, rect.y, col - 4f, EditorGUIUtility.singleLineHeight),
				phaseProp, new GUIContent("Phase"));

			if (isSquare)
			{
				// Migrate legacy entries authored before pulseWidth existed.
				if (pulseWidthProp.floatValue <= 0f)
					pulseWidthProp.floatValue = 0.5f;

				EditorGUIUtility.labelWidth = 40f;
				EditorGUI.PropertyField(new Rect(rect.x + col * c++, rect.y, col - 4f, EditorGUIUtility.singleLineHeight),
					pulseWidthProp, new GUIContent("Width"));
			}

			EditorGUIUtility.labelWidth = 40f;
			EditorGUI.PropertyField(new Rect(rect.x + col * c, rect.y, col - 4f, EditorGUIUtility.singleLineHeight),
				offsetProp, new GUIContent("Offset"));

			EditorGUIUtility.labelWidth = oldLabelWidth;
		}

		static void ShowAddMenu(SerializedObject so, string wavesPath)
		{
			var menu = new GenericMenu();
			foreach (MotionWaveType type in System.Enum.GetValues(typeof(MotionWaveType)))
			{
				MotionWaveType captured = type;
				menu.AddItem(new GUIContent(captured.ToString()), false, () =>
				{
					if (so == null || so.targetObject == null)
						return;

					so.Update();
					var prop = so.FindProperty(wavesPath);
					if (prop == null || !prop.isArray || prop.arraySize >= TangentialWaveMotionBehavior.MaxWaves)
						return;

					int index = prop.arraySize;
					prop.InsertArrayElementAtIndex(index);
					var el = prop.GetArrayElementAtIndex(index);
					MotionWave defaults = MotionWave.Default(captured);
					el.FindPropertyRelative("type").enumValueIndex = (int)defaults.type;
					el.FindPropertyRelative("amplitude").floatValue = defaults.amplitude;
					el.FindPropertyRelative("frequency").floatValue = defaults.frequency;
					el.FindPropertyRelative("phase").floatValue = defaults.phase;
					el.FindPropertyRelative("pulseWidth").floatValue = defaults.pulseWidth;
					el.FindPropertyRelative("offset").floatValue = defaults.offset;
					so.ApplyModifiedProperties();
				});
			}
			menu.ShowAsContext();
		}

		static void DrawGraph(Rect rect, SerializedProperty wavesProp)
		{
			EditorGUI.DrawRect(rect, GraphBackground);

			if (Event.current.type != EventType.Repaint)
				return;

			int waveCount = wavesProp.arraySize;

			// Read wave params once.
			var waves = new MotionWave[waveCount];
			for (int i = 0; i < waveCount; i++)
			{
				var el = wavesProp.GetArrayElementAtIndex(i);
				waves[i] = new MotionWave
				{
					type = (MotionWaveType)el.FindPropertyRelative("type").enumValueIndex,
					amplitude = el.FindPropertyRelative("amplitude").floatValue,
					frequency = el.FindPropertyRelative("frequency").floatValue,
					phase = el.FindPropertyRelative("phase").floatValue,
					pulseWidth = el.FindPropertyRelative("pulseWidth").floatValue,
					offset = el.FindPropertyRelative("offset").floatValue
				};
			}

			// Auto range from the composite peak (symmetric around zero).
			float maxAbs = 1f;
			for (int s = 0; s <= GraphSamples; s++)
			{
				float t = PreviewSeconds * s / GraphSamples;
				float sum = 0f;
				for (int i = 0; i < waveCount; i++)
					sum += MotionWaveSampler.Sample(waves[i].type, t, waves[i].frequency, waves[i].phase, waves[i].amplitude, waves[i].pulseWidth, waves[i].offset);
				maxAbs = Mathf.Max(maxAbs, Mathf.Abs(sum));
			}

			// Zero line.
			Handles.color = ZeroLineColor;
			float midY = rect.y + rect.height * 0.5f;
			Handles.DrawLine(new Vector3(rect.x, midY), new Vector3(rect.xMax, midY));

			// Per-wave overlays.
			for (int i = 0; i < waveCount; i++)
			{
				Handles.color = WaveColors[i % WaveColors.Length];
				DrawCurve(rect, maxAbs, t => MotionWaveSampler.Sample(waves[i].type, t, waves[i].frequency, waves[i].phase, waves[i].amplitude, waves[i].pulseWidth, waves[i].offset), 1.5f);
			}

			// Composite on top.
			Handles.color = CompositeColor;
			DrawCurve(rect, maxAbs, t =>
			{
				float sum = 0f;
				for (int i = 0; i < waveCount; i++)
					sum += MotionWaveSampler.Sample(waves[i].type, t, waves[i].frequency, waves[i].phase, waves[i].amplitude, waves[i].pulseWidth, waves[i].offset);
				return sum;
			}, 2.5f);

			// Range labels.
			var labelStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 1f, 1f, 0.5f) } };
			GUI.Label(new Rect(rect.x + 2f, rect.y, 80f, 14f), $"+{maxAbs:0.#}", labelStyle);
			GUI.Label(new Rect(rect.x + 2f, rect.yMax - 15f, 80f, 14f), $"-{maxAbs:0.#}", labelStyle);
			GUI.Label(new Rect(rect.xMax - 30f, midY - 15f, 28f, 14f), $"{PreviewSeconds:0}s", labelStyle);
		}

		static void DrawCurve(Rect rect, float maxAbs, System.Func<float, float> sample, float width)
		{
			var points = new Vector3[GraphSamples + 1];
			for (int s = 0; s <= GraphSamples; s++)
			{
				float t = PreviewSeconds * s / GraphSamples;
				float v = sample(t);
				float x = rect.x + rect.width * s / GraphSamples;
				float y = rect.y + rect.height * 0.5f * (1f - Mathf.Clamp(v / maxAbs, -1f, 1f));
				points[s] = new Vector3(x, y, 0f);
			}
			Handles.DrawAAPolyLine(width, points);
		}
	}
}
#endif
