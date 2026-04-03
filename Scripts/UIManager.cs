using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    private GameManager _gameManager;
    [SerializeField] private TextMeshProUGUI _titleLabelText;
    [SerializeField] private TextMeshProUGUI _timerLabelText;
    [SerializeField] private TextMeshProUGUI _bestTimeLabelText;
    [SerializeField] private TextMeshProUGUI _statusMessageText;
    [SerializeField] private TextMeshProUGUI _previewButtonLabelText;
    [SerializeField] private RawImage _previewImage;
    [SerializeField] private GameObject _previewPanelObject;
    [SerializeField] private Button _previewToggleButton;
    [SerializeField] private Button _resetPuzzleButton;
    [SerializeField] private Button _newImageButton;
    [SerializeField] private Button _closePreviewButton;
    [SerializeField] private AspectRatioFitter _previewImageAspectRatioFitter;
    public bool IsPreviewVisible => _previewPanelObject != null && _previewPanelObject.activeSelf;

    public void Initialize(GameManager gameManager)
    {
        _gameManager = gameManager;
        EnsureEventSystem();
        BindButtonCallbacks();
        ValidateReferences();
    }

    public void SetTitle(string title)
    {
        if (_titleLabelText != null)
        {
            _titleLabelText.text = title;
        }
    }

    public void SetTimer(float seconds)
    {
        if (_timerLabelText == null)
        {
            return;
        }

        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        _timerLabelText.text = $"Timer  {totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    public void SetBestTime(float seconds)
    {
        if (_bestTimeLabelText == null)
        {
            return;
        }

        if (seconds < 0f)
        {
            _bestTimeLabelText.text = "Best  --:--";
            return;
        }

        int totalSeconds = Mathf.FloorToInt(seconds);
        _bestTimeLabelText.text = $"Best  {totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    public void SetStatus(string message)
    {
        if (_statusMessageText != null)
        {
            _statusMessageText.text = message;
        }
    }

    public void SetPreview(bool isVisible, Texture previewTexture)
    {
        if (_previewPanelObject != null)
        {
            _previewPanelObject.SetActive(isVisible);
        }

        if (_previewImage != null)
        {
            _previewImage.texture = previewTexture;
        }

        if (previewTexture != null && _previewImageAspectRatioFitter != null)
        {
            _previewImageAspectRatioFitter.aspectRatio = previewTexture.width / (float)previewTexture.height;
        }

        if (_previewButtonLabelText != null)
        {
            _previewButtonLabelText.text = isVisible ? "Close Preview" : "Preview";
        }
    }

    private void ValidateReferences()
    {
        if (_titleLabelText == null) Debug.LogError("UIManager: Header/Title is missing.");
        if (_statusMessageText == null) Debug.LogError("UIManager: Header/Status is missing.");
        if (_timerLabelText == null) Debug.LogError("UIManager: Footer/Timer is missing.");
        if (_bestTimeLabelText == null) Debug.LogError("UIManager: Footer/Best is missing.");
        if (_resetPuzzleButton == null) Debug.LogError("UIManager: Footer/ResetButton is missing.");
        if (_previewToggleButton == null) Debug.LogError("UIManager: Footer/PreviewButton is missing.");
        if (_previewButtonLabelText == null) Debug.LogError("UIManager: Footer/PreviewButton/Label is missing.");
        if (_newImageButton == null) Debug.LogError("UIManager: Footer/NewImageButton is missing.");
        if (_previewPanelObject == null) Debug.LogError("UIManager: PreviewPanel is missing.");
        if (_previewImage == null) Debug.LogError("UIManager: PreviewPanel/PreviewImage is missing.");
        if (_previewImageAspectRatioFitter == null) Debug.LogError("UIManager: PreviewPanel/PreviewImage AspectRatioFitter is missing.");
        if (_closePreviewButton == null) Debug.LogError("UIManager: PreviewPanel/ClosePreviewButton is missing.");
    }

    private void BindButtonCallbacks()
    {
        BindButton(_resetPuzzleButton, () => _gameManager.ResetPuzzle());
        BindButton(_previewToggleButton, () => _gameManager.TogglePreview());
        BindButton(_newImageButton, () => _gameManager.LoadRandomImage());
        BindButton(_closePreviewButton, () =>
        {
            if (IsPreviewVisible)
            {
                _gameManager.TogglePreview();
            }
        });
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
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
