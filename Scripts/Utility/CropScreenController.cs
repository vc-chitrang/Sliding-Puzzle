using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the two states of CropImageScreen:
///   1. Edit state  — crop controls visible, cropped-image card hidden.
///   2. Done state  — crop controls hidden, cropped-image card + START button visible.
///
/// Also handles Back navigation and the START → GamePlayScreen transition.
/// Attach this component to the CropImageScreen root GameObject.
/// </summary>
public class CropScreenController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────────────────────

    [Header("Edit Controls (hidden after crop)")]
    [SerializeField] private GameObject cropImageButtonGO;
    [SerializeField] private GameObject resetButtonGO;
    [SerializeField] private GameObject zoomSliderGO;
    [Tooltip("The entire crop area (image + grid overlay + corner handles).")]
    [SerializeField] private GameObject cropAreaBackgroundGO;

    [Header("Post-Crop Card")]
    [Tooltip("Root panel that holds the cropped image card. Initially inactive.")]
    [SerializeField] private GameObject croppedCardGO;
    [SerializeField] private Image     croppedImageDisplay;
    [SerializeField] private TextMeshProUGUI cardTitleText;
    [SerializeField] private TextMeshProUGUI cardArtistText;
    [SerializeField] private TextMeshProUGUI cardAccessionText;

    [Header("Post-Crop")]
    [Tooltip("Green START button shown after cropping.")]
    [SerializeField] private Button startButton;

    [Header("Screens")]
    [SerializeField] private GameObject cropImageScreen;
    [SerializeField] private GameObject gamePlayScreen;
    [SerializeField] private GameObject collectionScreen;

    [Header("Navigation")]
    [SerializeField] private Button backButton;

    [Header("Game")]
    [SerializeField] private GameManager gameManager;

    // ─────────────────────────────────────────────────────────────────
    // Runtime state
    // ─────────────────────────────────────────────────────────────────

    private Sprite _croppedSprite;
    private string _artworkTitle;
    private string _artworkArtist;
    private string _artworkAccession;

    // ─────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (startButton != null)
            startButton.onClick.AddListener(OnStartPressed);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackPressed);
    }

    private void OnEnable()
    {
        // Every time this screen activates, reset to the edit state
        ResetToEditState();
    }

    // ─────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by CollectionUIManager before the crop screen opens.
    /// Stores artwork metadata for display after crop.
    /// </summary>
    public void SetArtworkData(ResultsData data)
    {
        _artworkTitle     = data?.title ?? "";
        _artworkAccession = data?.accession_number ?? "";

        // First artist name, or "Unknown"
        if (data?.artists != null && data.artists.Count > 0 && !string.IsNullOrEmpty(data.artists[0]?.name))
            _artworkArtist = data.artists[0].name;
        else
            _artworkArtist = "Unknown";
    }

    /// <summary>
    /// Called by ImageCropper after a successful crop.
    /// Hides edit controls + crop area, shows the cropped-image card and START button.
    /// </summary>
    public void OnCropComplete(Sprite croppedSprite)
    {
        _croppedSprite = croppedSprite;

        // Hide all edit controls (includes corner handles via their parent)
        SetEditControlsVisible(false);

        // Populate and reveal the post-crop card
        ShowCroppedCard(croppedSprite);

        // Show START button
        if (startButton != null) startButton.gameObject.SetActive(true);
    }

    // ─────────────────────────────────────────────────────────────────
    // Private
    // ─────────────────────────────────────────────────────────────────

    private void ResetToEditState()
    {
        _croppedSprite = null;

        // Restore edit controls
        SetEditControlsVisible(true);

        // Hide post-crop card and START button
        if (croppedCardGO != null) croppedCardGO.SetActive(false);
        if (startButton   != null) startButton.gameObject.SetActive(false);
    }

    private void SetEditControlsVisible(bool visible)
    {
        if (cropImageButtonGO   != null) cropImageButtonGO.SetActive(visible);
        if (resetButtonGO       != null) resetButtonGO.SetActive(visible);
        if (zoomSliderGO        != null) zoomSliderGO.SetActive(visible);
        if (cropAreaBackgroundGO != null) cropAreaBackgroundGO.SetActive(visible);
    }

    private void ShowCroppedCard(Sprite sprite)
    {
        if (croppedCardGO == null) return;
        croppedCardGO.SetActive(true);

        if (croppedImageDisplay != null)
        {
            croppedImageDisplay.sprite = sprite;
            croppedImageDisplay.preserveAspect = true;
        }

        if (cardTitleText     != null) cardTitleText.text     = _artworkTitle;
        if (cardArtistText    != null) cardArtistText.text    = _artworkArtist;
        if (cardAccessionText != null) cardAccessionText.text = _artworkAccession;
    }

    private void OnStartPressed()
    {
        if (gamePlayScreen  != null) gamePlayScreen.SetActive(true);
        if (cropImageScreen != null) cropImageScreen.SetActive(false);
        if (gameManager != null && _croppedSprite != null)
            gameManager.StartPuzzleWithCroppedSprite(_croppedSprite);
    }

    private void OnBackPressed()
    {
        if (cropImageScreen  != null) cropImageScreen.SetActive(false);
        if (collectionScreen != null) collectionScreen.SetActive(true);
    }
}
