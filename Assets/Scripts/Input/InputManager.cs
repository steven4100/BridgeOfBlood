using System.Collections.Generic;
using UnityEngine;

public class InputManager
{
    InputActionMap _actionMap;
    List<InputReciever> _inputRecieverList;
    InputReciever _focused;

    public void Init()
    {
        _actionMap = new InputActionMap();
        _inputRecieverList = new List<InputReciever>();
        _focused = null;
    }

    public void RegisterInputReciever(InputReciever inputReciever)
    {
        _inputRecieverList.Add(inputReciever);
    }

    public void UnregisterInputReciever(InputReciever inputReciever)
    {
        if (_focused == inputReciever)
        {
            _focused.OnLostInputFocus(_actionMap);
            _focused = null;
        }

        _inputRecieverList.Remove(inputReciever);
    }

    public void ProcessInput()
    {
        InputContext inputContext = new InputContext
        {
            mousePosition = Input.mousePosition,
            isClicking = Input.GetMouseButtonDown(0)
        };

        InputReciever hovered = GetCurrentlyHoveredInputReciever(inputContext.mousePosition);
        if (hovered != GetCurrentFocusedInputReciever())
        {
            _focused?.OnLostInputFocus(_actionMap);
            _focused = hovered;
            _focused?.OnGainedInputFocus(_actionMap);
        }

        if (inputContext.isClicking && _focused != null)
            _actionMap.OnClick?.Invoke(inputContext);
    }

    public void ClearFocus()
    {
        if (_focused == null)
            return;

        _focused.OnLostInputFocus(_actionMap);
        _focused = null;
    }

    private InputReciever GetCurrentFocusedInputReciever()
    {
        return _focused;
    }

    private InputReciever GetCurrentlyHoveredInputReciever(Vector2 position)
    {
        for (int i = _inputRecieverList.Count - 1; i >= 0; i--)
        {
            if (_inputRecieverList[i].IsHover(position))
                return _inputRecieverList[i];
        }

        return null;
    }
}
