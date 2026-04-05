#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using BridgeOfBlood.Data.Shared;
using UnityEditor;
using UnityEngine;

namespace BridgeOfBlood.Editor
{
	[CustomPropertyDrawer(typeof(SerializeInterfaceAttribute))]
	public class SerializeInterfaceDrawer : PropertyDrawer
	{
		const float Spacing = 2f;

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			if (property.isArray)
				return SerializeReferenceListHelper.GetListHeight(property);

			return GetSingleFieldHeight(property, label);
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			Type baseType = GetBaseType(fieldInfo);

			if (property.isArray)
			{
				SerializeReferenceListHelper.DrawList(position, property, label, baseType);
				return;
			}

			DrawSingleField(position, property, label, baseType);
		}

		float GetSingleFieldHeight(SerializedProperty property, GUIContent label)
		{
			float h = EditorGUIUtility.singleLineHeight + Spacing;

			if (property.managedReferenceValue != null)
				h += EditorGUI.GetPropertyHeight(property, label, true) + Spacing;

			return h;
		}

		void DrawSingleField(Rect position, SerializedProperty property, GUIContent label, Type baseType)
		{
			var r = position;

			string currentName = property.managedReferenceValue != null
				? ObjectNames.NicifyVariableName(property.managedReferenceValue.GetType().Name)
				: "(None)";

			var labelRect = new Rect(r.x, r.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);
			EditorGUI.LabelField(labelRect, label);

			var buttonRect = new Rect(r.x + EditorGUIUtility.labelWidth, r.y,
				r.width - EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);

			if (EditorGUI.DropdownButton(buttonRect, new GUIContent(currentName), FocusType.Keyboard))
				ShowSingleFieldMenu(property, baseType);

			r.y += EditorGUIUtility.singleLineHeight + Spacing;

			if (property.managedReferenceValue != null)
			{
				EditorGUI.indentLevel++;
				float propHeight = EditorGUI.GetPropertyHeight(property, true);
				EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, propHeight), property, GUIContent.none, true);
				EditorGUI.indentLevel--;
			}
		}

		static void ShowSingleFieldMenu(SerializedProperty property, Type baseType)
		{
			var so = property.serializedObject;
			var path = property.propertyPath;

			var menu = new GenericMenu();
			menu.AddItem(new GUIContent("(None)"), property.managedReferenceValue == null, () =>
			{
				so.Update();
				var prop = so.FindProperty(path);
				prop.managedReferenceValue = null;
				so.ApplyModifiedProperties();
			});

			menu.AddSeparator("");

			var types = SerializeReferenceListHelper.GetConcreteTypes(baseType);
			foreach (var type in types)
			{
				var captured = type;
				bool isActive = property.managedReferenceValue != null
					&& property.managedReferenceValue.GetType() == captured;
				menu.AddItem(new GUIContent(SerializeReferenceListHelper.GetMenuLabel(captured)), isActive, () =>
				{
					so.Update();
					var prop = so.FindProperty(path);
					prop.managedReferenceValue = Activator.CreateInstance(captured);
					so.ApplyModifiedProperties();
				});
			}

			menu.ShowAsContext();
		}

		static Type GetBaseType(FieldInfo fi)
		{
			Type fieldType = fi.FieldType;

			if (fieldType.IsArray)
				return fieldType.GetElementType();

			if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
				return fieldType.GetGenericArguments()[0];

			return fieldType;
		}
	}
}
#endif
