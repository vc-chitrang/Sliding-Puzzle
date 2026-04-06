using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Centralised screen navigation manager.
///
/// Provides a stack-based back-navigation system with DOTween fade transitions.
/// All screens call ShowScreen / GoBack through this singleton.
///
/// Usage:
///   ScreenManager.Instance.ShowScreen(myScreen);   // push + show
///   ScreenManager.Instance.GoBack();               // pop and show previous
///   ScreenManager.Instance.ReplaceScreen(myScreen); // swap without pushing
///   ScreenManager.Instance.ClearStack();            // wipe history (e.g. New Image)
/// </summary>
public class ScreenManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────
    // Singleton
    // ─────────────────────────────────────────────────────────────────

    public static ScreenManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────────────────────

    [Header("Startup")]
    [Tooltip("The first screen to activate when the game starts.\n" +
             "Assign Launch_Screen here. All other screens should start inactive in the scene.")]
    [SerializeField] private GameObject initialScreen;

    [Header("Transition")]
    [Tooltip("Duration of one fade (out or in). Total cross-fade = 2× this value.")]
    [SerializeField] private float fadeDuration = 0.2f;

    [Tooltip("(Optional) Assign a full-screen CanvasGroup as the black fade overlay.\n" +
             "Leave empty — one will be created automatically at runtime.")]
    [SerializeField] private CanvasGroup fadeOverlay;

    // ─────────────────────────────────────────────────────────────────
    // State
    // ─────────────────────────────────────────────────────────────────

    private readonly Stack<GameObject> _screenStack = new Stack<GameObject>();
    private GameObject _currentScreen;
    private bool _isTransitioning;

    // ─────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureFadeOverlay();
    }

    private void Start()
    {
        // Auto-show the designated starting screen.
        // All other screens should be inactive in the scene by default.
        if (initialScreen != null && _currentScreen == null)
            SetInitialScreen(initialScreen);
    }

    // ─────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Set the initial active screen without any transition or stack push.
    /// Call this once on startup for the first screen.
    /// </summary>
    public void SetInitialScreen(GameObject screen)
    {
        if (screen == null) return;
        screen.SetActive(true);
        _currentScreen = screen;
    }

    /// <summary>
    /// Show <paramref name="newScreen"/> with a fade transition.
    /// By default the current screen is pushed to the back-stack so GoBack() returns to it.
    /// Pass <c>pushCurrent = false</c> to replace without stacking.
    /// </summary>
    public void ShowScreen(GameObject newScreen, bool pushCurrent = true)
    {
        if (newScreen == null || _isTransitioning) return;
        if (newScreen == _currentScreen) return;

        _isTransitioning = true;

        GameObject outgoing = _currentScreen;

        if (pushCurrent && outgoing != null)
            _screenStack.Push(outgoing);

        PerformTransition(outgoing, newScreen, () =>
        {
            _currentScreen = newScreen;
            _isTransitioning = false;
        });
    }

    /// <summary>
    /// Navigate back to the previous screen (pops the stack).
    /// No-op if the stack is empty or a transition is in progress.
    /// </summary>
    public void GoBack()
    {
        if (_isTransitioning || _screenStack.Count == 0) return;

        _isTransitioning = true;

        GameObject outgoing = _currentScreen;
        GameObject incoming = _screenStack.Pop();

        PerformTransition(outgoing, incoming, () =>
        {
            _currentScreen = incoming;
            _isTransitioning = false;
        });
    }

    /// <summary>
    /// Replace the current screen without pushing it to the stack.
    /// Useful for hard-redirects (e.g. "New Image" → Launch).
    /// </summary>
    public void ReplaceScreen(GameObject newScreen)
    {
        ShowScreen(newScreen, pushCurrent: false);
    }

    /// <summary>Wipe the entire navigation history.</summary>
    public void ClearStack()
    {
        _screenStack.Clear();
    }

    /// <returns><c>true</c> if there is at least one screen to go back to.</returns>
    public bool CanGoBack => _screenStack.Count > 0;

    // ─────────────────────────────────────────────────────────────────
    // Transition
    // ─────────────────────────────────────────────────────────────────

    private void PerformTransition(GameObject outgoing, GameObject incoming, System.Action onComplete)
    {
        if (fadeOverlay == null)
        {
            // Instant swap (no overlay available)
            if (outgoing != null) outgoing.SetActive(false);
            incoming.SetActive(true);
            onComplete?.Invoke();
            return;
        }

        fadeOverlay.gameObject.SetActive(true);
        fadeOverlay.alpha = 0f;
        fadeOverlay.blocksRaycasts = true;

        DOTween.Sequence()
            .Append(fadeOverlay.DOFade(1f, fadeDuration).SetEase(Ease.OutQuad))
            .AppendCallback(() =>
            {
                if (outgoing != null) outgoing.SetActive(false);
                incoming.SetActive(true);
            })
            .Append(fadeOverlay.DOFade(0f, fadeDuration).SetEase(Ease.InQuad))
            .AppendCallback(() =>
            {
                fadeOverlay.blocksRaycasts = false;
                fadeOverlay.gameObject.SetActive(false);
                onComplete?.Invoke();
            });
    }

    // ─────────────────────────────────────────────────────────────────
    // Fade overlay auto-creation
    // ─────────────────────────────────────────────────────────────────

    private void EnsureFadeOverlay()
    {
        if (fadeOverlay != null) return;

        // Try to find a root Canvas to parent the overlay under
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null)
            rootCanvas = FindObjectOfType<Canvas>();

        if (rootCanvas == null)
        {
            Debug.LogWarning("[ScreenManager] No Canvas found — fade transitions disabled.");
            return;
        }

        GameObject overlayGO = new GameObject("FadeOverlay");
        overlayGO.transform.SetParent(rootCanvas.transform, false);

        // Stretch to fill the canvas
        RectTransform rt = overlayGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();

        // Black fill
        Image img = overlayGO.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = true;

        // CanvasGroup for alpha control
        fadeOverlay = overlayGO.AddComponent<CanvasGroup>();
        fadeOverlay.alpha = 0f;
        fadeOverlay.interactable = false;
        fadeOverlay.blocksRaycasts = false;

        overlayGO.SetActive(false);
    }
}
