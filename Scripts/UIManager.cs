using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages all UI elements on the Puzzle_Screen.
///
/// Supports two visual modes:
///   Launch Mode  — puzzle visible but gameplay UI hidden; "Discover Art" button shown.
///   Gameplay Mode — full gameplay UI visible; "Discover Art" button hidden.
///
/// GameManager calls EnterLaunchMode() on startup and ExitLaunchMode() on
/// first tile/arrow interaction or when a cropped sprite is loaded.
/// </summary>
public class UIManager : MonoBehaviour
{
    private GameManager _gameManager;

    [Header("Header / Footer containers")]
    [Tooltip("The Header child of Puzzle_Screen. Hidden in launch mode.")]
    [SerializeField] private GameObject _header;
    [Tooltip("The Footer child of Puzzle_Screen. Hidden in launch mode.")]
    [SerializeField] private GameObject _footer;

    [Header("Gameplay Text")]
    [SerializeField] private TextMeshProUGUI _titleLabelText;
    [SerializeField] private TextMeshProUGUI _timerLabelText;
    [SerializeField] private TextMeshProUGUI _bestTimeLabelText;
    [SerializeField] private TextMeshProUGUI _statusMessageText;

    [Header("Gameplay Buttons / Panel")]
    [SerializeField] private RawImage    _previewImage;
    [SerializeField] private GameObject  _previewPanelObject;
    [SerializeField] private Button      _previewToggleButton;
    [SerializeField] private Button      _resetPuzzleButton;
    [SerializeField] private Button      _newImageButton;
    [SerializeField] private Button      _backButton;
    [SerializeField] private AspectRatioFitter _previewImageAspectRatioFitter;

    [Header("Launch Mode")]
    [Tooltip("'Discover Art' button shown only in launch mode.")]
    [SerializeField] private Button      _discoverArtButton;
    [Tooltip("Browse_And_Discover_Screen root — navigated to by 'Discover Art'.")]
    [SerializeField] private GameObject  _browseScreen;

    // ─────────────────────────────────────────────────────────────────
    // Public properties
    // ─────────────────────────────────────────────────────────────────

    public bool IsPreviewVisible =>
        _previewPanelObject != null && _previewPanelObject.activeSelf;

    // ─────────────────────────────────────────────────────────────────
    // Initialisation (called by GameManager.Awake)
    // ─────────────────────────────────────────────────────────────────

    public void Initialize(GameManager gameManager)
    {
        _gameManager = gameManager;
        EnsureEventSystem();
        BindButtonCallbacks();
        ValidateReferences();
    }

    // ─────────────────────────────────────────────────────────────────
    // Launch / Gameplay mode
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Switch to launch mode: hide gameplay UI, show "Discover Art" button.
    /// Called by GameManager on startup and when "New Image" is pressed.
    /// </summary>
    public void EnterLaunchMode()
    {
        _header?.SetActive(false);
        _footer?.SetActive(false);
        _discoverArtButton?.gameObject.SetActive(true);
    }

    /// <summary>
    /// Switch to gameplay mode: show gameplay UI, hide "Discover Art" button.
    /// Called by GameManager on first tile/arrow click or when a cropped image is loaded.
    /// </summary>
    public void ExitLaunchMode()
    {
        _discoverArtButton?.gameObject.SetActive(false);
        _header?.SetActive(true);
        _footer?.SetActive(true);
    }

    // ─────────────────────────────────────────────────────────────────
    // Gameplay state setters (called by GameManager)
    // ─────────────────────────────────────────────────────────────────

    public void SetTitle(string title)
    {
        if (_titleLabelText != null)
            _titleLabelText.text = title;
    }

    public void SetTimer(float seconds)
    {
        if (_timerLabelText == null) return;
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        _timerLabelText.text = $"Timer  {totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    public void SetBestTime(float seconds)
    {
        if (_bestTimeLabelText == null) return;
        if (seconds < 0f)
        {
            _bestTimeLabelText.text = "--:--";
            return;
        }
        int totalSeconds = Mathf.FloorToInt(seconds);
        _bestTimeLabelText.text = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    public void SetStatus(string message)
    {
        if (_statusMessageText != null)
            _statusMessageText.text = message;
    }

    public void SetPreview(bool isVisible, Texture previewTexture)
    {
        if (_previewPanelObject != null)
        {
            _previewPanelObject.SetActive(isVisible);

            // Ensure PreviewPanel always renders in front of siblings
            if (isVisible)
                _previewPanelObject.transform.SetAsLastSibling();
        }

        if (_previewImage != null)
            _previewImage.texture = previewTexture;

        if (previewTexture != null && _previewImageAspectRatioFitter != null)
            _previewImageAspectRatioFitter.aspectRatio =
                previewTexture.width / (float)previewTexture.height;
    }

    /// <summary>
    /// Match PreviewPanel's RectTransform to the board panel so they overlap exactly.
    /// </summary>
    public void MatchPreviewToBoard(RectTransform boardPanel)
    {
        if (_previewPanelObject == null || boardPanel == null) return;

        RectTransform prt = _previewPanelObject.GetComponent<RectTransform>();
        if (prt == null) return;

        prt.anchorMin        = boardPanel.anchorMin;
        prt.anchorMax        = boardPanel.anchorMax;
        prt.pivot            = boardPanel.pivot;
        prt.sizeDelta        = boardPanel.sizeDelta;
        prt.anchoredPosition = boardPanel.anchoredPosition;
    }

    // ─────────────────────────────────────────────────────────────────
    // Button binding
    // ─────────────────────────────────────────────────────────────────

    private void BindButtonCallbacks()
    {
        BindButton(_resetPuzzleButton,   () => _gameManager.ResetPuzzle());

        UIPressHandler handler = null;
        _previewToggleButton.TryGetComponent<UIPressHandler>(out handler);
        if (handler != null) {
            handler.onPressDown.AddListener(() => {
                _gameManager.TogglePreview(true);
            });

            handler.onPressUp.AddListener(() => {
                _gameManager.TogglePreview(false);
            });
        }

        // "New Image" resets to launch mode with a fresh random image
        BindButton(_newImageButton, () =>
        {
            ScreenManager.Instance?.ClearStack();
            _gameManager.ResetToLaunchMode();
        });

        // Back button — returns to previous screen (e.g. Artwork_Focus_Screen)
        BindButton(_backButton, () => ScreenManager.Instance?.GoBack());

        // Discover Art — navigates to browse screen (launch mode only)
        BindButton(_discoverArtButton, OnDiscoverArtPressed);
    }

    private void OnDiscoverArtPressed()
    {
        if (ScreenManager.Instance != null)
            ScreenManager.Instance.ShowScreen(_browseScreen);
        else if (_browseScreen != null)
            _browseScreen.SetActive(true);
    }

    // ─────────────────────────────────────────────────────────────────
    // Validation
    // ─────────────────────────────────────────────────────────────────

    private void ValidateReferences()
    {
        if (_header               == null) Debug.LogWarning("UIManager: _header is missing.");
        if (_footer               == null) Debug.LogWarning("UIManager: _footer is missing.");
        if (_titleLabelText       == null) Debug.LogError("UIManager: Header/Title is missing.");
        if (_statusMessageText    == null) Debug.LogError("UIManager: Header/Status is missing.");
        if (_timerLabelText       == null) Debug.LogError("UIManager: Footer/Timer is missing.");
        if (_bestTimeLabelText    == null) Debug.LogError("UIManager: Footer/Best is missing.");
        if (_resetPuzzleButton    == null) Debug.LogError("UIManager: Footer/ResetButton is missing.");
        if (_previewToggleButton  == null) Debug.LogError("UIManager: Footer/PreviewButton is missing.");
        if (_newImageButton       == null) Debug.LogError("UIManager: Footer/NewImageButton is missing.");
        if (_previewPanelObject   == null) Debug.LogError("UIManager: PreviewPanel is missing.");
        if (_previewImage         == null) Debug.LogError("UIManager: PreviewPanel/PreviewImage is missing.");
        if (_previewImageAspectRatioFitter == null)
            Debug.LogError("UIManager: PreviewPanel/PreviewImage AspectRatioFitter is missing.");
        if (_discoverArtButton    == null) Debug.LogWarning("UIManager: _discoverArtButton is missing.");
        if (_browseScreen         == null) Debug.LogWarning("UIManager: _browseScreen is missing.");
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
