using BridgeOfBlood.Data.Shared;
using BridgeOfBlood.Data.Spells;
using EZServiceLocation;
using TMPro;
using UnityEngine;

/// <summary>
/// Shows remaining loop mana over <see cref="GameConfig.totalMana"/>. Binds combat from
/// <see cref="ServiceLocator"/> on <see cref="ServicesRegisteredEvent"/> and refreshes on
/// <see cref="SimulationCompleteEvent"/>.
/// </summary>
[DefaultExecutionOrder(50)]
public class ManaUiController : MonoBehaviour
{
	[SerializeField] TMP_Text manaText;

	CombatSimulationController _combat;

	void OnEnable()
	{
		SharedGameEventBus.Bus.SubscribeTo<SimulationCompleteEvent>(OnSimulationComplete);
		ServicesRegisteredEvent.SubscribeAndCatchUp(OnServicesRegistered);
	}

	void OnDisable()
	{
		SharedGameEventBus.Bus.UnsubscribeFrom<SimulationCompleteEvent>(OnSimulationComplete);
		ServicesRegisteredEvent.Unsubscribe(OnServicesRegistered);
		_combat = null;
	}

	void OnServicesRegistered(ref ServicesRegisteredEvent _)
	{
		_combat = ServiceLocator.Current.GetService<CombatSimulationController>(throwError: false);
		Refresh(null);
	}

	void OnSimulationComplete(ref SimulationCompleteEvent @event)
	{
		Refresh(@event.frameModifications);
	}

	void Refresh(SpellModificationCollection modifications)
	{
		if (_combat == null)
			return;

		float total = _combat.RuntimeGameConfig.totalMana;
		float remaining = _combat.LoopedSpellCaster.GetRemainingMana(total, modifications);
		manaText.text = $"{remaining:0} / {total:0}";
	}
}
