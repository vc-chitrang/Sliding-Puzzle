using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Custom masonry (Pinterest-style) layout controller for the artwork card grid.
///
/// ── Algorithm ────────────────────────────────────────────────────────────────
///  1. Determine column count from container pixel width (mobile / tablet / desktop).
///  2. For each active card:
///       a. Read the loaded sprite's aspect ratio (w ÷ h).  Falls back to a
///          configurable default while the image is still downloading.
///       b. imageHeight = Clamp( columnWidth / aspect, min, max )
///       c. cardHeight  = imageHeight + cardTextHeight  (fixed text area below image)
///  3. Find the column with the smallest running height → place card there.
///       anchoredPosition = ( paddingLeft + col * (colWidth + colSpacing),
///                            -columnHeights[col] )
///       sizeDelta        = ( colWidth, cardHeight )
///  4. columnHeights[col] += cardHeight + rowSpacing
///  5. Resize the container RectTransform so the ScrollRect can scroll it.
///
/// ── Integration ──────────────────────────────────────────────────────────────
///  • Attach to the ScrollRect's Content RectTransform (same GameObject as
///    cardGridContent in CollectionUIManager).
///  • Assign the reference in CollectionUIManager and call SetCards() from
///    DisplayCards() after binding/clearing the pool.
///  • CardItemUI fires the static event OnAnyImageLoaded whenever a new sprite
///    is applied; MasonryLayoutGroup subscribes and marks the layout dirty so
///    the masonry grid updates to the real aspect ratio automatically.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MasonryLayoutGroup : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────
    // Inspector — Responsive column counts
    // ─────────────────────────────────────────────────────────────────

    [Header("Columns (responsive to container width)")]
    [Tooltip("Columns when container width < mobileBreakpoint.")]
    [SerializeField] private int mobileColumns  = 2;
    [Tooltip("Columns between mobile and tablet breakpoints.")]
    [SerializeField] private int tabletColumns  = 3;
    [Tooltip("Columns between tablet and widescreen breakpoints.")]
    [SerializeField] private int desktopColumns = 4;
    [Tooltip("Columns at 4K / ultra-wide (>= widescreenBreakpoint).")]
    [SerializeField] private int widescreenColumns = 5;
    [SerializeField] private float mobileBreakpoint     = 600f;
    [SerializeField] private float tabletBreakpoint     = 1200f;
    [SerializeField] private float widescreenBreakpoint = 1800f;

    // ─────────────────────────────────────────────────────────────────
    // Inspector — Spacing & Padding
    // ─────────────────────────────────────────────────────────────────

    [Header("Spacing & Padding (canvas pixels)")]
    [SerializeField] private float columnSpacing = 16f;   // horizontal gap between columns
    [SerializeField] private float rowSpacing    = 16f;   // vertical gap between cards
    [SerializeField] private float paddingLeft   = 16f;
    [SerializeField] private float paddingRight  = 16f;
    [SerializeField] private float paddingTop    = 16f;
    [SerializeField] private float paddingBottom = 40f;

    // ─────────────────────────────────────────────────────────────────
    // Inspector — Card Height Metrics
    // ─────────────────────────────────────────────────────────────────

    [Header("Card Height Metrics (canvas pixels)")]
    [Tooltip("Fixed height of the text area below the image " +
             "(title 70 + artist 35 + accession 30 + VLG top/bot padding 20 + 3×spacing 24 = 179).")]
    [SerializeField] private float cardTextHeight    = 179f;
    [Tooltip("Minimum image area height per card (floor for extreme landscape images).")]
    [SerializeField] private float minImageHeight = 80f;
    [Tooltip("Maximum image area height (0 = unlimited — image drives its own height " +
             "from aspect ratio, which is the correct masonry behaviour).")]
    [SerializeField] private float maxImageHeight = 0f;   // 0 = no upper cap
    [Tooltip("Fallback aspect ratio (width÷height) used while the real image is loading.")]
    [SerializeField] private float defaultAspectRatio = 0.75f;  // portrait default

    // ─────────────────────────────────────────────────────────────────
    // Private state
    // ─────────────────────────────────────────────────────────────────

    private RectTransform _rt;
    private readonly List<CardItemUI> _cards = new List<CardItemUI>();

    // Running height of each column (reset on every layout pass)
    private float[] _colHeights;

    // Guard: avoid full recalc if nothing changed except a scroll
    private float _lastContainerWidth = -1f;
    private int   _lastColumnCount    = -1;

    // Dirty flag — set by any image-load event or container resize
    private bool _dirty;

    // ─────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Replaces the tracked card list and immediately schedules a layout pass.
    /// Call this from CollectionUIManager.DisplayCards() after Bind/Clear.
    /// </summary>
    public void SetCards(List<CardItemUI> cards)
    {
        _cards.Clear();
        if (cards != null) _cards.AddRange(cards);
        MarkDirty();
    }

    /// <summary>Marks the layout as dirty so it recalculates in LateUpdate.</summary>
    public void MarkDirty() => _dirty = true;

    // ─────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();

        // Ensure this content RectTransform is anchored to the top of its parent
        // (top-stretch) so it expands downward as content grows, which is required
        // for a vertical ScrollRect to be scrollable.
        _rt.anchorMin = new Vector2(0f, 1f);
        _rt.anchorMax = new Vector2(1f, 1f);
        _rt.pivot     = new Vector2(0.5f, 1f);
        _rt.anchoredPosition = Vector2.zero;
        // x=0 → let anchors handle width; y will be set by CalculateLayout
        _rt.sizeDelta = new Vector2(0f, _rt.sizeDelta.y);
    }

    private void OnEnable()
    {
        // Refresh whenever any card finishes loading its image
        CardItemUI.OnAnyImageLoaded += MarkDirty;
    }

    private void OnDisable()
    {
        CardItemUI.OnAnyImageLoaded -= MarkDirty;
    }

    private void LateUpdate()
    {
        if (!_dirty) return;
        _dirty = false;
        CalculateLayout();
    }

    // Called by Unity when this RectTransform's size changes
    // (scroll view resize, orientation change, canvas scale change)
    private void OnRectTransformDimensionsChange()
    {
        float newW  = _rt != null ? _rt.rect.width : 0f;
        int   newC  = GetColumnCount(newW);

        // Only recalculate if the column count changed or width shifted meaningfully
        if (newC != _lastColumnCount || Mathf.Abs(newW - _lastContainerWidth) > 2f)
            MarkDirty();
    }

    // ─────────────────────────────────────────────────────────────────
    // Layout engine
    // ─────────────────────────────────────────────────────────────────

    private void CalculateLayout()
    {
        if (_cards == null || _cards.Count == 0) return;

        float containerW = _rt.rect.width;
        if (containerW <= 0f) return;

        // ── Column metrics ──────────────────────────────────────────
        int   cols   = GetColumnCount(containerW);
        float usable = containerW - paddingLeft - paddingRight
                       - columnSpacing * (cols - 1);
        float colW   = Mathf.Max(1f, usable / cols);

        // ── Reset per-column running heights ────────────────────────
        if (_colHeights == null || _colHeights.Length != cols)
            _colHeights = new float[cols];
        for (int i = 0; i < cols; i++)
            _colHeights[i] = paddingTop;

        // ── Position each active card ───────────────────────────────
        foreach (CardItemUI card in _cards)
        {
            if (card == null || !card.gameObject.activeSelf) continue;

            RectTransform cardRT = card.GetComponent<RectTransform>();
            if (cardRT == null) continue;

            // Anchor card to top-left of the container so anchoredPosition
            // is measured from the top-left corner.
            cardRT.anchorMin = new Vector2(0f, 1f);
            cardRT.anchorMax = new Vector2(0f, 1f);
            cardRT.pivot     = new Vector2(0f, 1f);

            // ── Compute card dimensions ─────────────────────────────
            // Use the real sprite aspect ratio (width÷height) if available.
            // imgH = columnWidth / aspect  →  preserves the image's exact proportions.
            // No upper cap by default: a 339×1500 portrait image must produce a
            // tall card, not a squashed one.
            float aspect   = card.GetImageAspectRatio(defaultAspectRatio);
            float rawImgH  = colW / Mathf.Max(aspect, 0.01f);   // guard against division by zero
            float imgH     = maxImageHeight > 0f
                             ? Mathf.Clamp(rawImgH, minImageHeight, maxImageHeight)
                             : Mathf.Max(rawImgH, minImageHeight);
            float cardH    = imgH + cardTextHeight;

            // Resize card RectTransform
            cardRT.sizeDelta = new Vector2(colW, cardH);

            // Tell the card to update its internal image LayoutElement height
            // so the VLG distributes space correctly
            card.SetImageHeight(imgH);

            // ── Shortest-column placement ───────────────────────────
            int   col  = ShortestColumn();
            float xPos = paddingLeft + col * (colW + columnSpacing);
            float yPos = -_colHeights[col]; // negative = downward in UI space

            cardRT.anchoredPosition = new Vector2(xPos, yPos);

            // Advance this column's running height
            _colHeights[col] += cardH + rowSpacing;
        }

        // ── Resize container so ScrollRect can scroll the full content ──
        // sizeDelta.x = 0 because anchorMin/Max.x = 0/1 (full-width stretch).
        // sizeDelta.y = totalH makes the container exactly as tall as all cards.
        float totalH = MaxColumnHeight() + paddingBottom;
        _rt.sizeDelta = new Vector2(0f, totalH);

        // Cache for change-detection in OnRectTransformDimensionsChange
        _lastContainerWidth = containerW;
        _lastColumnCount    = cols;
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private int GetColumnCount(float width)
    {
        if (width < mobileBreakpoint)     return mobileColumns;
        if (width < tabletBreakpoint)     return tabletColumns;
        if (width < widescreenBreakpoint) return desktopColumns;
        return widescreenColumns;   // 4K / ultra-wide
    }

    /// <summary>Returns the index of the column with the smallest running height.</summary>
    private int ShortestColumn()
    {
        int   idx = 0;
        float min = _colHeights[0];
        for (int i = 1; i < _colHeights.Length; i++)
        {
            if (_colHeights[i] < min) { min = _colHeights[i]; idx = i; }
        }
        return idx;
    }

    /// <summary>Returns the tallest column height (used for container resize).</summary>
    private float MaxColumnHeight()
    {
        float max = 0f;
        foreach (float h in _colHeights)
            if (h > max) max = h;
        return max;
    }
}
