#if UNITY_EDITOR
using System;
using BridgeOfBlood.Data.Shared;
using UnityEditor;
using UnityEngine;

namespace BridgeOfBlood.Editor
{
	[CustomPropertyDrawer(typeof(ScriptableObjectImplementsAttribute))]
	public class ScriptableObjectImplementsDrawer : PropertyDrawer
	{
		const float Spacing = 4f;

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			float h = EditorGUIUtility.singleLineHeight;
			var attr = (ScriptableObjectImplementsAttribute)attribute;
			var current = property.objectReferenceValue as ScriptableObject;
			if (current != null && !attr.InterfaceType.IsAssignableFrom(current.GetType()))
				h += Spacing + EditorGUIUtility.singleLineHeight * 2.2f + EditorGUIUtility.singleLineHeight + Spacing;
			return h;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var attr = (ScriptableObjectImplementsAttribute)attribute;
			Type iface = attr.InterfaceType;

			EditorGUI.BeginProperty(position, label, property);

			var current = property.objectReferenceValue as ScriptableObject;
			Rect lineRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

			int id = GUIUtility.GetControlID(FocusType.Passive);
			Rect labelRect = EditorGUI.PrefixLabel(lineRect, id, label);
			const float pickButtonWidth = 24f;
			Rect pickRect = new Rect(labelRect.xMax - pickButtonWidth, labelRect.y, pickButtonWidth, labelRect.height);
			Rect objectRect = new Rect(labelRect.x, labelRect.y, Mathf.Max(0f, labelRect.width - pickButtonWidth - 2f), labelRect.height);

			string pickTip = $"List assets implementing {iface.Name}";
			var pickIconBase = EditorGUIUtility.IconContent("d_Search Icon");
			var pickContent = pickIconBase?.image != null
				? new GUIContent(pickIconBase.image, pickTip)
				: new GUIContent("…", pickTip);

			if (GUI.Button(pickRect, pickContent, EditorStyles.iconButton))
				ScriptableObjectImplementsPickerWindow.Show(property, iface);

			// Do not use EditorGUI.ObjectField: it always opens Unity's "Select Scriptable Object" picker,
			// which cannot filter by interface. Use a matching visual + drag-drop + our window (button / double-click).
			DrawScriptableObjectFieldWithoutUnityPicker(objectRect, property, iface, current);

			current = property.objectReferenceValue as ScriptableObject;
			if (current != null && !iface.IsAssignableFrom(current.GetType()))
			{
				float y = lineRect.yMax + Spacing;
				var helpRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight * 2f);
				EditorGUI.HelpBox(
					helpRect,
					$"Reference does not implement {iface.Name}. Assign a compatible asset or clear.",
					MessageType.Warning);
				y = helpRect.yMax + Spacing;
				var clearRect = new Rect(position.x, y, 120f, EditorGUIUtility.singleLineHeight);
				if (GUI.Button(clearRect, "Clear reference"))
				{
					property.objectReferenceValue = null;
					property.serializedObject.ApplyModifiedProperties();
				}
			}

			EditorGUI.EndProperty();
		}

		static void DrawScriptableObjectFieldWithoutUnityPicker(Rect objectRect, SerializedProperty property, Type iface,
			ScriptableObject current)
		{
			Event evt = Event.current;
			if (objectRect.Contains(evt.mousePosition))
			{
				switch (evt.type)
				{
					case EventType.DragUpdated:
					case EventType.DragPerform:
					{
						ScriptableObject toAssign = null;
						foreach (UnityEngine.Object r in DragAndDrop.objectReferences)
						{
							if (r is ScriptableObject so && iface.IsAssignableFrom(so.GetType()))
							{
								toAssign = so;
								break;
							}
						}

						if (toAssign != null)
						{
							DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
							if (evt.type == EventType.DragPerform)
							{
								property.objectReferenceValue = toAssign;
								property.serializedObject.ApplyModifiedProperties();
								DragAndDrop.AcceptDrag();
							}
							evt.Use();
						}
						break;
					}
					case EventType.MouseDown when evt.button == 0 && evt.clickCount == 2:
						ScriptableObjectImplementsPickerWindow.Show(property, iface);
						evt.Use();
						break;
					case EventType.ContextClick:
					{
						var menu = new GenericMenu();
						menu.AddItem(new GUIContent($"Pick {iface.Name}…"), false,
							() => ScriptableObjectImplementsPickerWindow.Show(property, iface));
						if (current != null)
						{
							menu.AddItem(new GUIContent("Ping"), false, () => EditorGUIUtility.PingObject(current));
							menu.AddItem(new GUIContent("Clear"), false, () =>
							{
								property.objectReferenceValue = null;
								property.serializedObject.ApplyModifiedProperties();
							});
						}
						menu.ShowAsContext();
						evt.Use();
						break;
					}
				}
			}

			GUI.Box(objectRect, GUIContent.none, EditorStyles.objectField);
			Rect inner = EditorStyles.objectField.padding.Remove(objectRect);
			GUIContent objGui = EditorGUIUtility.ObjectContent(current, typeof(ScriptableObject));
			EditorGUI.LabelField(inner, objGui);
		}
	}
}
#endif
