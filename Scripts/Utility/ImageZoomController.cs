using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls zoom level of ImageToCrop via a UI Slider,
/// with a Reset button that restores the default (fit) scale.
///
/// Slider range 0..1 maps to:
///   0 = original best-fit scale (minScale)
///   1 = maxZoomFactor x minScale
///
/// The Reset button is only interactable when slider > 0.
/// </summary>
public class ImageZoomController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Slider controlling zoom. Range 0 (fit) to 1 (max zoom).")]
    [SerializeField] private Slider zoomSlider;

    [Tooltip("Button that resets zoom to default.")]
    [SerializeField] private Button resetButton;

    [Tooltip("The RectTransform of the image being zoomed (ScrollRect content).")]
    [SerializeField] private RectTransform imageToCrop;

    [Header("Zoom Settings")]
    [Tooltip("Maximum zoom multiplier relative to fit scale (e.g. 3 = 3x).")]
    [SerializeField] private float maxZoomFactor = 3f;

    // ── Runtime state ────────────────────────────────────────────────
    private float _minScale = 1f;
    private bool _initialized;

    private void Start()
    {
        Initialize();
    }

    /// <summary>
    /// Call once after the image is loaded / layout is built.
    /// Captures the current scale as the "fit" baseline.
    /// Safe to call multiple times (re-initializes).
    /// </summary>
    public void Initialize()
    {
        if (imageToCrop == null)
        {
            Debug.LogWarning("ImageZoomController: imageToCrop is not assigned.");
            return;
        }

        // Current scale is the "best fit" baseline
        _minScale = imageToCrop.localScale.x;
        if (_minScale <= 0f) _minScale = 1f;

        _initialized = true;

        // Wire events
        if (zoomSlider != null)
        {
            zoomSlider.onValueChanged.RemoveListener(OnSliderChanged);
            zoomSlider.onValueChanged.AddListener(OnSliderChanged);
            zoomSlider.value = 0f;
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(ResetZoom);
            resetButton.onClick.AddListener(ResetZoom);
        }

        // Apply initial state
        ApplyZoom(0f);
        SyncResetButton();
    }

    /// <summary>
    /// Called when the slider value changes.
    /// </summary>
    public void OnSliderChanged(float value)
    {
        if (!_initialized) return;

        ApplyZoom(value);
        SyncResetButton();
    }

    /// <summary>
    /// Resets zoom to fit scale and slider to 0.
    /// </summary>
    public void ResetZoom()
    {
        if (zoomSlider != null)
        {
            zoomSlider.value = 0f; // triggers OnSliderChanged
        }
        else
        {
            ApplyZoom(0f);
            SyncResetButton();
        }
    }

    // ── Private helpers ──────────────────────────────────────────────

    private void ApplyZoom(float sliderValue)
    {
        if (imageToCrop == null) return;

        float targetScale = Mathf.Lerp(_minScale, _minScale * maxZoomFactor, sliderValue);
        imageToCrop.localScale = Vector3.one * targetScale;
    }

    /// <summary>
    /// Reset button is interactable only when zoomed beyond default.
    /// </summary>
    private void SyncResetButton()
    {
        if (resetButton != null)
        {
            resetButton.interactable = zoomSlider != null && zoomSlider.value > 0f;
        }
    }
}
