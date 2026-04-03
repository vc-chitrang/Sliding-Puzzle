using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Two-way sync between a UI Slider and <see cref="CropGridResizer"/>.
///
///   Slider → CropGridResizer  (user drags slider)
///   CropGridResizer → Slider  (user drags corner handles)
///
/// Slider semantics:
///   0 = minimum crop size (20% of initial)
///   1 = maximum crop size (initial / default)
///
/// Reset button is only interactable when size &lt; max (slider &lt; 1).
/// </summary>
public class ImageZoomController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Slider controlling crop grid size. Range 0 (min) to 1 (max/default).")]
    [SerializeField] private Slider zoomSlider;

    [Tooltip("Button that resets crop grid to default (max) size.")]
    [SerializeField] private Button resetButton;

    [Tooltip("The CropGridResizer that manages the crop grid.")]
    [SerializeField] private CropGridResizer cropGridResizer;

    // ── Loop guard ───────────────────────────────────────────────────
    private bool _isUpdatingFromCode;

    // ─────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        // Subscribe to grid size changes from drag handles
        if (cropGridResizer != null)
            cropGridResizer.OnNormalizedSizeChanged += OnGridSizeChanged;

        // Subscribe to slider changes (user drags slider)
        if (zoomSlider != null)
        {
            zoomSlider.onValueChanged.AddListener(OnSliderChanged);
            zoomSlider.value = 1f; // Start at max (full size)
        }

        if (resetButton != null)
            resetButton.onClick.AddListener(ResetSize);

        SyncResetButton();
    }

    private void OnDisable()
    {
        if (cropGridResizer != null)
            cropGridResizer.OnNormalizedSizeChanged -= OnGridSizeChanged;

        if (zoomSlider != null)
            zoomSlider.onValueChanged.RemoveListener(OnSliderChanged);

        if (resetButton != null)
            resetButton.onClick.RemoveListener(ResetSize);
    }

    // ─────────────────────────────────────────────────────────────────
    // Slider → Grid (user drags slider)
    // ─────────────────────────────────────────────────────────────────

    private void OnSliderChanged(float value)
    {
        if (_isUpdatingFromCode) return;

        _isUpdatingFromCode = true;

        if (cropGridResizer != null)
            cropGridResizer.SetNormalizedSize(value);

        _isUpdatingFromCode = false;
        SyncResetButton();
    }

    // ─────────────────────────────────────────────────────────────────
    // Grid → Slider (user drags corner handles)
    // ─────────────────────────────────────────────────────────────────

    private void OnGridSizeChanged(float normalizedSize)
    {
        if (_isUpdatingFromCode) return;

        _isUpdatingFromCode = true;

        if (zoomSlider != null)
            zoomSlider.value = normalizedSize;

        _isUpdatingFromCode = false;
        SyncResetButton();
    }

    // ─────────────────────────────────────────────────────────────────
    // Reset
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Resets the crop grid to its initial (maximum) size.
    /// </summary>
    public void ResetSize()
    {
        _isUpdatingFromCode = true;

        if (zoomSlider != null)
            zoomSlider.value = 1f;

        if (cropGridResizer != null)
            cropGridResizer.ResetToInitialSize();

        _isUpdatingFromCode = false;
        SyncResetButton();
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private void SyncResetButton()
    {
        if (resetButton != null)
            resetButton.interactable = zoomSlider != null && zoomSlider.value < 0.99f;
    }
}
