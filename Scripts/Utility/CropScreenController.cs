using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the Crop_Image_Screen.
///
/// States:
///   • Edit   — crop controls visible (entered via OnEnable).
///   • Done   — after Crop() completes; navigates directly to Artwork_Focus_Screen.
///
/// The old post-crop card (CropResultPanel) has been removed.
/// Artwork details are now displayed on the dedicated Artwork_Focus_Screen.
/// </summary>
public class CropScreenController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────────────────────

    [Header("Edit Controls (hidden when transitioning away)")]
    [SerializeField] private GameObject cropImageButtonGO;
    [SerializeField] private GameObject resetButtonGO;
    [SerializeField] private GameObject zoomSliderGO;
    [Tooltip("The entire crop area (image + grid overlay + corner handles).")]
    [SerializeField] private GameObject cropAreaBackgroundGO;

    [Header("Navigation")]
    [SerializeField] private Button backButton;

    [Header("Screens — fallback when ScreenManager is absent")]
    [SerializeField] private GameObject cropImageScreen;
    [SerializeField] private GameObject collectionScreen;

    [Header("Artwork Focus Screen")]
    [Tooltip("Root GameObject of the Artwork_Focus_Screen.")]
    [SerializeField] private GameObject artworkFocusScreen;
    [Tooltip("Controller on the Artwork_Focus_Screen that receives the cropped sprite and metadata.")]
    [SerializeField] private ArtworkFocusScreenController artworkFocusController;

    // ─────────────────────────────────────────────────────────────────
    // Runtime state
    // ─────────────────────────────────────────────────────────────────

    private string _artworkTitle;
    private string _artworkArtist;
    private string _artworkAccession;

    // ─────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (backButton != null)
            backButton.onClick.AddListener(OnBackPressed);
    }

    private void OnEnable()
    {
        // Always restore edit controls when this screen becomes visible
        ResetToEditState();
    }

    // ─────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by CollectionUIManager before the crop screen opens.
    /// Stores artwork metadata so it can be forwarded to ArtworkFocusScreen after crop.
    /// </summary>
    public void SetArtworkData(ResultsData data)
    {
        _artworkTitle     = data?.title            ?? string.Empty;
        _artworkAccession = data?.accession_number ?? string.Empty;

        if (data?.artists != null && data.artists.Count > 0 &&
            !string.IsNullOrEmpty(data.artists[0]?.name))
            _artworkArtist = data.artists[0].name;
        else
            _artworkArtist = "Unknown";
    }

    /// <summary>
    /// Called by ImageCropper after a successful crop.
    /// Populates ArtworkFocusScreen with the cropped sprite and metadata,
    /// then navigates there.
    /// </summary>
    public void OnCropComplete(Sprite croppedSprite)
    {
        // Populate the focus screen before showing it
        if (artworkFocusController != null)
            artworkFocusController.SetData(croppedSprite, _artworkTitle, _artworkArtist, _artworkAccession);

        if (ScreenManager.Instance != null)
        {
            ScreenManager.Instance.ShowScreen(artworkFocusScreen);
        }
        else
        {
            if (cropImageScreen  != null) cropImageScreen.SetActive(false);
            if (artworkFocusScreen != null) artworkFocusScreen.SetActive(true);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Private
    // ─────────────────────────────────────────────────────────────────

    private void ResetToEditState()
    {
        SetEditControlsVisible(true);
    }

    private void SetEditControlsVisible(bool visible)
    {
        if (cropImageButtonGO    != null) cropImageButtonGO.SetActive(visible);
        if (resetButtonGO        != null) resetButtonGO.SetActive(visible);
        if (zoomSliderGO         != null) zoomSliderGO.SetActive(visible);
        if (cropAreaBackgroundGO != null) cropAreaBackgroundGO.SetActive(visible);
    }

    private void OnBackPressed()
    {
        if (ScreenManager.Instance != null)
        {
            ScreenManager.Instance.GoBack();
        }
        else
        {
            if (cropImageScreen  != null) cropImageScreen.SetActive(false);
            if (collectionScreen != null) collectionScreen.SetActive(true);
        }
    }
}
