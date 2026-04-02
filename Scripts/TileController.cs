using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI-based puzzle tile.  Uses <see cref="UnityEngine.UI.Image"/> + Sprite
/// slicing instead of MeshRenderer + UV manipulation.
///
/// Movement uses <see cref="RectTransform.anchoredPosition"/>.
/// Tile click detection uses <see cref="Button"/> OnClick.
///
/// Shuffle / win-detection / swap logic in GameManager is untouched.
/// </summary>
[RequireComponent(typeof(Image))]
[RequireComponent(typeof(Button))]
public class TileController : MonoBehaviour
{
    // ── Cached references ────────────────────────────────────────────
    private RectTransform _rectTransform;
    private Image _tileImage;
    private Button _button;
    private Coroutine _moveRoutine;

    // ── Tile data ────────────────────────────────────────────────────
    public int Index { get; private set; }
    public Vector2Int CorrectCell { get; private set; }
    public Vector2Int CurrentCell { get; private set; }
    public bool IsInCorrectPosition => CurrentCell == CorrectCell;

    /// <summary>
    /// Raised when the tile is clicked/tapped.  GameManager subscribes.
    /// </summary>
    public event System.Action<TileController> OnTileClicked;

    // ─────────────────────────────────────────────────────────────────
    // Initialization
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Set up a UI tile with its sprite slice and grid position.
    /// Called by GameManager during board spawn.
    /// </summary>
    public void Initialize(
        int tileIndex,
        Vector2Int correctCell,
        Vector2Int currentCell,
        Sprite tileSprite)
    {
        CacheComponents();

        Index = tileIndex;
        CorrectCell = correctCell;
        CurrentCell = currentCell;
        name = $"Tile_{tileIndex:00}";

        // Assign the sprite slice for this tile
        _tileImage.sprite = tileSprite;
        _tileImage.preserveAspect = false;
        _tileImage.type = Image.Type.Simple;

        // Wire click
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => OnTileClicked?.Invoke(this));
    }

    // ─────────────────────────────────────────────────────────────────
    // Public API (called by GameManager — same interface as before)
    // ─────────────────────────────────────────────────────────────────

    public void SetCurrentCell(Vector2Int cell)
    {
        CurrentCell = cell;
    }

    /// <summary>
    /// Instantly move tile to a target anchored position.
    /// </summary>
    public void SnapTo(Vector2 anchoredPosition)
    {
        if (_moveRoutine != null)
        {
            StopCoroutine(_moveRoutine);
            _moveRoutine = null;
        }

        CacheComponents();
        _rectTransform.anchoredPosition = anchoredPosition;
    }

    /// <summary>
    /// Smoothly animate tile to a target anchored position.
    /// </summary>
    public IEnumerator AnimateTo(Vector2 targetPosition, float duration)
    {
        if (_moveRoutine != null)
        {
            StopCoroutine(_moveRoutine);
        }

        _moveRoutine = StartCoroutine(AnimateRoutine(targetPosition, duration));
        yield return _moveRoutine;
        _moveRoutine = null;
    }

    // ─────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────

    private IEnumerator AnimateRoutine(Vector2 targetPosition, float duration)
    {
        CacheComponents();
        Vector2 start = _rectTransform.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            _rectTransform.anchoredPosition = Vector2.LerpUnclamped(start, targetPosition, eased);
            yield return null;
        }

        _rectTransform.anchoredPosition = targetPosition;
    }

    private void CacheComponents()
    {
        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();
        if (_tileImage == null)
            _tileImage = GetComponent<Image>();
        if (_button == null)
            _button = GetComponent<Button>();
    }
}
