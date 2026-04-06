using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Adds live-search filtering and a loading spinner to any TMP_Dropdown popup.
///
/// Attach to the same GameObject as a TMP_Dropdown (filter dropdowns only).
///
/// Behaviour
/// ─────────
/// 1. A transparent interceptor child covers the dropdown button. Because
///    Unity raycasts hit the frontmost element first, the interceptor receives
///    the click before TMP_Dropdown's IPointerClickHandler, letting us show a
///    spinner on the button for one rendered frame before the synchronous
///    popup-build freeze starts (= user sees click feedback).
///
/// 2. Every frame, Update() checks for a child named "Dropdown List". When the
///    popup appears, it finds the "SearchField/InputField" (added to the template
///    by CollectionUIManager.ConfigureDropdownTemplate) and wires its
///    onValueChanged to FilterItems().
///
/// 3. A semi-transparent "SpinnerOverlay" child of the popup (also added to the
///    template) is shown for 2 frames after open, covering the layout-rebuild
///    phase for large option lists.
/// </summary>
[RequireComponent(typeof(TMP_Dropdown))]
public class DropdownSearchController : MonoBehaviour
{
    // Set by CollectionUIManager after AddComponent
    public GameObject spinnerPrefab;

    private TMP_Dropdown    _dd;
    private GameObject      _popup;          // live "Dropdown List" instance
    private TMP_InputField  _searchInput;
    private GameObject      _btnSpinner;     // spinner shown on button before popup opens
    private bool            _opening;        // coroutine in flight

    // ─── Lifecycle ────────────────────────────────────────────────────────

    private void Awake()
    {
        _dd = GetComponent<TMP_Dropdown>();
        BuildInterceptor();
    }

    private void OnDestroy() => _dd = null;

    private void Update()
    {
        if (_dd == null) return;

        Transform popupT = transform.Find("Dropdown List");
        if (popupT != null && _popup == null)
        {
            _popup = popupT.gameObject;
            OnPopupOpened();
        }
        else if (popupT == null && _popup != null)
        {
            _popup       = null;
            _searchInput = null;
        }
    }

    // ─── Click interceptor ────────────────────────────────────────────────

    private void BuildInterceptor()
    {
        // Transparent child that covers the dropdown area. As the frontmost
        // element, it receives raycasts instead of TMP_Dropdown, giving us
        // control over when Show() is called.
        var go = new GameObject("_ClickInterceptor",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color         = Color.clear;
        img.raycastTarget = true;

        var et    = go.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        entry.callback.AddListener(_ => HandleClick());
        et.triggers.Add(entry);
    }

    private void HandleClick()
    {
        if (_dd == null) return;
        // If popup is already open, close it
        if (transform.Find("Dropdown List") != null) { _dd.Hide(); return; }
        if (_opening) return;

        _opening = true;
        SetBtnSpinner(true);
        StartCoroutine(OpenCo());
    }

    private IEnumerator OpenCo()
    {
        yield return null;       // 1 frame — button spinner renders
        if (_dd != null) _dd.Show();
        SetBtnSpinner(false);
        _opening = false;
    }

    // ─── Popup detection ──────────────────────────────────────────────────

    private void OnPopupOpened()
    {
        WireSearchField();
        SetPopupSpinner(true);
        StartCoroutine(HidePopupSpinnerCo());
    }

    private IEnumerator HidePopupSpinnerCo()
    {
        yield return null;
        yield return null;   // 2 frames — layout fully rebuilt
        SetPopupSpinner(false);
    }

    // ─── Search field ─────────────────────────────────────────────────────

    private void WireSearchField()
    {
        if (_popup == null) return;

        // Path matches what ConfigureDropdownTemplate builds:
        // Popup → SearchField → InputField
        var sfT = _popup.transform.Find("SearchField/InputField");
        if (sfT == null) return;

        _searchInput = sfT.GetComponent<TMP_InputField>();
        if (_searchInput == null) return;

        _searchInput.text = "";
        _searchInput.onValueChanged.RemoveAllListeners();
        _searchInput.onValueChanged.AddListener(FilterItems);
        FilterItems("");
        _searchInput.ActivateInputField();
    }

    private void FilterItems(string query)
    {
        if (_popup == null) return;
        var content = _popup.transform.Find("Viewport/Content");
        if (content == null) return;

        query = (query ?? "").Trim().ToLowerInvariant();
        bool hasQuery = query.Length > 0;

        for (int i = 0; i < content.childCount; i++)
        {
            var item = content.GetChild(i);

            // Child 0 = TMP_Dropdown's hidden item-template → always inactive
            if (i == 0) { item.gameObject.SetActive(false); continue; }

            // Child 1 = index-0 option = placeholder ("Department" / "All") → always visible
            if (i == 1 || !hasQuery) { item.gameObject.SetActive(true); continue; }

            // Remaining items: show if label contains query
            var labelT = item.Find("Item Label");
            var text   = "";
            if (labelT != null)
            {
                var tmp = labelT.GetComponent<TextMeshProUGUI>();
                if (tmp != null) text = tmp.text;
            }
            item.gameObject.SetActive(text.ToLowerInvariant().Contains(query));
        }
    }

    // ─── Spinner helpers ──────────────────────────────────────────────────

    private void SetBtnSpinner(bool show)
    {
        if (show && _btnSpinner == null && spinnerPrefab != null)
        {
            _btnSpinner = Instantiate(spinnerPrefab, transform, false);
            _btnSpinner.name = "_BtnSpinner";
            var rt = _btnSpinner.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin        = new Vector2(0.5f, 0.5f);
                rt.anchorMax        = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
            }
        }
        if (_btnSpinner != null) _btnSpinner.SetActive(show);
    }

    private void SetPopupSpinner(bool show)
    {
        if (_popup == null) return;
        var ov = _popup.transform.Find("SpinnerOverlay");
        if (ov != null) ov.gameObject.SetActive(show);
    }
}
