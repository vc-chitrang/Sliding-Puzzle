using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ArrowController : MonoBehaviour
{
    private RectTransform _rectTransform;
    private Button _button;
    private Text _label;
    private Coroutine _pulseRoutine;
    private Action<Vector2Int> _pressedAction;

    public Vector2Int Direction { get; private set; }

    public static ArrowController Create(
        RectTransform parent,
        Vector2Int direction,
        string label,
        Func<Vector2Int, bool> callback)
    {
        GameObject root = new GameObject(
            $"Arrow_{direction.x}_{direction.y}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(ArrowController));

        root.transform.SetParent(parent, false);

        ArrowController controller = root.GetComponent<ArrowController>();
        controller.Setup(direction, label, callback);
        return controller;
    }

    public void Setup(Vector2Int direction, string label, Func<Vector2Int, bool> callback)
    {
        Direction = direction;
        _rectTransform = GetComponent<RectTransform>();
        _button = GetComponent<Button>();

        _rectTransform.sizeDelta = new Vector2(70f, 70f);

        Image background = GetComponent<Image>();
        background.color = new Color(0.15f, 0.72f, 0.25f, 0.88f);

        ColorBlock colors = _button.colors;
        colors.normalColor = background.color;
        colors.highlightedColor = new Color(0.2f, 0.86f, 0.31f, 1f);
        colors.pressedColor = new Color(0.09f, 0.58f, 0.18f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.15f, 0.72f, 0.25f, 0.28f);
        _button.colors = colors;

        GameObject textRoot = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textRoot.transform.SetParent(transform, false);

        RectTransform textRect = textRoot.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        _label = textRoot.GetComponent<Text>();
        _label.text = label;
        _label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _label.fontStyle = FontStyle.Bold;
        _label.alignment = TextAnchor.MiddleCenter;
        _label.color = Color.white;
        _label.resizeTextForBestFit = true;

        _pressedAction = value => callback(value);
        _button.onClick.AddListener(HandlePressed);
        SetVisible(false);
    }

    public void SetScreenPosition(Vector2 screenPoint, Vector2 offset)
    {
        _rectTransform.position = screenPoint + offset;
    }

    public void SetVisible(bool isVisible)
    {
        gameObject.SetActive(isVisible);

        if (isVisible)
        {
            if (_pulseRoutine == null)
            {
                _pulseRoutine = StartCoroutine(PulseRoutine());
            }
        }
        else
        {
            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
                _pulseRoutine = null;
            }

            transform.localScale = Vector3.one;
        }
    }

    private void HandlePressed()
    {
        _pressedAction?.Invoke(Direction);
    }

    private IEnumerator PulseRoutine()
    {
        while (true)
        {
            float scale = 1f + (Mathf.Sin(Time.unscaledTime * 4f) * 0.06f);
            transform.localScale = Vector3.one * scale;
            yield return null;
        }
    }
}
