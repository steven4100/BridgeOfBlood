using UnityEngine;

public interface InputReciever
{
    public bool IsHover(in Vector2 position);

    public void OnLostInputFocus(InputActionMap input);

    public void OnGainedInputFocus(InputActionMap input);

}
