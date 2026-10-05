using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Catalog row that arms one map-modifier definition for placement.</summary>
public sealed class MapModifierUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] TMP_Text description;

    MapModifierDefinition _definition;
    Action<MapModifierDefinition> _onSelected;

    public MapModifierDefinition Definition => _definition;

    public void Bind(MapModifierDefinition definition, Action<MapModifierDefinition> onSelected)
    {
        _definition = definition;
        _onSelected = onSelected;
        SetArmed(false);
    }

    public void SetArmed(bool armed)
    {
        description.text = armed
            ? $"> {_definition.DisplayName}"
            : _definition.DisplayName;
        description.fontStyle = armed ? FontStyles.Bold : FontStyles.Normal;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _onSelected(_definition);
    }
}
