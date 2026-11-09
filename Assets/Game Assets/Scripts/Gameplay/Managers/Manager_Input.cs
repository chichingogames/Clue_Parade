using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Raw pointer/touch -> press / drag / release intents in screen space.
// Mutates no game state; Manager_Game decides what a drag does.
public class Manager_Input : Singleton<Manager_Input>
{
    public event Action<Vector2> OnPress = delegate { };                    // press position
    public event Action<Vector2, Vector2> OnDrag = delegate { };            // press position, current position
    public event Action<Vector2, Vector2, float> OnRelease = delegate { };  // press position, release position, seconds held
    public event Action OnCancel = delegate { };                            // press lost (focus, input gated off)

    private bool _acceptInput;
    public bool AcceptInput
    {
        get => _acceptInput;
        set
        {
            _acceptInput = value;
            if (!value) CancelPress();
        }
    }

    private bool _pressing;
    private Vector2 _pressStart;
    private Vector2 _lastPosition;
    private float _pressTime;

    private void Update()
    {
        var pointer = Pointer.current;
        if (pointer == null || !_acceptInput) return;

        Vector2 position = pointer.position.ReadValue();

        if (pointer.press.wasPressedThisFrame)
        {
            _pressing = true;
            _pressStart = _lastPosition = position;
            _pressTime = Time.unscaledTime;
            OnPress(position);
        }
        else if (_pressing && pointer.press.isPressed && position != _lastPosition)
        {
            _lastPosition = position;
            OnDrag(_pressStart, position);
        }

        if (_pressing && pointer.press.wasReleasedThisFrame)
        {
            _pressing = false;
            OnRelease(_pressStart, position, Time.unscaledTime - _pressTime);
        }
    }

    private void CancelPress()
    {
        if (!_pressing) return;
        _pressing = false;
        OnCancel();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) CancelPress();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) CancelPress();
    }
}
