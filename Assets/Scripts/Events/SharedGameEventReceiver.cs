using System;
using BridgeOfBlood.Data.Shared;
using UnityEngine;
using UnityEngine.Events;


public sealed class SharedGameEventReceiver : MonoBehaviour
{
	[SerializeReference, SerializeInterface]
	[SerializeField] GameEventListenerBase eventType;


    private void OnEnable()
    {
		eventType.OnEnable();
    }
    private void OnDisable()
    {
		eventType.OnDisable();
    }

}

[System.Serializable]
public abstract class GameEventListenerBase 
{

	public abstract void OnEnable();
	public abstract void OnDisable();
}
[System.Serializable]
public abstract class GameEventListener<T> : GameEventListenerBase where T : IEvent
{
	public UnityEvent<T> GameEvent;

	public override void OnEnable(){
		SharedGameEventBus.Bus.SubscribeTo<T>(Handle);
	}
    public override void OnDisable()
    {
		SharedGameEventBus.Bus.UnsubscribeFrom<T>(Handle);
    }
    public void Handle(ref T gameEvent)
	{
		GameEvent?.Invoke(gameEvent);
	}
}
[System.Serializable]
public class RoundEnterEventListener : GameEventListener<RoundEnterEvent> { }


[System.Serializable]
public class ShopEnterEventListener : GameEventListener<ShopEnterEvent> { }
