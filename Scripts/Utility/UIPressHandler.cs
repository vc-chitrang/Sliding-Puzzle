using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class UIPressHandler:MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler {
    [Header("Events")]
    public UnityEvent onPressDown;
    public UnityEvent onPressUp;

    private bool _isPressed;

    public void OnPointerDown(PointerEventData eventData) {
        _isPressed = true;
        onPressDown?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData) {
        if (!_isPressed)
            return;

        _isPressed = false;
        onPressUp?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData) {
        if (!_isPressed)
            return;

        _isPressed = false;
        onPressUp?.Invoke(); // treat exit as release
    }
}