using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit HUD for the round phase: spell loop strip, quota/blood/loops, passive item states.
/// </summary>
public class RoundPanelPresenter : MonoBehaviour, IStatePresenter<RoundSessionViewData>
{
	[SerializeField] VisualTreeAsset roundPanelUxml;
	[SerializeField] UIDocument uiDocument;

	VisualElement _roundRoot;
	VisualElement _spellArea;
	VisualElement _spellTrack;
	VisualElement _marker;
	Label _quotaLabel;
	Label _bloodLabel;
	Label _loopsLabel;
	VisualElement _itemStrip;

	PanelSettings _runtimePanelSettings;
	bool _ownsPanelSettings;

	bool _hasSpellSnapshot;
	List<string> _lastSpellLabels = new List<string>();

	bool _hasItemSnapshot;
	List<RoundItemRowViewData> _lastItemRows = new List<RoundItemRowViewData>();

	public void SetRoundVisible(bool visible) => SetRootVisible(visible);

	public void SetRootVisible(bool visible)
	{
		EnsureRoot();
		if (_roundRoot == null)
			return;
		_roundRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
	}

	public void Render(RoundSessionViewData data)
	{
		EnsureRoot();
		if (_quotaLabel == null)
			return;

		_quotaLabel.text = $"Quota: {data.BloodQuota:F0}";
		_bloodLabel.text = $"Blood: {data.BloodExtracted:F0}";
		_loopsLabel.text = $"Loops left: {data.LoopsRemaining}";

		IReadOnlyList<string> labels = data.SpellSlotLabels;
		int n = labels?.Count ?? 0;

		if (!_hasSpellSnapshot || !SpellLabelsEqual(_lastSpellLabels, labels))
		{
			_spellTrack.Clear();
			_lastSpellLabels.Clear();
			if (labels != null)
			{
				for (int i = 0; i < labels.Count; i++)
					_lastSpellLabels.Add(labels[i]);
			}
			_hasSpellSnapshot = true;

			for (int i = 0; i < n; i++)
			{
				var slot = new VisualElement();
				slot.AddToClassList("round-spell-slot");
				var nameLabel = new Label(labels[i]);
				nameLabel.AddToClassList("round-spell-slot-label");
				slot.Add(nameLabel);
				_spellTrack.Add(slot);
			}
		}

		UpdateMarker(data.IndexOfLastCastInLoop, n);

		if (!_hasItemSnapshot || !ItemRowsEqual(_lastItemRows, data.ItemRows))
		{
			_itemStrip.Clear();
			_lastItemRows.Clear();
			if (data.ItemRows != null)
			{
				for (int i = 0; i < data.ItemRows.Count; i++)
					_lastItemRows.Add(data.ItemRows[i]);
			}
			_hasItemSnapshot = true;

			if (data.ItemRows != null)
			{
				for (int i = 0; i < data.ItemRows.Count; i++)
					_itemStrip.Add(BuildItemChip(data.ItemRows[i]));
			}
		}
	}

	void UpdateMarker(int indexOfLastCast, int spellCount)
	{
		if (_marker == null || _spellArea == null)
			return;

		if (spellCount <= 0)
		{
			_marker.style.display = DisplayStyle.None;
			return;
		}

		_marker.style.display = DisplayStyle.Flex;
		int slot = indexOfLastCast < 0 ? 0 : indexOfLastCast;
		if (slot >= spellCount)
			slot = spellCount - 1;

		float centerPercent = (slot + 0.5f) / spellCount * 100f;
		_marker.style.left = new StyleLength(Length.Percent(centerPercent));
	}

	static VisualElement BuildItemChip(RoundItemRowViewData row)
	{
		var chip = new VisualElement();
		chip.AddToClassList("round-item-chip");
		chip.AddToClassList(row.IsActive ? "round-item-chip-active" : "round-item-chip-awaiting");

		var name = new Label(row.DisplayName);
		name.AddToClassList("round-item-chip-name");
		chip.Add(name);

		var state = new Label(row.IsActive ? "Active" : "Awaiting");
		state.AddToClassList("round-item-chip-state");
		chip.Add(state);

		return chip;
	}

	static bool SpellLabelsEqual(List<string> last, IReadOnlyList<string> current)
	{
		if (current == null)
			return last.Count == 0;
		if (last.Count != current.Count)
			return false;
		for (int i = 0; i < last.Count; i++)
		{
			if (last[i] != current[i])
				return false;
		}
		return true;
	}

	static bool ItemRowsEqual(List<RoundItemRowViewData> last, List<RoundItemRowViewData> current)
	{
		if (current == null)
			return last.Count == 0;
		if (last.Count != current.Count)
			return false;
		for (int i = 0; i < last.Count; i++)
		{
			RoundItemRowViewData a = last[i];
			RoundItemRowViewData b = current[i];
			if (a.DisplayName != b.DisplayName || a.IsActive != b.IsActive)
				return false;
		}
		return true;
	}

	void Awake()
	{
		if (uiDocument == null)
			uiDocument = GetComponent<UIDocument>();
		if (uiDocument == null)
			uiDocument = gameObject.AddComponent<UIDocument>();

		if (uiDocument.panelSettings == null)
		{
			_runtimePanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
			_runtimePanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
			_runtimePanelSettings.referenceResolution = new Vector2Int(1920, 1080);
			uiDocument.panelSettings = _runtimePanelSettings;
			_ownsPanelSettings = true;
		}

		if (roundPanelUxml != null)
			uiDocument.visualTreeAsset = roundPanelUxml;

		uiDocument.sortingOrder = 90;
	}

	void OnDestroy()
	{
		if (_ownsPanelSettings && _runtimePanelSettings != null)
			Destroy(_runtimePanelSettings);
	}

	void EnsureRoot()
	{
		if (_roundRoot != null || uiDocument == null)
			return;

		VisualElement tree = uiDocument.rootVisualElement;
		if (tree == null)
			return;

		_roundRoot = tree.Q<VisualElement>("round-root");
		_spellArea = tree.Q<VisualElement>("spell-loop-area");
		_spellTrack = tree.Q<VisualElement>("spell-loop-track");
		_marker = tree.Q<VisualElement>("spell-loop-marker");
		_quotaLabel = tree.Q<Label>("quota-label");
		_bloodLabel = tree.Q<Label>("blood-label");
		_loopsLabel = tree.Q<Label>("loops-label");
		_itemStrip = tree.Q<VisualElement>("item-strip");
	}
}
