using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds one catalog row per map-modifier definition for arming placement.</summary>
public sealed class MapModifierController : MonoBehaviour
{
    [SerializeField] Transform layoutRoot;
    [SerializeField] MapModifierUI modifierPrefab;

    readonly List<MapModifierUI> _instances = new List<MapModifierUI>();

    public void Bind(IReadOnlyList<MapModifierDefinition> definitions, Action<MapModifierDefinition> onSelected)
    {
        Clear();
        for (int i = 0; i < definitions.Count; i++)
        {
            MapModifierUI ui = Instantiate(modifierPrefab, layoutRoot);
            ui.Bind(definitions[i], onSelected);
            _instances.Add(ui);
        }
    }

    public void SetArmed(MapModifierDefinition definition)
    {
        for (int i = 0; i < _instances.Count; i++)
            _instances[i].SetArmed(_instances[i].Definition == definition);
    }

    void Clear()
    {
        for (int i = 0; i < _instances.Count; i++)
            Destroy(_instances[i].gameObject);
        _instances.Clear();
    }
}
