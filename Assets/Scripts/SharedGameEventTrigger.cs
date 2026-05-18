using BridgeOfBlood.Data.Shared;
using UnityEngine;

public sealed class SharedGameEventTrigger : MonoBehaviour
{
	[SerializeReference, SerializeInterface]
	SessionPhaseEventBase eventTrigger;

	public void Trigger()
	{
		eventTrigger.Invoke();
	}
}
