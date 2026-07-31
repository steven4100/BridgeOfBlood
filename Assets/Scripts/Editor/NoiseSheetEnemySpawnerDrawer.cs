#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BridgeOfBlood.Editor
{
	/// <summary>
	/// Inspector for <see cref="NoiseSheetEnemySpawner"/>: grid-sampled noise preview plus layer stack controls.
	/// </summary>
	[CustomPropertyDrawer(typeof(NoiseSheetEnemySpawner))]
	public class NoiseSheetEnemySpawnerDrawer : PropertyDrawer
	{
		const float Spacing = 2f;
		const float RowHeight = 20f;
		const float PreviewSize = 160f;
		const int PreviewResolution = 64;

		static readonly Color PreviewBackground = new Color(0.1f, 0.1f, 0.1f);

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			var layersProp = property.FindPropertyRelative("layers");

			float h = 0f;
			h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("spawnTable"), true) + Spacing;
			h += RowHeight + Spacing; // centerOffsetNormalized
			h += RowHeight + Spacing; // cellSize
			h += RowHeight + Spacing; // threshold
			h += RowHeight + Spacing; // seed
			h += PreviewSize + Spacing;
			h += layersProp.arraySize * (RowHeight * 3f + Spacing);
			h += RowHeight + Spacing; // add button
			return h;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var spawnTableProp = property.FindPropertyRelative("spawnTable");
			var centerOffsetProp = property.FindPropertyRelative("centerOffsetNormalized");
			var cellSizeProp = property.FindPropertyRelative("cellSize");
			var thresholdProp = property.FindPropertyRelative("threshold");
			var seedProp = property.FindPropertyRelative("seed");
			var layersProp = property.FindPropertyRelative("layers");

			var r = new Rect(position.x, position.y, position.width, RowHeight);

			float tableH = EditorGUI.GetPropertyHeight(spawnTableProp, true);
			EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, tableH), spawnTableProp, true);
			r.y += tableH + Spacing;

			r.height = RowHeight;
			EditorGUI.PropertyField(r, centerOffsetProp);
			r.y += RowHeight + Spacing;
			EditorGUI.PropertyField(r, cellSizeProp);
			r.y += RowHeight + Spacing;
			EditorGUI.PropertyField(r, thresholdProp);
			r.y += RowHeight + Spacing;
			EditorGUI.PropertyField(r, seedProp);
			r.y += RowHeight + Spacing;

			float previewSide = Mathf.Min(PreviewSize, position.width);
			var previewRect = new Rect(r.x + (position.width - previewSide) * 0.5f, r.y, previewSide, previewSide);
			uint seed = seedProp.intValue <= 0 ? 1u : (uint)seedProp.intValue;
			DrawNoisePreview(previewRect, layersProp, seed, thresholdProp.floatValue);
			r.y += PreviewSize + Spacing;

			for (int i = 0; i < layersProp.arraySize; i++)
			{
				var layer = layersProp.GetArrayElementAtIndex(i);

				var headerRect = new Rect(r.x, r.y, position.width - 64f, RowHeight);
				EditorGUI.LabelField(new Rect(headerRect.x, headerRect.y, 70f, RowHeight), $"Layer {i + 1}", EditorStyles.boldLabel);

				var enabledProp = layer.FindPropertyRelative("enabled");
				EditorGUI.PropertyField(new Rect(headerRect.x + 74f, headerRect.y, 20f, RowHeight), enabledProp, GUIContent.none);

				var typeProp = layer.FindPropertyRelative("type");
				EditorGUI.PropertyField(new Rect(headerRect.x + 98f, headerRect.y, headerRect.width - 98f, RowHeight), typeProp, GUIContent.none);

				var removeRect = new Rect(position.xMax - 60f, r.y, 60f, RowHeight - 2f);
				if (GUI.Button(removeRect, "Remove"))
				{
					layersProp.DeleteArrayElementAtIndex(i);
					property.serializedObject.ApplyModifiedProperties();
					GUIUtility.ExitGUI();
					return;
				}
				r.y += RowHeight;

				DrawLayerFields(new Rect(r.x, r.y, position.width, RowHeight * 2f), layer);
				r.y += RowHeight * 2f + Spacing;
			}

			var addRect = new Rect(r.x, r.y, position.width, RowHeight);
			if (GUI.Button(addRect, "Add Layer..."))
				ShowAddMenu(property.serializedObject, layersProp.propertyPath);
		}

		static void DrawLayerFields(Rect rect, SerializedProperty layer)
		{
			var blendProp = layer.FindPropertyRelative("blend");
			var weightProp = layer.FindPropertyRelative("weight");
			var freqProp = layer.FindPropertyRelative("frequency");
			var offsetProp = layer.FindPropertyRelative("offset");
			var invertProp = layer.FindPropertyRelative("invert");

			float half = rect.width * 0.5f;
			float oldLabelWidth = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = 48f;

			EditorGUI.PropertyField(new Rect(rect.x, rect.y, half - 4f, RowHeight), blendProp, new GUIContent("Blend"));
			EditorGUI.PropertyField(new Rect(rect.x + half, rect.y, half - 4f, RowHeight), weightProp, new GUIContent("Weight"));

			EditorGUI.PropertyField(new Rect(rect.x, rect.y + RowHeight, half - 4f, RowHeight), freqProp, new GUIContent("Freq"));
			EditorGUI.PropertyField(new Rect(rect.x + half, rect.y + RowHeight, half * 0.55f - 4f, RowHeight), offsetProp, new GUIContent("Offset"));
			EditorGUI.PropertyField(new Rect(rect.x + half + half * 0.55f, rect.y + RowHeight, half * 0.45f - 4f, RowHeight), invertProp, new GUIContent("Invert"));

			EditorGUIUtility.labelWidth = oldLabelWidth;
		}

		static void ShowAddMenu(SerializedObject so, string layersPath)
		{
			var menu = new GenericMenu();
			foreach (NoiseType type in System.Enum.GetValues(typeof(NoiseType)))
			{
				NoiseType captured = type;
				menu.AddItem(new GUIContent(captured.ToString()), false, () =>
				{
					if (so == null || so.targetObject == null)
						return;

					so.Update();
					var prop = so.FindProperty(layersPath);
					if (prop == null || !prop.isArray)
						return;

					int index = prop.arraySize;
					prop.InsertArrayElementAtIndex(index);
					var el = prop.GetArrayElementAtIndex(index);
					NoiseLayer defaults = NoiseLayer.Default(captured);
					el.FindPropertyRelative("enabled").boolValue = defaults.enabled;
					el.FindPropertyRelative("type").enumValueIndex = (int)defaults.type;
					el.FindPropertyRelative("blend").enumValueIndex = (int)defaults.blend;
					el.FindPropertyRelative("weight").floatValue = defaults.weight;
					el.FindPropertyRelative("frequency").floatValue = defaults.frequency;
					el.FindPropertyRelative("offset").vector2Value = defaults.offset;
					el.FindPropertyRelative("invert").boolValue = defaults.invert;
					so.ApplyModifiedProperties();
				});
			}
			menu.ShowAsContext();
		}

		static void DrawNoisePreview(Rect rect, SerializedProperty layersProp, uint seed, float threshold)
		{
			EditorGUI.DrawRect(rect, PreviewBackground);

			if (Event.current.type != EventType.Repaint)
				return;

			var layers = ReadLayers(layersProp);
			float cellW = rect.width / PreviewResolution;
			float cellH = rect.height / PreviewResolution;

			for (int row = 0; row < PreviewResolution; row++)
			{
				for (int col = 0; col < PreviewResolution; col++)
				{
					float u = (col + 0.5f) / PreviewResolution;
					float v = (row + 0.5f) / PreviewResolution;
					float sample = NoiseField.SampleComposed(layers, u, v, seed);
					bool spawn = sample >= threshold;
					float gray = spawn ? sample : sample * 0.25f;
					var cell = new Rect(
						rect.x + col * cellW,
						rect.yMax - (row + 1) * cellH,
						cellW + 0.5f,
						cellH + 0.5f);
					EditorGUI.DrawRect(cell, new Color(gray, gray, gray, 1f));
				}
			}
		}

		static List<NoiseLayer> ReadLayers(SerializedProperty layersProp)
		{
			var layers = new List<NoiseLayer>(layersProp.arraySize);
			for (int i = 0; i < layersProp.arraySize; i++)
			{
				var el = layersProp.GetArrayElementAtIndex(i);
				layers.Add(new NoiseLayer
				{
					enabled = el.FindPropertyRelative("enabled").boolValue,
					type = (NoiseType)el.FindPropertyRelative("type").enumValueIndex,
					blend = (NoiseBlendMode)el.FindPropertyRelative("blend").enumValueIndex,
					weight = el.FindPropertyRelative("weight").floatValue,
					frequency = el.FindPropertyRelative("frequency").floatValue,
					offset = el.FindPropertyRelative("offset").vector2Value,
					invert = el.FindPropertyRelative("invert").boolValue
				});
			}
			return layers;
		}
	}
}
#endif
