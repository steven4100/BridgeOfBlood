using UnityEngine;

public interface IInputReciever
{
    public bool IsHover(in Vector2 position);

    public void OnLostInputFocus(InputActionMap input);

    public void OnGainedInputFocus(InputActionMap input);

}
