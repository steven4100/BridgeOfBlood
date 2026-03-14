#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BridgeOfBlood.Editor
{
	/// <summary>
	/// Reusable helper for drawing [SerializeReference] lists with a concrete-type picker menu.
	/// Works with any interface or abstract base type.
	/// </summary>
	public static class SerializeReferenceListHelper
	{
		const float ButtonHeight = 22f;
		const float Spacing = 2f;
		const float MinElementHeightLines = 6f;

		static readonly Dictionary<Type, List<Type>> _typeCache = new();

		public static List<Type> GetConcreteTypes(Type baseType)
		{
			if (_typeCache.TryGetValue(baseType, out var cached))
				return cached;

			var types = TypeCache.GetTypesDerivedFrom(baseType)
				.Where(t =>
					!t.IsAbstract &&
					!t.IsGenericTypeDefinition &&
					t.GetConstructor(Type.EmptyTypes) != null)
				.OrderBy(t => t.Name)
				.ToList();

			_typeCache[baseType] = types;
			return types;
		}

		public static float GetListHeight(SerializedProperty property)
		{
			if (property == null || !property.isArray)
				return EditorGUIUtility.singleLineHeight;

			float h = EditorGUIUtility.singleLineHeight + Spacing;
			h += ButtonHeight + Spacing;

			if (!property.isExpanded)
				return h;

			for (int i = 0; i < property.arraySize; i++)
				h += GetElementHeight(property.GetArrayElementAtIndex(i)) + Spacing;

			return h;
		}

		static float GetElementHeight(SerializedProperty el)
		{
			float elHeight = EditorGUI.GetPropertyHeight(el, true);
			float minHeight = EditorGUIUtility.singleLineHeight * MinElementHeightLines
				+ EditorGUIUtility.standardVerticalSpacing * 2f;
			return Mathf.Max(elHeight, minHeight);
		}

		public static void DrawList(Rect position, SerializedProperty property, GUIContent label, Type baseType)
		{
			if (property == null || !property.isArray)
			{
				EditorGUI.PropertyField(position, property, label, true);
				return;
			}

			var r = position;

			property.isExpanded = EditorGUI.Foldout(
				new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight),
				property.isExpanded, label, true);
			r.y += EditorGUIUtility.singleLineHeight + Spacing;

			var addRow = new Rect(r.x, r.y, r.width, ButtonHeight);
			if (GUI.Button(addRow, $"Add {baseType.Name}..."))
				ShowAddMenu(property, baseType);
			r.y += ButtonHeight + Spacing;

			if (!property.isExpanded)
				return;

			EditorGUI.indentLevel++;
			for (int i = 0; i < property.arraySize; i++)
			{
				var el = property.GetArrayElementAtIndex(i);
				float elHeight = GetElementHeight(el);

				string typeName = el.managedReferenceValue != null
					? ObjectNames.NicifyVariableName(el.managedReferenceValue.GetType().Name)
					: "(null)";

				var headerRect = new Rect(r.x, r.y, r.width - 64f, EditorGUIUtility.singleLineHeight);
				EditorGUI.LabelField(headerRect, typeName, EditorStyles.boldLabel);

				var removeRect = new Rect(r.xMax - 60f, r.y, 60f, EditorGUIUtility.singleLineHeight);
				if (GUI.Button(removeRect, "Remove"))
				{
					property.DeleteArrayElementAtIndex(i);
					if (i < property.arraySize && property.GetArrayElementAtIndex(i).managedReferenceValue == null)
						property.DeleteArrayElementAtIndex(i);

					property.serializedObject.ApplyModifiedProperties();
					EditorGUI.indentLevel--;
					GUIUtility.ExitGUI();
					return;
				}

				var elRect = new Rect(r.x, r.y + EditorGUIUtility.singleLineHeight + Spacing, r.width - 64f,
					elHeight - EditorGUIUtility.singleLineHeight - Spacing);
				EditorGUI.PropertyField(elRect, el, GUIContent.none, true);

				r.y += elHeight + Spacing;
			}
			EditorGUI.indentLevel--;
		}

		static void ShowAddMenu(SerializedProperty listProp, Type baseType)
		{
			var so = listProp.serializedObject;
			var path = listProp.propertyPath;

			var menu = new GenericMenu();
			var types = GetConcreteTypes(baseType);

			if (types.Count == 0)
			{
				menu.AddDisabledItem(new GUIContent($"No concrete {baseType.Name} types found"));
				menu.ShowAsContext();
				return;
			}

			foreach (var type in types)
			{
				var captured = type;
				menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(captured.Name)), false, () =>
				{
					if (so == null || so.targetObject == null)
						return;

					so.Update();
					var prop = so.FindProperty(path);
					if (prop == null || !prop.isArray)
						return;

					int index = prop.arraySize;
					prop.InsertArrayElementAtIndex(index);
					var el = prop.GetArrayElementAtIndex(index);
					el.managedReferenceValue = null;

					try
					{
						el.managedReferenceValue = Activator.CreateInstance(captured);
					}
					catch (Exception e)
					{
						Debug.LogError(
							$"Failed to create {captured.FullName}. " +
							$"Ensure it is [Serializable], non-abstract, and has a parameterless ctor.\n{e}");
						prop.DeleteArrayElementAtIndex(index);
						so.ApplyModifiedProperties();
						return;
					}

					el.isExpanded = true;
					so.ApplyModifiedProperties();
				});
			}

			menu.ShowAsContext();
		}
	}
}
#endif
