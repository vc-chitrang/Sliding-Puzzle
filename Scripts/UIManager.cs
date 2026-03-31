using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    private GameManager _gameManager;
    private Text _titleText;
    private Text _timerText;
    private Text _bestText;
    private Text _statusText;
    private RawImage _previewImage;
    private GameObject _previewPanel;
    private Button _previewButton;
    private AspectRatioFitter _previewFitter;

    public RectTransform ArrowLayer { get; private set; }
    public bool IsPreviewVisible => _previewPanel != null && _previewPanel.activeSelf;

    public void Initialize(GameManager gameManager)
    {
        _gameManager = gameManager;
        EnsureEventSystem();
        BuildCanvas();
    }

    public void SetTitle(string title)
    {
        _titleText.text = title;
    }

    public void SetTimer(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        _timerText.text = $"Timer  {totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    public void SetBestTime(float seconds)
    {
        if (seconds < 0f)
        {
            _bestText.text = "Best  --:--";
            return;
        }

        int totalSeconds = Mathf.FloorToInt(seconds);
        _bestText.text = $"Best  {totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    public void SetStatus(string message)
    {
        _statusText.text = message;
    }

    public void SetPreview(bool isVisible, Texture previewTexture)
    {
        _previewPanel.SetActive(isVisible);
        _previewImage.texture = previewTexture;
        if (previewTexture != null)
        {
            _previewFitter.aspectRatio = previewTexture.width / (float)previewTexture.height;
        }

        _previewButton.GetComponentInChildren<Text>().text = isVisible ? "Close Preview" : "Preview";
    }

    private void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("PuzzleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = canvasObject.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        CreateHeader(root);
        CreateFooter(root);
        CreateArrowLayer(root);
        CreatePreviewPanel(root);
    }

    private void CreateHeader(RectTransform root)
    {
        GameObject header = CreatePanel("Header", root, new Color(0f, 0f, 0f, 0.38f));
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0.5f, 1f);
        headerRect.anchorMax = new Vector2(0.5f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(940f, 160f);
        headerRect.anchoredPosition = new Vector2(0f, -36f);

        _titleText = CreateText("Title", headerRect, 42, FontStyle.Bold, TextAnchor.UpperCenter);
        _titleText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        _titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        _titleText.rectTransform.offsetMin = new Vector2(36f, -12f);
        _titleText.rectTransform.offsetMax = new Vector2(-36f, -18f);
        _titleText.color = Color.white;

        _statusText = CreateText("Status", headerRect, 28, FontStyle.Normal, TextAnchor.LowerCenter);
        _statusText.rectTransform.anchorMin = new Vector2(0f, 0f);
        _statusText.rectTransform.anchorMax = new Vector2(1f, 0.55f);
        _statusText.rectTransform.offsetMin = new Vector2(36f, 16f);
        _statusText.rectTransform.offsetMax = new Vector2(-36f, -8f);
        _statusText.color = new Color(0.88f, 0.92f, 0.96f, 1f);
    }

    private void CreateFooter(RectTransform root)
    {
        GameObject footer = CreatePanel("Footer", root, new Color(0f, 0f, 0f, 0.38f));
        RectTransform footerRect = footer.GetComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0.5f, 0f);
        footerRect.anchorMax = new Vector2(0.5f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.sizeDelta = new Vector2(1080f, 190f);
        footerRect.anchoredPosition = new Vector2(0f, 26f);

        _timerText = CreateText("Timer", footerRect, 32, FontStyle.Bold, TextAnchor.MiddleLeft);
        _timerText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        _timerText.rectTransform.anchorMax = new Vector2(0.32f, 1f);
        _timerText.rectTransform.offsetMin = new Vector2(36f, 0f);
        _timerText.rectTransform.offsetMax = new Vector2(-12f, -16f);
        _timerText.color = Color.white;

        _bestText = CreateText("Best", footerRect, 32, FontStyle.Bold, TextAnchor.MiddleLeft);
        _bestText.rectTransform.anchorMin = new Vector2(0f, 0f);
        _bestText.rectTransform.anchorMax = new Vector2(0.32f, 0.55f);
        _bestText.rectTransform.offsetMin = new Vector2(36f, 16f);
        _bestText.rectTransform.offsetMax = new Vector2(-12f, 0f);
        _bestText.color = Color.white;

        Button resetButton = CreateButton("ResetButton", footerRect, "Reset", new Vector2(620f, 100f), new Vector2(160f, 58f), () => _gameManager.ResetPuzzle());
        _previewButton = CreateButton("PreviewButton", footerRect, "Preview", new Vector2(800f, 100f), new Vector2(160f, 58f), () => _gameManager.TogglePreview());
        Button newImageButton = CreateButton("NewImageButton", footerRect, "New Image", new Vector2(980f, 100f), new Vector2(180f, 58f), () => _gameManager.LoadRandomImage());

        resetButton.GetComponent<Image>().color = new Color(0.16f, 0.46f, 0.83f, 0.95f);
        _previewButton.GetComponent<Image>().color = new Color(0.84f, 0.58f, 0.13f, 0.95f);
        newImageButton.GetComponent<Image>().color = new Color(0.48f, 0.2f, 0.82f, 0.95f);
    }

    private void CreateArrowLayer(RectTransform root)
    {
        GameObject arrowLayer = new GameObject("ArrowLayer", typeof(RectTransform));
        arrowLayer.transform.SetParent(root, false);

        ArrowLayer = arrowLayer.GetComponent<RectTransform>();
        ArrowLayer.anchorMin = Vector2.zero;
        ArrowLayer.anchorMax = Vector2.one;
        ArrowLayer.offsetMin = Vector2.zero;
        ArrowLayer.offsetMax = Vector2.zero;
    }

    private void CreatePreviewPanel(RectTransform root)
    {
        _previewPanel = CreatePanel("PreviewPanel", root, new Color(0f, 0f, 0f, 0.86f));
        RectTransform panelRect = _previewPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        GameObject imageObject = new GameObject("PreviewImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
        imageObject.transform.SetParent(_previewPanel.transform, false);

        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.sizeDelta = new Vector2(900f, 900f);

        _previewImage = imageObject.GetComponent<RawImage>();
        _previewFitter = imageObject.GetComponent<AspectRatioFitter>();
        _previewFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        _previewFitter.aspectRatio = 1f;

        Button closeButton = CreateButton("ClosePreviewButton", panelRect, "Back To Puzzle", new Vector2(0f, -420f), new Vector2(220f, 64f), () => _gameManager.TogglePreview());
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.5f, 0.5f);
        closeRect.anchorMax = new Vector2(0.5f, 0.5f);
        closeRect.pivot = new Vector2(0.5f, 0.5f);
        _previewPanel.SetActive(false);
    }

    private static GameObject CreatePanel(string name, RectTransform parent, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        panel.GetComponent<Image>().color = color;
        return panel;
    }

    private static Text CreateText(string name, RectTransform parent, int fontSize, FontStyle fontStyle, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Button CreateButton(string name, RectTransform parent, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.17f, 0.58f, 0.23f, 0.95f);

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(action);

        Text text = CreateText("Label", rect, 26, FontStyle.Bold, TextAnchor.MiddleCenter);
        text.text = label;
        text.color = Color.white;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
