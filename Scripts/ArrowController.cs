using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ArrowController : MonoBehaviour
{
    private const string DefaultFontAssetResourcePath = "Fonts & Materials/LiberationSans SDF";

    private RectTransform _rectTransform;
    private RectTransform _labelRect;
    private Button _button;
    [SerializeField] private TextMeshProUGUI _directionLabelText;
    private Coroutine _pulseRoutine;
    private Action<Vector2Int> _pressedAction;
    private float _baseSize = 56f;

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

        _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _rectTransform.sizeDelta = new Vector2(_baseSize, _baseSize);

        Image background = GetComponent<Image>();
        background.color = new Color(0.15f, 0.72f, 0.25f, 0.88f);

        ColorBlock colors = _button.colors;
        colors.normalColor = background.color;
        colors.highlightedColor = new Color(0.2f, 0.86f, 0.31f, 1f);
        colors.pressedColor = new Color(0.09f, 0.58f, 0.18f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.15f, 0.72f, 0.25f, 0.28f);
        _button.colors = colors;

        GameObject textRoot = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textRoot.transform.SetParent(transform, false);

        _labelRect = textRoot.GetComponent<RectTransform>();
        _labelRect.anchorMin = Vector2.zero;
        _labelRect.anchorMax = Vector2.one;
        _labelRect.offsetMin = Vector2.zero;
        _labelRect.offsetMax = Vector2.zero;

        _directionLabelText = textRoot.GetComponent<TextMeshProUGUI>();
        _directionLabelText.font = Resources.Load<TMP_FontAsset>(DefaultFontAssetResourcePath);
        _directionLabelText.fontStyle = FontStyles.Bold;
        _directionLabelText.alignment = TextAlignmentOptions.Center;
        _directionLabelText.color = Color.white;
        _directionLabelText.enableAutoSizing = true;
        _directionLabelText.fontSizeMin = 10f;
        _directionLabelText.fontSizeMax = 42f;

        _pressedAction = value => callback(value);
        _button.onClick.AddListener(HandlePressed);
        RefreshLabel();
        SetVisible(false);
    }

    public void SetLayout(Vector2 anchoredPosition, float buttonSize)
    {
        _baseSize = buttonSize;
        _rectTransform.anchoredPosition = anchoredPosition;
        _rectTransform.sizeDelta = new Vector2(buttonSize, buttonSize);
    }

    public void RefreshLabel()
    {
        if (_directionLabelText == null || _labelRect == null)
        {
            return;
        }

        if (Direction == Vector2Int.left)
        {
            _directionLabelText.text = "▶";
            _labelRect.anchoredPosition = new Vector2(-1f, 0f);
        }
        else if (Direction == Vector2Int.right)
        {
            _directionLabelText.text = "◀";
            _labelRect.anchoredPosition = new Vector2(-1f, 0f);
        }
        else if (Direction == Vector2Int.up)
        {
            _directionLabelText.text = "▲";
            _labelRect.anchoredPosition = Vector2.zero;
        }
        else
        {
            _directionLabelText.text = "▼";
            _labelRect.anchoredPosition = Vector2.zero;
        }
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
