using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR
/// <summary>
/// Shared drawing for the behaviors list (type picker + list). Used by AttackEntityDataDrawer so nested lists get the picker.
/// </summary>
public static class AttackEntityBehaviorsListDrawerHelper
{
    public const float ButtonHeight = 22f;
    public const float Spacing = 2f;
    /// <summary>Minimum height per list element when it has polymorphic content so bottoms don't clip.</summary>
    private const float MinElementHeightLines = 10f;

    private static List<Type> _concreteBehaviorTypes;

    /// <summary>
    /// Finds all non-abstract concrete AttackEntityBehavior types across ALL assemblies (asmdefs included).
    /// Uses TypeCache (Editor-only) which is fast and correct.
    /// </summary>
    public static List<Type> GetConcreteBehaviorTypes()
    {
        if (_concreteBehaviorTypes != null)
            return _concreteBehaviorTypes;

        _concreteBehaviorTypes = TypeCache.GetTypesDerivedFrom<AttackEntityBehavior>()
            .Where(t =>
                t != typeof(AttackEntityBehavior) &&
                !t.IsAbstract &&
                !t.IsGenericTypeDefinition &&
                t.GetConstructor(Type.EmptyTypes) != null // must be instantiable via Activator.CreateInstance
            )
            .OrderBy(t => t.Name)
            .ToList();

        return _concreteBehaviorTypes;
    }

    public static float GetBehaviorsListHeight(SerializedProperty property)
    {
        if (property == null || !property.isArray)
            return EditorGUIUtility.singleLineHeight;

        float h = EditorGUIUtility.singleLineHeight + Spacing; // foldout

        // button row
        h += ButtonHeight + Spacing;

        if (!property.isExpanded)
            return h;

        for (int i = 0; i < property.arraySize; i++)
        {
            var el = property.GetArrayElementAtIndex(i);
            h += GetElementHeight(el) + Spacing;
        }

        return h;
    }

    /// <summary>Shared height for one list element so GetBehaviorsListHeight and DrawBehaviorsList stay in sync.</summary>
    private static float GetElementHeight(SerializedProperty el)
    {
        float elHeight = EditorGUI.GetPropertyHeight(el, true);
        float minHeight = EditorGUIUtility.singleLineHeight * MinElementHeightLines + EditorGUIUtility.standardVerticalSpacing * 2f;
        if (elHeight < minHeight)
            elHeight = minHeight;
        return elHeight;
    }

    public static void DrawBehaviorsList(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property == null || !property.isArray)
        {
            EditorGUI.PropertyField(position, property, label, true);
            return;
        }

        var r = position;

        // Foldout
        property.isExpanded = EditorGUI.Foldout(
            new Rect(r.x, r.y, r.width, EditorGUIUtility.singleLineHeight),
            property.isExpanded,
            label,
            true
        );
        r.y += EditorGUIUtility.singleLineHeight + Spacing;

        // Add button
        var addRow = new Rect(r.x, r.y, r.width, ButtonHeight);
        if (GUI.Button(addRow, "Add behavior..."))
        {
            ShowAddBehaviorMenu(property);
        }
        r.y += ButtonHeight + Spacing;

        if (!property.isExpanded)
            return;

        // Elements
        EditorGUI.indentLevel++;
        for (int i = 0; i < property.arraySize; i++)
        {
            var el = property.GetArrayElementAtIndex(i);

            float elHeight = GetElementHeight(el);

            // Draw element (leave room for remove button)
            var elRect = new Rect(r.x, r.y, r.width - 64f, elHeight);
            EditorGUI.PropertyField(elRect, el, true);

            // Remove button
            var removeRect = new Rect(r.xMax - 60f, r.y, 60f, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(removeRect, "Remove"))
            {
                // For SerializeReference, Unity often requires two deletes:
                // 1) nulls the reference
                // 2) removes the array slot
                property.DeleteArrayElementAtIndex(i);
                if (i < property.arraySize && property.GetArrayElementAtIndex(i).managedReferenceValue == null)
                    property.DeleteArrayElementAtIndex(i);

                property.serializedObject.ApplyModifiedProperties();
                EditorGUI.indentLevel--;
                GUIUtility.ExitGUI();
                return;
            }

            r.y += elHeight + Spacing;
        }
        EditorGUI.indentLevel--;
    }

    private static void ShowAddBehaviorMenu(SerializedProperty listProp)
    {
        // IMPORTANT: Because GenericMenu callbacks run later, cache these now.
        var so = listProp.serializedObject;
        var path = listProp.propertyPath;

        var menu = new GenericMenu();
        var types = GetConcreteBehaviorTypes();

        if (types.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("No concrete AttackEntityBehavior types found"));
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

                // CRITICAL: InsertArrayElementAtIndex duplicates the previous element.
                // For SerializeReference, explicitly clear before assigning.
                el.managedReferenceValue = null;

                try
                {
                    el.managedReferenceValue = Activator.CreateInstance(captured);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to create instance of {captured.FullName}. " +
                                   $"Ensure it is [Serializable], non-abstract, and has a public parameterless ctor.\n{e}");

                    // Clean up the inserted slot if construction failed.
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

/// <summary>
/// Drawer for AttackEntityData so the nested "behaviors" list uses our type picker.
/// Unity often does not apply attribute drawers to nested properties; using the containing type ensures the picker is used in the inspector.
/// </summary>
[CustomPropertyDrawer(typeof(AttackEntityData))]
public class AttackEntityDataDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // AttackEntityData is a ScriptableObject; this field is an object reference.
        if (property.propertyType == SerializedPropertyType.ObjectReference)
            return EditorGUI.GetPropertyHeight(property, label);

        float h = 0f;

        var child = property.Copy();
        var end = property.GetEndProperty();

        child.NextVisible(true);
        while (!SerializedProperty.EqualContents(child, end))
        {
            float ch = (child.name == "behaviors")
                ? AttackEntityBehaviorsListDrawerHelper.GetBehaviorsListHeight(child)
                : EditorGUI.GetPropertyHeight(child, true);

            h += ch + AttackEntityBehaviorsListDrawerHelper.Spacing;

            if (!child.NextVisible(false))
                break;
        }

        return h;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // AttackEntityData is a ScriptableObject; this field is an object reference.
        if (property.propertyType == SerializedPropertyType.ObjectReference)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        var r = position;

        var child = property.Copy();
        var end = property.GetEndProperty();

        child.NextVisible(true);
        while (!SerializedProperty.EqualContents(child, end))
        {
            float height = (child.name == "behaviors")
                ? AttackEntityBehaviorsListDrawerHelper.GetBehaviorsListHeight(child)
                : EditorGUI.GetPropertyHeight(child, true);

            var propRect = new Rect(r.x, r.y, r.width, height);

            if (child.name == "behaviors")
                AttackEntityBehaviorsListDrawerHelper.DrawBehaviorsList(propRect, child, new GUIContent("Behaviors"));
            else
                EditorGUI.PropertyField(propRect, child, true);

            r.y += height + AttackEntityBehaviorsListDrawerHelper.Spacing;

            if (!child.NextVisible(false))
                break;
        }
    }
}

/// <summary>
/// Custom editor for AttackEntityData assets so the behaviors list uses the type picker when editing the asset.
/// </summary>
[CustomEditor(typeof(AttackEntityData))]
public class AttackEntityDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var it = serializedObject.GetIterator();
        it.Next(true);
        while (it.NextVisible(false))
        {
            if (it.name == "behaviors")
            {
                float h = AttackEntityBehaviorsListDrawerHelper.GetBehaviorsListHeight(it);
                Rect r = EditorGUILayout.GetControlRect(true, h);
                AttackEntityBehaviorsListDrawerHelper.DrawBehaviorsList(r, it, new GUIContent("Behaviors"));
            }
            else
                EditorGUILayout.PropertyField(it, true);
        }
        serializedObject.ApplyModifiedProperties();
    }
}
#endif