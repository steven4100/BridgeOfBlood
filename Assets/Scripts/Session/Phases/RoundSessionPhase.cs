using System.Collections.Generic;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Effects;
using UnityEngine;

public sealed class RoundSessionPhase : SessionPhaseBase<RoundSessionViewData>
{
	readonly RoundPanelPresenter _roundPanel;
	readonly Camera _camera;

	public RoundSessionPhase(RoundPanelPresenter roundPanel, Camera camera)
		: base(roundPanel != null ? roundPanel : RoundSessionNoOpPresenter.Instance)
	{
		_roundPanel = roundPanel;
		_camera = camera;
	}

	public override void Enter(SessionFlowContext context)
	{
		context.RoundController.PrepareForRoundAfterShop();
		context.SpellCollection.SyncSpellLoopFromInventory(
			context.RuntimeGameConfig.playerInventory.GetSpellLoopAuthoring());
		context.RoundController.ResetForNewRound(context.SimulationZone.rect);
		_roundPanel?.SetRoundVisible(true);
	}

	public override void Exit(SessionFlowContext context)
	{
		_roundPanel?.SetRoundVisible(false);
	}

	protected override RoundSessionViewData TickAndBuildViewData(SessionFlowContext context, float deltaTime)
	{
		RoundTickResult result = context.RoundController.Tick(
			deltaTime,
			context.SimulationZone.rect,
			_camera,
			context.SimulationZone);

		if (result.roundEnded)
			context.Flow.SetState(result.quotaMet ? SessionState.Shop : SessionState.Lose);

		return BuildRoundSessionViewData(context);
	}

	static RoundSessionViewData BuildRoundSessionViewData(SessionFlowContext context)
	{
		RoundController rc = context.RoundController;
		var spellLabels = new List<string>();
		IReadOnlyList<RuntimeSpell> runtimeSpells = context.SpellCollection.RuntimeSpells;
		for (int i = 0; i < runtimeSpells.Count; i++)
		{
			SpellAuthoringData def = runtimeSpells[i].Definition;
			string label = def != null && def.ShopItemDefinition != null
				&& !string.IsNullOrEmpty(def.ShopItemDefinition.DisplayName)
				? def.ShopItemDefinition.DisplayName
				: def.name;
			spellLabels.Add(label);
		}

		int loopsRemaining = Mathf.Max(0, rc.SpellLoopsPerRound - rc.LoopsCompletedThisRound);

		var itemRows = new List<RoundItemRowViewData>();
		IReadOnlyList<Item> passive = context.RuntimeGameConfig.playerInventory.GetPassiveItems();
		IReadOnlyList<ItemEvalResult> evals = rc.LastItemResults;
		int ei = 0;
		for (int i = 0; i < passive.Count; i++)
		{
			Item item = passive[i];
			if (item == null)
				continue;
			if (ei >= evals.Count)
				break;
			ItemEvalResult ev = evals[ei++];
			string displayName = item.ShopItemDefinition != null
				&& !string.IsNullOrEmpty(item.ShopItemDefinition.DisplayName)
				? item.ShopItemDefinition.DisplayName
				: item.name;
			itemRows.Add(new RoundItemRowViewData(displayName, ev.applied));
		}

		return new RoundSessionViewData(
			spellLabels,
			rc.IndexOfLastCastInLoop,
			rc.BloodQuota,
			rc.BloodExtractedThisRound,
			loopsRemaining,
			itemRows);
	}
}
