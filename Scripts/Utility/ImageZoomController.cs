using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Two-way zoom sync between a UI Slider and <see cref="PinchableScrollRect"/>.
///
///   Slider → PinchableScrollRect  (user drags slider)
///   PinchableScrollRect → Slider  (user pinches / scrolls)
///
/// Uses <c>_isUpdatingFromCode</c> flag to prevent infinite loops.
/// Reset button is only interactable when slider > 0.
/// </summary>
public class ImageZoomController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Slider controlling zoom. Range 0 (min zoom) to 1 (max zoom).")]
    [SerializeField] private Slider zoomSlider;

    [Tooltip("Button that resets zoom to default.")]
    [SerializeField] private Button resetButton;

    [Tooltip("The PinchableScrollRect that handles pinch/scroll zoom.")]
    [SerializeField] private PinchableScrollRect pinchableScrollRect;

    // ── Loop guard ───────────────────────────────────────────────────
    private bool _isUpdatingFromCode;

    // ─────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        // Subscribe to PinchableScrollRect zoom changes (pinch / scroll)
        if (pinchableScrollRect != null)
        {
            pinchableScrollRect.OnZoomChanged += OnPinchableZoomChanged;
        }

        // Subscribe to slider changes (user drags slider)
        if (zoomSlider != null)
        {
            zoomSlider.onValueChanged.AddListener(OnSliderChanged);
            zoomSlider.value = 0f;
        }

        if (resetButton != null)
        {
            resetButton.onClick.AddListener(ResetZoom);
        }

        SyncResetButton();
    }

    private void OnDisable()
    {
        if (pinchableScrollRect != null)
        {
            pinchableScrollRect.OnZoomChanged -= OnPinchableZoomChanged;
        }

        if (zoomSlider != null)
        {
            zoomSlider.onValueChanged.RemoveListener(OnSliderChanged);
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(ResetZoom);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Slider → Zoom (user drags slider)
    // ─────────────────────────────────────────────────────────────────

    private void OnSliderChanged(float value)
    {
        if (_isUpdatingFromCode) return;

        _isUpdatingFromCode = true;

        if (pinchableScrollRect != null)
        {
            pinchableScrollRect.SetNormalizedZoom(value);
        }

        _isUpdatingFromCode = false;
        SyncResetButton();
    }

    // ─────────────────────────────────────────────────────────────────
    // Zoom → Slider (user pinches or scrolls)
    // ─────────────────────────────────────────────────────────────────

    private void OnPinchableZoomChanged(float normalizedZoom)
    {
        if (_isUpdatingFromCode) return;

        _isUpdatingFromCode = true;

        if (zoomSlider != null)
        {
            zoomSlider.value = normalizedZoom;
        }

        _isUpdatingFromCode = false;
        SyncResetButton();
    }

    // ─────────────────────────────────────────────────────────────────
    // Reset
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Resets zoom to minimum and slider to 0.
    /// </summary>
    public void ResetZoom()
    {
        if (zoomSlider != null)
        {
            zoomSlider.value = 0f; // triggers OnSliderChanged → SetNormalizedZoom
        }
        else if (pinchableScrollRect != null)
        {
            pinchableScrollRect.SetNormalizedZoom(0f);
        }

        SyncResetButton();
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private void SyncResetButton()
    {
        if (resetButton != null)
        {
            resetButton.interactable = zoomSlider != null && zoomSlider.value > 0f;
        }
    }
}
