using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI component for a single artwork card in the collection grid.
/// Object-pooled via <see cref="Bind"/>/<see cref="Clear"/>.
///
/// Images loaded via UnityWebRequestTexture with static sprite cache.
/// Shows a loading spinner prefab while image downloads.
/// Hover: subtle scale-up + shadow on pointer enter.
/// </summary>
public class CardItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // ─────────────────────────────────────────────────────────────────
    // References
    // ─────────────────────────────────────────────────────────────────

    [SerializeField] private Image artworkImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI artistText;
    [SerializeField] private TextMeshProUGUI accessionText;
    [SerializeField] private Button cardButton;
    [SerializeField] private Image cardBackground;

    /// <summary>Exposed so the scroll-drag blocker can toggle interactability.</summary>
    public Button CardButton => cardButton;

    // ─────────────────────────────────────────────────────────────────
    // Static sprite cache + spinner prefab
    // ─────────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();
    private static GameObject _spinnerPrefab;

    /// <summary>Call once at startup to set the spinner prefab for all cards.</summary>
    public static void SetSpinnerPrefab(GameObject prefab) => _spinnerPrefab = prefab;

    // ─────────────────────────────────────────────────────────────────
    // State
    // ─────────────────────────────────────────────────────────────────

    private ResultsData _boundData;
    private Action<ResultsData> _onClickCallback;
    private Coroutine _loadCoroutine;
    private string _loadingUrl;
    private GameObject _spinnerInstance;

    // Hover
    private static readonly Vector3 HoverScale = new Vector3(1.03f, 1.03f, 1f);
    private Shadow _shadow;

    // ─────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────

    public void Bind(ResultsData data, MediaManager mediaManager, Action<ResultsData> onCardClicked)
    {
        _boundData = data;
        _onClickCallback = onCardClicked;

        // ── ACTIVATE FIRST so coroutines can run ────────────────────
        gameObject.SetActive(true);

        // Title
        if (titleText != null)
            titleText.text = !string.IsNullOrEmpty(data.title) ? data.title : "Untitled";

        // Artist
        if (artistText != null)
        {
            string artistName = "Unknown";
            if (data.artists != null && data.artists.Count > 0 && !string.IsNullOrEmpty(data.artists[0].name))
                artistName = data.artists[0].name;
            artistText.text = artistName;
        }

        // Accession number
        if (accessionText != null)
            accessionText.text = !string.IsNullOrEmpty(data.accession_number) ? data.accession_number : "";

        // Image — reset to placeholder
        if (artworkImage != null)
        {
            artworkImage.sprite = null;
            artworkImage.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        }

        // Cancel any in-flight load
        if (_loadCoroutine != null)
        {
            StopCoroutine(_loadCoroutine);
            _loadCoroutine = null;
        }

        // Hide old spinner
        HideSpinner();

        // Load image
        if (!string.IsNullOrEmpty(data.primary_image))
        {
            _loadingUrl = data.primary_image;

            if (SpriteCache.TryGetValue(data.primary_image, out Sprite cached) && cached != null)
            {
                ApplySprite(cached);
            }
            else
            {
                ShowSpinner();
                _loadCoroutine = StartCoroutine(LoadImageCoroutine(data.primary_image));
            }
        }

        // Button
        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(OnCardClicked);
        }

        // Reset hover
        transform.localScale = Vector3.one;
    }

    public void Clear()
    {
        if (_loadCoroutine != null)
        {
            StopCoroutine(_loadCoroutine);
            _loadCoroutine = null;
        }

        _boundData = null;
        _onClickCallback = null;
        _loadingUrl = null;

        if (titleText != null) titleText.text = "";
        if (artistText != null) artistText.text = "";
        if (accessionText != null) accessionText.text = "";
        if (artworkImage != null)
        {
            artworkImage.sprite = null;
            artworkImage.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        }
        if (cardButton != null) cardButton.onClick.RemoveAllListeners();

        HideSpinner();
        transform.localScale = Vector3.one;
        gameObject.SetActive(false);
    }

    public void AssignReferences(Image artwork, TextMeshProUGUI title, TextMeshProUGUI artist,
        TextMeshProUGUI accession, Button button, Image background)
    {
        artworkImage = artwork;
        titleText = title;
        artistText = artist;
        accessionText = accession;
        cardButton = button;
        cardBackground = background;

        _shadow = gameObject.GetComponent<Shadow>();
        if (_shadow == null)
        {
            _shadow = gameObject.AddComponent<Shadow>();
            _shadow.effectColor = new Color(0f, 0f, 0f, 0.15f);
            _shadow.effectDistance = new Vector2(4f, -4f);
            _shadow.enabled = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Spinner (loading indicator on image area)
    // ─────────────────────────────────────────────────────────────────

    private void ShowSpinner()
    {
        if (_spinnerPrefab == null || artworkImage == null) return;

        if (_spinnerInstance == null)
        {
            _spinnerInstance = Instantiate(_spinnerPrefab, artworkImage.transform);
            RectTransform rt = _spinnerInstance.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(80f, 80f);
            }
        }

        _spinnerInstance.SetActive(true);
    }

    private void HideSpinner()
    {
        if (_spinnerInstance != null)
            _spinnerInstance.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────
    // Image loading
    // ─────────────────────────────────────────────────────────────────

    private IEnumerator LoadImageCoroutine(string url)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            // Guard: card may have been recycled
            if (_loadingUrl != url || !gameObject.activeInHierarchy)
            {
                _loadCoroutine = null;
                yield break;
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(request);
                if (tex != null)
                {
                    Sprite sprite = Sprite.Create(tex,
                        new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f));
                    SpriteCache[url] = sprite;
                    ApplySprite(sprite);
                }
            }
            else
            {
                Debug.LogWarning($"[CardItemUI] Failed: {url} — {request.error}");
            }

            HideSpinner();
        }

        _loadCoroutine = null;
    }

    private void ApplySprite(Sprite sprite)
    {
        if (artworkImage == null) return;
        artworkImage.sprite = sprite;
        artworkImage.color = Color.white;
        artworkImage.preserveAspect = true;
        HideSpinner();
    }

    // ─────────────────────────────────────────────────────────────────
    // Hover
    // ─────────────────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_boundData == null) return;
        transform.localScale = HoverScale;
        if (_shadow != null) _shadow.enabled = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = Vector3.one;
        if (_shadow != null) _shadow.enabled = false;
    }

    // ─────────────────────────────────────────────────────────────────
    // Click
    // ─────────────────────────────────────────────────────────────────

    private void OnCardClicked()
    {
        if (_boundData != null)
            _onClickCallback?.Invoke(_boundData);
    }
}
