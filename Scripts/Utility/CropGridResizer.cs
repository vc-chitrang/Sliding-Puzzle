using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Manages a resizable square crop grid with 4 corner drag handles.
///
/// The grid (<see cref="gridRect"/>) always remains square (1:1) and is
/// clamped inside the visible bounds of <see cref="imageRect"/> (which
/// uses preserveAspect).
///
/// Slider integration is handled via <see cref="OnNormalizedSizeChanged"/>
/// and <see cref="SetNormalizedSize"/>.
/// </summary>
public class CropGridResizer : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The RectTransform of the square crop grid overlay.")]
    [SerializeField] private RectTransform gridRect;

    [Tooltip("The RectTransform of the image being cropped.")]
    [SerializeField] private RectTransform imageRect;

    [Tooltip("The Image component on ImageToCrop (to read sprite aspect ratio).")]
    [SerializeField] private Image imageToCropImage;

    [Header("Handle Settings")]
    [Tooltip("Visual size of each corner drag handle (pixels at reference resolution).")]
    [SerializeField] private float handleVisualSize = 80f;

    [Tooltip("Color of the corner drag handles.")]
    [SerializeField] private Color handleColor = new Color(1f, 1f, 1f, 0.9f);

    [Header("Size Limits")]
    [Tooltip("Minimum grid size as a fraction of the initial size (0.2 = 20%).")]
    [SerializeField, Range(0.1f, 0.5f)] private float minSizeFraction = 0.2f;
    [SerializeField] private bool canShowZoomHandle = true;

    // ─────────────────────────────────────────────────────────────────
    // Events
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fires when the grid size changes via drag handles.
    /// Value is normalized: 0 = minSize, 1 = maxSize (initial).
    /// </summary>
    public event Action<float> OnNormalizedSizeChanged;

    // ─────────────────────────────────────────────────────────────────
    // Runtime state
    // ─────────────────────────────────────────────────────────────────

    private float _initialSize;
    private float _minSize;
    private float _currentSize;
    private bool _initialized;
    private bool _isUpdatingFromCode;

    // 4 corner handle RectTransforms (BL, TL, TR, BR — Unity corner order)
    private RectTransform[] _handles;
    private Canvas _rootCanvas;

    // Cached visible image bounds (world space)
    private Vector2 _visibleImageMin;
    private Vector2 _visibleImageMax;

    // ─────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────

    public float NormalizedSize
    {
        get
        {
            if (_initialSize <= _minSize) return 1f;
            return Mathf.InverseLerp(_minSize, _initialSize, _currentSize);
        }
    }

    /// <summary>
    /// Sets the grid size from a normalized 0..1 value.
    /// 0 = minSize (20%), 1 = maxSize (initial).
    /// </summary>
    public void SetNormalizedSize(float normalized01)
    {
        if (!_initialized) return;

        float newSize = Mathf.Lerp(_minSize, _initialSize, Mathf.Clamp01(normalized01));
        ApplySize(newSize);
    }

    /// <summary>Restores the grid to its initial (maximum) size.</summary>
    public void ResetToInitialSize()
    {
        if (!_initialized) return;
        ApplySize(_initialSize);
    }

    /// <summary>
    /// Re-computes initial size from the current image.
    /// Call after the sprite on ImageToCrop changes.
    /// </summary>
    public void Reinitialize()
    {
        Initialize();
    }

    // ─────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        // Delay one frame so Canvas layout is finalized
        StartCoroutine(InitializeNextFrame());
    }

    private System.Collections.IEnumerator InitializeNextFrame()
    {
        yield return null; // wait for layout
        Canvas.ForceUpdateCanvases();
        Initialize();
    }

    private void Initialize()
    {
        if (gridRect == null || imageRect == null || imageToCropImage == null)
        {
            Debug.LogError("CropGridResizer: Missing references.");
            return;
        }

        _rootCanvas = gridRect.GetComponentInParent<Canvas>();
        if (_rootCanvas != null) _rootCanvas = _rootCanvas.rootCanvas;

        // Compute visible image bounds (preserveAspect aware)
        ComputeVisibleImageBounds(out _visibleImageMin, out _visibleImageMax);
        float visW = _visibleImageMax.x - _visibleImageMin.x;
        float visH = _visibleImageMax.y - _visibleImageMin.y;

        _initialSize = Mathf.Min(visW, visH);
        if (_initialSize <= 0f)
        {
            Debug.LogWarning("CropGridResizer: Visible image size is zero. Using fallback.");
            _initialSize = Mathf.Min(imageRect.rect.width, imageRect.rect.height);
        }

        _minSize = _initialSize * minSizeFraction;
        _currentSize = _initialSize;

        // Set grid to initial size, centered on visible image
        SetGridSizeAndCenter(_initialSize);

        // Create corner handles (only once)
        if (_handles == null && canShowZoomHandle)
            CreateCornerHandles();

        // Attach drag-to-move on the grid itself (only once)
        if (gridRect.GetComponent<GridDragMover>() == null)
        {
            GridDragMover mover = gridRect.gameObject.AddComponent<GridDragMover>();
            mover.Initialize(this);
        }

        // Ensure the grid Image receives pointer events for dragging
        Image gridImage = gridRect.GetComponent<Image>();
        if (gridImage != null)
            gridImage.raycastTarget = true;

        UpdateHandlePositions();
        _initialized = true;
    }

    // ─────────────────────────────────────────────────────────────────
    // Size application
    // ─────────────────────────────────────────────────────────────────

    private void ApplySize(float newSize)
    {
        newSize = Mathf.Clamp(newSize, _minSize, _initialSize);
        _currentSize = newSize;

        // Resize around center
        gridRect.sizeDelta = new Vector2(_currentSize, _currentSize);

        // Ensure grid stays inside visible image
        ClampGridInsideImage();
        UpdateHandlePositions();
    }

    private void SetGridSizeAndCenter(float size)
    {
        _currentSize = size;
        gridRect.sizeDelta = new Vector2(size, size);

        // Center on visible image
        Vector2 visCenter = (_visibleImageMin + _visibleImageMax) * 0.5f;
        // Convert world center to gridRect's parent local space
        RectTransform parentRT = gridRect.parent as RectTransform;
        if (parentRT != null)
        {
            Vector2 localCenter;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT,
                WorldToScreen(visCenter),
                GetCanvasCamera(),
                out localCenter);
            gridRect.anchoredPosition = localCenter;
        }

        ClampGridInsideImage();
    }

    // ─────────────────────────────────────────────────────────────────
    // Grid drag-to-move
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="GridDragMover"/> when the user drags the grid body.
    /// Moves the grid while keeping it fully inside the visible image bounds.
    /// </summary>
    internal void OnGridDragged(PointerEventData eventData)
    {
        if (!_initialized) return;

        RectTransform parentRT = gridRect.parent as RectTransform;
        if (parentRT == null) return;

        Camera cam = GetCanvasCamera();

        // Convert current and previous pointer positions to parent-local space
        Vector2 localCurrent, localPrevious;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRT, eventData.position, cam, out localCurrent);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRT, eventData.position - eventData.delta, cam, out localPrevious);

        // Apply the local-space delta
        gridRect.anchoredPosition += localCurrent - localPrevious;

        ClampGridInsideImage();
        UpdateHandlePositions();
    }

    // ─────────────────────────────────────────────────────────────────
    // Corner drag handling
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by a <see cref="CornerDragHandle"/> when the user drags a corner.
    /// </summary>
    internal void OnCornerDragged(int cornerIndex, PointerEventData eventData)
    {
        if (!_initialized) return;

        // Convert pointer delta to world space
        Vector2 worldDelta = ScreenDeltaToWorld(eventData.delta);

        // Diagonal direction for each corner (Unity corner order: BL, TL, TR, BR)
        // BL: expanding = moving left-down = (-1,-1) → size decreases
        // TL: expanding = moving left-up = (-1,+1) → size increases by moving left
        // TR: expanding = moving right-up = (+1,+1) → size increases
        // BR: expanding = moving right-down = (+1,-1)
        Vector2[] diagonals =
        {
            new Vector2(-1f, -1f), // BL
            new Vector2(-1f, +1f), // TL
            new Vector2(+1f, +1f), // TR
            new Vector2(+1f, -1f), // BR
        };

        Vector2 diag = diagonals[cornerIndex].normalized;

        // Project world delta onto diagonal to get scalar size change
        float sizeDelta = Vector2.Dot(worldDelta, diag) * Mathf.Sqrt(2f);

        float newSize = Mathf.Clamp(_currentSize + sizeDelta, _minSize, _initialSize);
        float actualDelta = newSize - _currentSize;

        if (Mathf.Abs(actualDelta) < 0.01f) return;

        // Record opposite corner position before resize
        int oppositeIndex = (cornerIndex + 2) % 4;
        Vector3[] corners = new Vector3[4];
        gridRect.GetWorldCorners(corners);
        Vector2 oppositeCornerBefore = corners[oppositeIndex];

        // Apply new size
        _currentSize = newSize;
        gridRect.sizeDelta = new Vector2(_currentSize, _currentSize);

        // Compensate position so the opposite corner stays fixed
        gridRect.GetWorldCorners(corners);
        Vector2 oppositeCornerAfter = corners[oppositeIndex];
        Vector2 worldOffset = oppositeCornerBefore - oppositeCornerAfter;

        // Convert world offset to parent local offset
        RectTransform parentRT = gridRect.parent as RectTransform;
        if (parentRT != null)
        {
            Vector2 localBefore, localAfter;
            Camera cam = GetCanvasCamera();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT, WorldToScreen(oppositeCornerBefore), cam, out localBefore);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT, WorldToScreen(oppositeCornerAfter), cam, out localAfter);
            gridRect.anchoredPosition += localBefore - localAfter;
        }

        // Clamp inside image
        ClampGridInsideImage();
        UpdateHandlePositions();

        // Notify listeners (slider sync)
        _isUpdatingFromCode = true;
        OnNormalizedSizeChanged?.Invoke(NormalizedSize);
        _isUpdatingFromCode = false;
    }

    // ─────────────────────────────────────────────────────────────────
    // Containment
    // ─────────────────────────────────────────────────────────────────

    private void ClampGridInsideImage()
    {
        // Recompute visible bounds (image might have moved/scaled)
        ComputeVisibleImageBounds(out _visibleImageMin, out _visibleImageMax);

        Vector3[] gridCorners = new Vector3[4];
        gridRect.GetWorldCorners(gridCorners);
        Vector2 gridMin = gridCorners[0]; // BL
        Vector2 gridMax = gridCorners[2]; // TR
        float gridW = gridMax.x - gridMin.x;
        float gridH = gridMax.y - gridMin.y;

        // If grid is larger than visible image, shrink it
        float visW = _visibleImageMax.x - _visibleImageMin.x;
        float visH = _visibleImageMax.y - _visibleImageMin.y;
        float maxFit = Mathf.Min(visW, visH);
        if (_currentSize > maxFit && maxFit > _minSize)
        {
            _currentSize = maxFit;
            gridRect.sizeDelta = new Vector2(_currentSize, _currentSize);
            gridRect.GetWorldCorners(gridCorners);
            gridMin = gridCorners[0];
            gridMax = gridCorners[2];
            gridW = gridMax.x - gridMin.x;
            gridH = gridMax.y - gridMin.y;
        }

        // Compute offset needed to push grid inside image
        float offsetX = 0f, offsetY = 0f;

        if (gridMin.x < _visibleImageMin.x)
            offsetX = _visibleImageMin.x - gridMin.x;
        else if (gridMax.x > _visibleImageMax.x)
            offsetX = _visibleImageMax.x - gridMax.x;

        if (gridMin.y < _visibleImageMin.y)
            offsetY = _visibleImageMin.y - gridMin.y;
        else if (gridMax.y > _visibleImageMax.y)
            offsetY = _visibleImageMax.y - gridMax.y;

        if (Mathf.Abs(offsetX) > 0.01f || Mathf.Abs(offsetY) > 0.01f)
        {
            // Convert world offset to parent local
            RectTransform parentRT = gridRect.parent as RectTransform;
            if (parentRT != null)
            {
                Camera cam = GetCanvasCamera();
                Vector2 screenA = WorldToScreen(Vector2.zero);
                Vector2 screenB = WorldToScreen(new Vector2(offsetX, offsetY));

                Vector2 localA, localB;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRT, screenA, cam, out localA);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRT, screenB, cam, out localB);
                gridRect.anchoredPosition += localB - localA;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Visible image bounds (preserveAspect aware)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes the world-space bounds of the actual rendered image,
    /// accounting for <c>preserveAspect</c> on the Image component.
    /// </summary>
    public void ComputeVisibleImageBounds(out Vector2 visMin, out Vector2 visMax)
    {
        Vector3[] corners = new Vector3[4];
        imageRect.GetWorldCorners(corners);
        Vector2 rtMin = corners[0];
        Vector2 rtMax = corners[2];
        float rtW = rtMax.x - rtMin.x;
        float rtH = rtMax.y - rtMin.y;

        if (imageToCropImage == null || imageToCropImage.sprite == null ||
            !imageToCropImage.preserveAspect || rtW <= 0f || rtH <= 0f)
        {
            visMin = rtMin;
            visMax = rtMax;
            return;
        }

        Sprite sp = imageToCropImage.sprite;
        float spriteAspect = sp.rect.width / sp.rect.height;
        float rtAspect = rtW / rtH;
        float visW, visH;

        if (spriteAspect > rtAspect)
        {
            // Wider than container → width fills, height shrinks
            visW = rtW;
            visH = rtW / spriteAspect;
        }
        else
        {
            // Taller → height fills, width shrinks
            visH = rtH;
            visW = rtH * spriteAspect;
        }

        Vector2 center = (rtMin + rtMax) * 0.5f;
        visMin = center - new Vector2(visW, visH) * 0.5f;
        visMax = center + new Vector2(visW, visH) * 0.5f;
    }

    // ─────────────────────────────────────────────────────────────────
    // Corner handle creation
    // ─────────────────────────────────────────────────────────────────

    private void CreateCornerHandles()
    {
        _handles = new RectTransform[4];
        string[] names = { "Handle_BL", "Handle_TL", "Handle_TR", "Handle_BR" };

        RectTransform handleParent = gridRect.parent as RectTransform;
        if (handleParent == null) handleParent = gridRect;

        for (int i = 0; i < 4; i++)
        {
            GameObject go = new GameObject(names[i],
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

            go.transform.SetParent(handleParent, false);
            go.layer = gridRect.gameObject.layer;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(handleVisualSize, handleVisualSize);

            Image img = go.GetComponent<Image>();
            img.color = handleColor;
            img.raycastTarget = true;
            img.sprite = CreateCircleSprite(128);

            // Add drag handler
            CornerDragHandle dragHandle = go.AddComponent<CornerDragHandle>();
            dragHandle.Initialize(this, i);

            _handles[i] = rt;
        }
    }

    private void UpdateHandlePositions()
    {
        if (_handles == null || gridRect == null) return;

        Vector3[] gridCorners = new Vector3[4];
        gridRect.GetWorldCorners(gridCorners);

        RectTransform handleParent = _handles[0].parent as RectTransform;
        Camera cam = GetCanvasCamera();

        for (int i = 0; i < 4; i++)
        {
            if (_handles[i] == null) continue;

            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                handleParent,
                WorldToScreen(gridCorners[i]),
                cam,
                out localPos);

            _handles[i].anchoredPosition = localPos;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Coordinate helpers
    // ─────────────────────────────────────────────────────────────────

    private Camera GetCanvasCamera()
    {
        if (_rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            return _rootCanvas.worldCamera;
        return null;
    }

    private Vector2 WorldToScreen(Vector2 worldPos)
    {
        Camera cam = GetCanvasCamera();
        if (cam != null)
            return cam.WorldToScreenPoint(worldPos);
        // Screen Space Overlay: world pos IS screen pos for UI
        return worldPos;
    }

    private Vector2 ScreenDeltaToWorld(Vector2 screenDelta)
    {
        if (_rootCanvas == null) return screenDelta;

        // Account for canvas scaler
        float scaleFactor = _rootCanvas.scaleFactor;
        if (scaleFactor <= 0f) scaleFactor = 1f;
        return screenDelta / scaleFactor;
    }

    // ─────────────────────────────────────────────────────────────────
    // Circle sprite generator
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a white anti-aliased circle sprite at runtime.
    /// Used so corner handles render as circles without needing an asset.
    /// </summary>
    private static Sprite CreateCircleSprite(int size = 128)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.name       = "CircleHandle";

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float   radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist  = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float alpha = Mathf.Clamp01(radius - dist + 1f); // 1px anti-alias edge
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}

// ═════════════════════════════════════════════════════════════════════
// Corner drag handle — sits on each of the 4 corners of the crop grid
// ═════════════════════════════════════════════════════════════════════

/// <summary>
/// Small MonoBehaviour on each corner handle that forwards drag events
/// to the parent <see cref="CropGridResizer"/>.
/// </summary>
public class CornerDragHandle : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private CropGridResizer _resizer;
    private int _cornerIndex;

    public void Initialize(CropGridResizer resizer, int cornerIndex)
    {
        _resizer = resizer;
        _cornerIndex = cornerIndex;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Consume the event so drag starts
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_resizer != null)
            _resizer.OnCornerDragged(_cornerIndex, eventData);
    }
}

// ═════════════════════════════════════════════════════════════════════
// Grid body drag — left-click and drag to move the crop grid
// ═════════════════════════════════════════════════════════════════════

/// <summary>
/// Attached to the crop grid overlay. Allows the user to drag the
/// entire grid to reposition it within the visible image bounds.
/// Only responds to left mouse button / single-finger touch.
/// </summary>
public class GridDragMover : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private CropGridResizer _resizer;

    public void Initialize(CropGridResizer resizer)
    {
        _resizer = resizer;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Consume so drag begins — only on left click / first touch
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_resizer != null && eventData.button == PointerEventData.InputButton.Left)
            _resizer.OnGridDragged(eventData);
    }
}
