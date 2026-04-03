using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A ScrollRect subclass that supports pinch-to-zoom (touch) and scroll-wheel zoom (PC),
/// with zoom centered on the pinch midpoint or mouse cursor.
///
/// Exposes <see cref="OnZoomChanged"/> so external UI (e.g. a Slider) can stay in sync.
/// Call <see cref="SetNormalizedZoom"/> to drive zoom from external code.
/// </summary>
public class PinchableScrollRect : ScrollRect
{
    // ── Inspector-tunable fields ──────────────────────────────────────────
    [SerializeField] protected float _minZoom = 1f;
    [SerializeField] protected float _maxZoom = 10f;
    [SerializeField] private float _zoomLerpSpeed = 8f;
    [SerializeField] private float _mouseWheelSensitivity = 1f;

    // ── Runtime state ─────────────────────────────────────────────────────
    public float _currentZoom = 1f;

    private bool _isPinching;
    private float _startPinchDist;
    private float _startPinchZoom = 1f;
    private Vector2 _startPinchCenterPosition = Vector2.zero;
    private Vector2 _startPinchScreenPosition = Vector2.zero;
    private bool _blockPan;

    // ── Zoom sync event ───────────────────────────────────────────────────
    /// <summary>
    /// Fired whenever the zoom level changes.  The parameter is the
    /// normalized zoom (0 = minZoom, 1 = maxZoom).
    /// </summary>
    public event Action<float> OnZoomChanged;

    /// <summary>Current zoom as a 0..1 normalized value.</summary>
    public float NormalizedZoom => Mathf.InverseLerp(_minZoom, _maxZoom, _currentZoom);

    // Flag to prevent infinite loops when sync is driven from code
    private bool _isUpdatingFromCode;

    // ─────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        Input.multiTouchEnabled = true;
    }

    protected virtual void Update()
    {
        HandleTouchInput();
        HandleMouseWheelInput();
        SmoothApplyZoom();
    }

    protected override void SetContentAnchoredPosition(Vector2 position)
    {
        if (_isPinching || _blockPan)
            return;
        base.SetContentAnchoredPosition(position);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Public API for external sync (e.g. UI Slider)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Set zoom from an external source (0 = minZoom, 1 = maxZoom).
    /// Skips raising <see cref="OnZoomChanged"/> to avoid loops.
    /// </summary>
    public void SetNormalizedZoom(float normalized01)
    {
        if (_isUpdatingFromCode) return;

        _isUpdatingFromCode = true;
        _currentZoom = Mathf.Lerp(_minZoom, _maxZoom, Mathf.Clamp01(normalized01));
        _isUpdatingFromCode = false;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Input handlers
    // ─────────────────────────────────────────────────────────────────────

    private void HandleTouchInput()
    {
        if (Input.touchCount == 2)
        {
            if (!_isPinching)
            {
                _isPinching = true;
                OnPinchStart();
            }
            OnPinch();
        }
        else
        {
            if (_isPinching)
            {
                _isPinching = false;
            }
            if (Input.touchCount <= 1)
            {
                _blockPan = false;
            }
        }
    }

    private void HandleMouseWheelInput()
    {
        float scrollDelta = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scrollDelta) <= float.Epsilon)
            return;

        _currentZoom *= 1f + scrollDelta * _mouseWheelSensitivity;
        _currentZoom = Mathf.Clamp(_currentZoom, _minZoom, _maxZoom);

        _startPinchScreenPosition = Input.mousePosition;
        SetZoomPivotFromScreenPoint(_startPinchScreenPosition);

        NotifyZoomChanged();
    }

    private void SmoothApplyZoom()
    {
        float targetScale = _currentZoom;
        if (Mathf.Abs(content.localScale.x - targetScale) > 0.001f)
        {
            content.localScale = Vector3.Lerp(
                content.localScale,
                Vector3.one * targetScale,
                _zoomLerpSpeed * Time.deltaTime
            );
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Pinch helpers
    // ─────────────────────────────────────────────────────────────────────

    private void OnPinchStart()
    {
        Vector2 pos1 = Input.touches[0].position;
        Vector2 pos2 = Input.touches[1].position;

        _startPinchScreenPosition = (pos1 + pos2) * 0.5f;
        _startPinchDist = PinchDistanceInContentSpace(pos1, pos2);
        _startPinchZoom = _currentZoom;

        SetZoomPivotFromScreenPoint(_startPinchScreenPosition);
        _blockPan = true;
    }

    private void OnPinch()
    {
        Vector2 pos1 = Input.touches[0].position;
        Vector2 pos2 = Input.touches[1].position;

        float currentPinchDist = PinchDistanceInContentSpace(pos1, pos2);

        if (_startPinchDist < float.Epsilon)
            return;

        _currentZoom = (currentPinchDist / _startPinchDist) * _startPinchZoom;
        _currentZoom = Mathf.Clamp(_currentZoom, _minZoom, _maxZoom);

        NotifyZoomChanged();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Zoom-changed notification (with loop guard)
    // ─────────────────────────────────────────────────────────────────────

    private void NotifyZoomChanged()
    {
        if (_isUpdatingFromCode) return;

        _isUpdatingFromCode = true;
        OnZoomChanged?.Invoke(NormalizedZoom);
        _isUpdatingFromCode = false;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Shared utility
    // ─────────────────────────────────────────────────────────────────────

    private void SetZoomPivotFromScreenPoint(Vector2 screenPoint)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            content, screenPoint, null, out _startPinchCenterPosition);

        Vector2 pivotOffset = new Vector2(content.pivot.x * content.rect.width,
                                          content.pivot.y * content.rect.height);
        Vector2 posFromBottomLeft = pivotOffset + _startPinchCenterPosition;

        Vector2 newPivot = new Vector2(
            posFromBottomLeft.x / content.rect.width,
            posFromBottomLeft.y / content.rect.height
        );

        SetPivot(content, newPivot);
    }

    private float PinchDistanceInContentSpace(Vector2 screenPos1, Vector2 screenPos2)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screenPos1, null, out Vector2 local1);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screenPos2, null, out Vector2 local2);
        return Vector2.Distance(local1, local2);
    }

    private static void SetPivot(RectTransform rectTransform, Vector2 pivot)
    {
        if (rectTransform == null)
            return;

        Vector2 size = rectTransform.rect.size;
        Vector2 deltaPivot = rectTransform.pivot - pivot;

        Vector3 deltaPosition = new Vector3(
            deltaPivot.x * size.x,
            deltaPivot.y * size.y,
            0f
        ) * rectTransform.localScale.x;

        rectTransform.pivot = pivot;
        rectTransform.localPosition -= deltaPosition;
    }
}
