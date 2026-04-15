using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Artwork_Focus_Screen controller.
///
/// Displays the cropped artwork image together with metadata (title, artist,
/// accession number). Provides two buttons:
///   • "Start Puzzle"  — passes the cropped sprite to GameManager and opens
///                       the Puzzle_Screen.
///   • "Back"          — returns to the previous screen via ScreenManager.
///
/// Populated by <see cref="CropScreenController.OnCropComplete"/> before this
/// screen is shown.
/// </summary>
public class ArtworkFocusScreenController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────────────────────

    [Header("Artwork Display")]
    [SerializeField] private Image artworkImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI artistText;
    [SerializeField] private TextMeshProUGUI accessionText;

    [Header("Buttons")]
    [SerializeField] private Button startPuzzleButton;
    [SerializeField] private Button backButton;

    [Header("Screens")]
    [Tooltip("The Puzzle_Screen root. Shown when 'Start Puzzle' is pressed.")]
    [SerializeField] private GameObject puzzleScreen;
    [Tooltip("Fallback: the Crop screen to restore when GoBack() is unavailable.")]
    [SerializeField] private GameObject cropImageScreen;

    [Header("References")]
    [SerializeField] private GameManager gameManager;

    // ─────────────────────────────────────────────────────────────────
    // Runtime state
    // ─────────────────────────────────────────────────────────────────

    private Sprite _croppedSprite;

    // ─────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (startPuzzleButton != null)
            startPuzzleButton.onClick.AddListener(OnStartPuzzlePressed);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackPressed);
    }

    // ─────────────────────────────────────────────────────────────────
    // Public API — called by CropScreenController before ShowScreen()
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Populate the screen with the cropped sprite and artwork metadata.
    /// Must be called before this screen becomes visible.
    /// </summary>
    public void SetData(Sprite croppedSprite, string title, string artist, string accession)
    {
        _croppedSprite = croppedSprite;

        if (artworkImage != null)
        {
            artworkImage.sprite         = croppedSprite;
            artworkImage.preserveAspect = true;
        }

        if (titleText     != null) titleText.text     = title     ?? string.Empty;
        if (artistText    != null) artistText.text    = artist    ?? string.Empty;
        if (accessionText != null) accessionText.text = accession ?? string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────
    // Button handlers
    // ─────────────────────────────────────────────────────────────────

    private void OnStartPuzzlePressed()
    {
        if (_croppedSprite == null)
        {
            Debug.LogWarning("[ArtworkFocusScreen] No cropped sprite set — cannot start puzzle.");
            return;
        }

        // Activate the puzzle screen immediately so its children (arrows) are active
        // before BuildPuzzle runs — ArrowController.SetVisible starts a coroutine
        // that requires an active GameObject.
        if (puzzleScreen != null && !puzzleScreen.activeSelf)
            puzzleScreen.SetActive(true);

        // Build the puzzle while the screen is guaranteed active
        if (gameManager != null)
            gameManager.StartPuzzleWithCroppedSprite(_croppedSprite);

        // Visual fade transition — screen is already active, ScreenManager just
        // manages the navigation stack and animates the overlay.
        if (ScreenManager.Instance != null)
            ScreenManager.Instance.ShowScreen(puzzleScreen);
    }

    private void OnBackPressed()
    {
        if (ScreenManager.Instance != null)
            ScreenManager.Instance.GoBack();
        else
        {
            gameObject.SetActive(false);
            if (cropImageScreen != null) cropImageScreen.SetActive(true);
        }
    }
}
