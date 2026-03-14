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

			var types = SerializeReferenceListHelper.GetConcreteTypes(baseType);
			string currentName = property.managedReferenceValue != null
				? property.managedReferenceValue.GetType().Name
				: "(None)";

			var names = new List<string> { "(None)" };
			int selected = 0;
			for (int i = 0; i < types.Count; i++)
			{
				names.Add(ObjectNames.NicifyVariableName(types[i].Name));
				if (property.managedReferenceValue != null && property.managedReferenceValue.GetType() == types[i])
					selected = i + 1;
			}

			var typeRect = new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight);
			int newSelected = EditorGUI.Popup(typeRect, label.text, selected, names.ToArray());

			if (newSelected != selected)
			{
				if (newSelected == 0)
					property.managedReferenceValue = null;
				else
					property.managedReferenceValue = Activator.CreateInstance(types[newSelected - 1]);

				property.serializedObject.ApplyModifiedProperties();
			}

			r.y += EditorGUIUtility.singleLineHeight + Spacing;

			if (property.managedReferenceValue != null)
			{
				EditorGUI.indentLevel++;
				float propHeight = EditorGUI.GetPropertyHeight(property, true);
				EditorGUI.PropertyField(new Rect(r.x, r.y, r.width, propHeight), property, GUIContent.none, true);
				EditorGUI.indentLevel--;
			}
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
