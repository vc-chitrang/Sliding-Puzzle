using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using ViitorCloud.API;
using ViitorCloud.Utility.PopupManager;

/// <summary>
/// Collection browsing screen controller.
/// Every filter / search / sort / page change builds a new API URL
/// and fetches data from the server. No client-side filtering.
/// </summary>
public class CollectionUIManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────
    // Inspector — UI References
    // ─────────────────────────────────────────────────────────────────

    [Header("Data")]
    [SerializeField] private MediaManager mediaManager;

    [Header("Search")]
    [SerializeField] private TMP_InputField searchInputField;
    [SerializeField] private Button searchButton;
    [SerializeField] private Button clearSearchButton;

    [Header("Filters")]
    [SerializeField] private TMP_Dropdown departmentDropdown;
    [SerializeField] private TMP_Dropdown classificationDropdown;
    [SerializeField] private TMP_Dropdown artistDropdown;
    [SerializeField] private TMP_Dropdown cultureDropdown;
    [SerializeField] private TMP_Dropdown dateDropdown;
    [SerializeField] private Button clearFiltersButton;

    [Header("Result Info")]
    [SerializeField] private TextMeshProUGUI resultCountText;
    [SerializeField] private TMP_Dropdown perPageDropdown;
    [SerializeField] private TMP_Dropdown sortByDropdown;

    [Header("Card Grid")]
    [SerializeField] private ScrollRect cardScrollView;
    [SerializeField] private RectTransform cardGridContent;
    [Tooltip("MasonryLayoutGroup on the same GameObject as cardGridContent.")]
    [SerializeField] private MasonryLayoutGroup masonryLayout;

    [Header("Pagination")]
    [SerializeField] private Button prevPageButton;
    [SerializeField] private Button nextPageButton;
    [SerializeField] private RectTransform pageNumbersContainer;
    [SerializeField] private TextMeshProUGUI pageInfoText;

    [Header("Screen Flow")]
    [SerializeField] private GameObject collectionScreen;
    [SerializeField] private GameObject cropImageScreen;
    [SerializeField] private Image imageToCropTarget;
    [SerializeField] private CropScreenController cropScreenController;
    [SerializeField] private Button backButton;

    [Header("Pool Settings")]
    [SerializeField] private int maxPoolSize = 80;

    [Header("Card Loading")]
    [Tooltip("Spinner prefab shown on each card while its image loads.")]
    [SerializeField] private GameObject spinnerPrefab;
    [SerializeField] private TMP_FontAsset TMP_FontAsset;
    [SerializeField] private Sprite selectedPageSprite;
    // ─────────────────────────────────────────────────────────────────
    // State
    // ─────────────────────────────────────────────────────────────────

    private readonly List<CardItemUI> _cardPool = new List<CardItemUI>();
    private bool _filtersPopulated;
    private bool _isFetching;

    // Current query state
    private int _currentPage = 1;
    private int _itemsPerPage = 20;
    private string _searchQuery = "";
    private SortMode _sortMode = SortMode.Default;

    // Filter IDs/values (0 or empty = no filter)
    private int _selectedDeptId;
    private int _selectedClassId;
    private int _selectedArtistId;
    private string _selectedCulture = "";
    private string _selectedDate = "";

    // Filter option lists (index 0 = "All" placeholder)
    private List<int> _deptIds = new List<int>();
    private List<int> _classIds = new List<int>();
    private List<int> _artistIds = new List<int>();
    private List<string> _cultureValues = new List<string>();
    private List<string> _dateValues = new List<string>();

    // Pagination from API
    private int _totalResults;
    private int _lastPage = 1;

    // Drag-vs-click: disable card buttons while scrolling
    private bool _isDragging;

    // Individual clear (×) buttons — one per filter dropdown
    private Button _deptClearBtn;
    private Button _classClearBtn;
    private Button _artistClearBtn;
    private Button _cultureClearBtn;
    private Button _dateClearBtn;

    private enum SortMode { Default, TitleAZ, TitleZA, DateAsc, DateDesc, ArtistAZ, ArtistZA }

    // Sort field/order mapping — index matches SortMode enum value
    private static readonly string[] SortFields = { "", "title", "title", "date", "date", "artist", "artist" };
    private static readonly string[] SortOrders = { "", "ASC", "DESC", "ASC", "DESC", "ASC",    "DESC"   };

    // ─────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        // Listen for initial data from APIHandler
        APIHandler.OnAPIDataFetchedEvent.AddListener(OnInitialDataReceived);

        // Search
        if (searchButton != null) searchButton.onClick.AddListener(OnSearchSubmit);
        if (clearSearchButton != null) clearSearchButton.onClick.AddListener(OnClearSearch);
        if (searchInputField != null) searchInputField.onSubmit.AddListener(_ => OnSearchSubmit());

        // Filters
        if (departmentDropdown != null) departmentDropdown.onValueChanged.AddListener(OnDepartmentChanged);
        if (classificationDropdown != null) classificationDropdown.onValueChanged.AddListener(OnClassificationChanged);
        if (artistDropdown != null) artistDropdown.onValueChanged.AddListener(OnArtistChanged);
        if (cultureDropdown != null) cultureDropdown.onValueChanged.AddListener(OnCultureChanged);
        if (dateDropdown != null) dateDropdown.onValueChanged.AddListener(OnDateChanged);
        if (clearFiltersButton != null) clearFiltersButton.onClick.AddListener(OnClearFilters);

        // Per-page
        if (perPageDropdown != null)
        {
            perPageDropdown.ClearOptions();
            perPageDropdown.AddOptions(new List<string> { "20", "40", "80" });
            perPageDropdown.SetValueWithoutNotify(0);  // always start at "20"
            perPageDropdown.RefreshShownValue();
            perPageDropdown.onValueChanged.AddListener(OnPerPageChanged);
            ConfigureDropdownTemplate(perPageDropdown, 172f); // 3 items × 52 + 16 = 172
        }

        // Sort
        if (sortByDropdown != null)
        {
            sortByDropdown.ClearOptions();
            sortByDropdown.AddOptions(new List<string>
            {
                "Default",
                "Title A-Z", "Title Z-A",
                "Date Ascending", "Date Descending",
                "Artist A-Z", "Artist Z-A"
            });
            sortByDropdown.SetValueWithoutNotify(0);  // always start at "Default"
            sortByDropdown.RefreshShownValue();
            sortByDropdown.onValueChanged.AddListener(OnSortChanged);
            ConfigureDropdownTemplate(sortByDropdown, 300f); // 7 items × ~44px ≈ 308px
        }

        // Pagination
        if (prevPageButton != null) prevPageButton.onClick.AddListener(PreviousPage);
        if (nextPageButton != null) nextPageButton.onClick.AddListener(NextPage);

        // Back navigation
        if (backButton != null) backButton.onClick.AddListener(OnBackPressed);

        // Drag detection — disable card buttons while scrolling to prevent accidental clicks
        WireScrollDragEvents();

        UpdateClearSearchVisibility();

        // Browse_And_Discover_Screen may start inactive (Launch_Screen is the entry point).
        // If the APIHandler already fetched data before this screen was activated,
        // the event won't fire again — trigger our own fetch using the cached response.
        if (!_filtersPopulated && APIHandler.LastFetchedData != null)
            FetchCollection();

        // Load spinner prefab for card loading indicators
        LoadSpinnerPrefab();
    }

    private void LoadSpinnerPrefab()
    {
        if (spinnerPrefab != null)
            CardItemUI.SetSpinnerPrefab(spinnerPrefab);
        else
            Debug.LogWarning("[CollectionUI] Spinner prefab not assigned.");
    }

    private void OnDisable()
    {
        APIHandler.OnAPIDataFetchedEvent.RemoveListener(OnInitialDataReceived);

        if (searchButton != null) searchButton.onClick.RemoveAllListeners();
        if (clearSearchButton != null) clearSearchButton.onClick.RemoveAllListeners();
        if (departmentDropdown != null) departmentDropdown.onValueChanged.RemoveAllListeners();
        if (classificationDropdown != null) classificationDropdown.onValueChanged.RemoveAllListeners();
        if (artistDropdown != null) artistDropdown.onValueChanged.RemoveAllListeners();
        if (cultureDropdown != null) cultureDropdown.onValueChanged.RemoveAllListeners();
        if (dateDropdown != null) dateDropdown.onValueChanged.RemoveAllListeners();
        if (clearFiltersButton != null) clearFiltersButton.onClick.RemoveAllListeners();
        if (perPageDropdown != null) perPageDropdown.onValueChanged.RemoveAllListeners();
        if (sortByDropdown != null) sortByDropdown.onValueChanged.RemoveAllListeners();
        if (prevPageButton != null) prevPageButton.onClick.RemoveAllListeners();
        if (nextPageButton != null) nextPageButton.onClick.RemoveAllListeners();
        if (backButton     != null) backButton.onClick.RemoveAllListeners();
    }

    // ─────────────────────────────────────────────────────────────────
    // Initial data
    // ─────────────────────────────────────────────────────────────────

    private void OnInitialDataReceived(MAPData data)
    {
        // APIHandler fires this event with its own default limit (e.g. 35 items).
        // We don't use that data directly — instead treat the event as an
        // "auth-ready / server reachable" signal and do our own controlled
        // fetch with limit=20 & page=1 so the first load is always consistent
        // with every subsequent page/filter change.
        FetchCollection();
    }

    // ─────────────────────────────────────────────────────────────────
    // API call — build URL & fetch
    // ─────────────────────────────────────────────────────────────────

    private string BuildCollectionURL()
    {
        string url = API.APICollectionBase;

        // Limit
        url += $"&limit={_itemsPerPage}";

        // Page
        url += $"&page={_currentPage}";

        // Search
        if (!string.IsNullOrEmpty(_searchQuery))
            url += $"&q={Uri.EscapeDataString(_searchQuery)}";

        // Filters
        if (_selectedDeptId > 0) url += $"&department={_selectedDeptId}";
        if (_selectedClassId > 0) url += $"&classification={_selectedClassId}";
        if (_selectedArtistId > 0) url += $"&artist={_selectedArtistId}";
        if (!string.IsNullOrEmpty(_selectedCulture)) url += $"&culture={Uri.EscapeDataString(_selectedCulture)}";
        if (!string.IsNullOrEmpty(_selectedDate)) url += $"&date={Uri.EscapeDataString(_selectedDate)}";

        // Sort
        int si = (int)_sortMode;
        if (si > 0 && si < SortFields.Length && !string.IsNullOrEmpty(SortFields[si]))
        {
            url += $"&sort_by={SortFields[si]}&sort_order={SortOrders[si]}";
        }

        return url;
    }

    private void FetchCollection()
    {
        if (_isFetching) return;
        _isFetching = true;

        string url = BuildCollectionURL();
        Debug.Log($"[CollectionUI] Fetching: {url}");

        PopupManager.Instance.ShowLoading();
        PopupManager.Instance.SetProgressText("Loading collection...");

        ServerCommunication.Instance.SendRequestGet<MAPData>(url, OnFetchSuccess, OnFetchFailed);
    }

    private void OnFetchSuccess(MAPData data)
    {
        _isFetching = false;
        PopupManager.Instance.HideLoading();

        if (data == null || data.results == null)
        {
            Debug.LogError("[CollectionUI] Received null data from API.");
            return;
        }

        // First load → configure dropdown templates + fill initial options.
        // Every subsequent load → refresh options to match the server-side cascade:
        // the API returns ONLY options valid for the currently active filters.
        if (data.filters != null)
        {
            if (!_filtersPopulated)
            {
                PopulateFilterDropdowns(data.filters);  // sets up templates + initial options
                _filtersPopulated = true;
            }
            else
            {
                UpdateFilterDropdowns(data.filters);    // updates options + restores/resets selections
            }
        }

        // Pagination info from API
        if (data.results.pagination != null)
        {
            _totalResults = data.results.pagination.total;
            _lastPage     = data.results.pagination.last_page;
            _currentPage  = data.results.pagination.current_page;
        }

        EnsureCardPool();
        DisplayCards(data.results.data);
        UpdateResultCount(data.results.pagination);
        UpdatePaginationUI();
    }

    private void OnFetchFailed(string error)
    {
        _isFetching = false;
        PopupManager.Instance.HideLoading();
        PopupManager.Instance.ShowToast($"Failed to load: {error}");
        Debug.LogError($"[CollectionUI] API error: {error}");
    }

    // ─────────────────────────────────────────────────────────────────
    // Filter dropdown population
    // ─────────────────────────────────────────────────────────────────

    private void PopulateFilterDropdowns(Filters filters)
    {
        // Department (ID-based)
        _deptIds = new List<int> { 0 };
        var deptLabels = new List<string> { "Department" };
        if (filters.department != null)
        {
            foreach (var d in filters.department)
            {
                if (!string.IsNullOrEmpty(d.dept))
                {
                    _deptIds.Add(d.id);
                    deptLabels.Add(d.dept);
                }
            }
        }
        SetDropdown(departmentDropdown, deptLabels, addSearch: true);

        // Classification (ID-based)
        _classIds = new List<int> { 0 };
        var classLabels = new List<string> { "Classification" };
        if (filters.classification != null)
        {
            foreach (var c in filters.classification)
            {
                if (!string.IsNullOrEmpty(c.@class))
                {
                    _classIds.Add(c.id);
                    classLabels.Add(c.@class);
                }
            }
        }
        SetDropdown(classificationDropdown, classLabels, addSearch: true);

        // Artist (ID-based)
        _artistIds = new List<int> { 0 };
        var artistLabels = new List<string> { "Artist/Maker" };
        if (filters.artist != null)
        {
            foreach (var a in filters.artist)
            {
                if (!string.IsNullOrEmpty(a.name))
                {
                    _artistIds.Add(a.id);
                    artistLabels.Add(a.name);
                }
            }
        }
        SetDropdown(artistDropdown, artistLabels, addSearch: true);

        // Culture (name-based)
        _cultureValues = new List<string> { "" };
        var cultureLabels = new List<string> { "Place of origin" };
        if (filters.culture != null)
        {
            foreach (var c in filters.culture)
            {
                if (!string.IsNullOrEmpty(c.culture))
                {
                    _cultureValues.Add(c.culture);
                    cultureLabels.Add(c.culture);
                }
            }
        }
        SetDropdown(cultureDropdown, cultureLabels, addSearch: true);

        // Date (name-based)
        _dateValues = new List<string> { "" };
        var dateLabels = new List<string> { "Date" };
        if (filters.date != null)
        {
            foreach (var d in filters.date)
            {
                if (!string.IsNullOrEmpty(d.date))
                {
                    _dateValues.Add(d.date);
                    dateLabels.Add(d.date);
                }
            }
        }
        SetDropdown(dateDropdown, dateLabels, addSearch: true);

        // ── Add search controller + clear buttons (first load only) ────────
        AddDropdownSearch(departmentDropdown);
        AddDropdownSearch(classificationDropdown);
        AddDropdownSearch(artistDropdown);
        AddDropdownSearch(cultureDropdown);
        AddDropdownSearch(dateDropdown);

        _deptClearBtn    = CreateClearButton(departmentDropdown);
        _classClearBtn   = CreateClearButton(classificationDropdown);
        _artistClearBtn  = CreateClearButton(artistDropdown);
        _cultureClearBtn = CreateClearButton(cultureDropdown);
        _dateClearBtn    = CreateClearButton(dateDropdown);

        // Wire clear button clicks — each resets its dropdown to index 0
        // and fires the same handler as a user selecting index 0
        if (_deptClearBtn    != null) _deptClearBtn.onClick.AddListener(()    => { departmentDropdown.SetValueWithoutNotify(0);      OnDepartmentChanged(0); });
        if (_classClearBtn   != null) _classClearBtn.onClick.AddListener(()   => { classificationDropdown.SetValueWithoutNotify(0);  OnClassificationChanged(0); });
        if (_artistClearBtn  != null) _artistClearBtn.onClick.AddListener(()  => { artistDropdown.SetValueWithoutNotify(0);          OnArtistChanged(0); });
        if (_cultureClearBtn != null) _cultureClearBtn.onClick.AddListener(() => { cultureDropdown.SetValueWithoutNotify(0);         OnCultureChanged(0); });
        if (_dateClearBtn    != null) _dateClearBtn.onClick.AddListener(()    => { dateDropdown.SetValueWithoutNotify(0);            OnDateChanged(0); });
    }

    /// <summary>Attaches DropdownSearchController to a filter dropdown if not already present.</summary>
    private void AddDropdownSearch(TMP_Dropdown dd)
    {
        if (dd == null) return;
        var ctrl = dd.GetComponent<DropdownSearchController>();
        if (ctrl == null) ctrl = dd.gameObject.AddComponent<DropdownSearchController>();
        ctrl.spinnerPrefab = spinnerPrefab;
    }

    /// <summary>
    /// Creates a small × button as a child of the dropdown, anchored to its
    /// right edge. The button is hidden by default; it appears when the dropdown
    /// has an active selection (value > 0) and clears it when clicked.
    /// The button is rendered on top of the DropdownSearchController's interceptor
    /// because it is added later (higher sibling index = drawn on top).
    /// </summary>
    private static Button CreateClearButton(TMP_Dropdown dd)
    {
        if (dd == null) return null;

        var go = new GameObject("ClearBtn",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(dd.transform, false);

        // Anchor to the right-center of the dropdown, inset from the edge
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 0.5f);
        rt.anchorMax        = new Vector2(1f, 0.5f);
        rt.pivot            = new Vector2(1f, 0.5f);
        rt.sizeDelta        = new Vector2(32f, 32f);
        rt.anchoredPosition = new Vector2(-6f, 0f);

        var img = go.GetComponent<Image>();
        img.color         = new Color(0.7f, 0.15f, 0.15f, 0.9f);
        img.raycastTarget = true;

        // × label
        var lGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
        lGO.transform.SetParent(go.transform, false);
        var lRT = lGO.GetComponent<RectTransform>();
        lRT.anchorMin = Vector2.zero;
        lRT.anchorMax = Vector2.one;
        lRT.offsetMin = lRT.offsetMax = Vector2.zero;
        var lTMP = lGO.AddComponent<TextMeshProUGUI>();
        lTMP.text           = "×";
        lTMP.fontSize       = 20f;
        lTMP.color          = Color.white;
        lTMP.alignment      = TextAlignmentOptions.Center;
        lTMP.raycastTarget  = false;

        go.SetActive(false);   // hidden until a value is selected
        return go.GetComponent<Button>();
    }

    /// <summary>
    /// Called on every API response after the first.
    /// Rebuilds all five filter option lists from the server response, which
    /// already contains ONLY options valid for the current active filter combination
    /// (server-side cascade). Restores any still-valid active selections via
    /// SetValueWithoutNotify so no new API call is triggered. Resets any selection
    /// whose value is no longer present in the narrowed option set.
    /// </summary>
    private void UpdateFilterDropdowns(Filters filters)
    {
        // ── Department ──────────────────────────────────────────────
        _deptIds = new List<int> { 0 };
        var deptLabels = new List<string> { "Department" };
        if (filters.department != null)
            foreach (var d in filters.department)
                if (!string.IsNullOrEmpty(d.dept)) { _deptIds.Add(d.id); deptLabels.Add(d.dept); }
        RefreshDropdownOptions(departmentDropdown, deptLabels);
        int deptIdx = _deptIds.IndexOf(_selectedDeptId);
        if (deptIdx < 0) { _selectedDeptId = 0; deptIdx = 0; }
        if (departmentDropdown != null) { departmentDropdown.SetValueWithoutNotify(deptIdx); departmentDropdown.RefreshShownValue(); }

        // ── Classification ──────────────────────────────────────────
        _classIds = new List<int> { 0 };
        var classLabels = new List<string> { "Classification" };
        if (filters.classification != null)
            foreach (var c in filters.classification)
                if (!string.IsNullOrEmpty(c.@class)) { _classIds.Add(c.id); classLabels.Add(c.@class); }
        RefreshDropdownOptions(classificationDropdown, classLabels);
        int classIdx = _classIds.IndexOf(_selectedClassId);
        if (classIdx < 0) { _selectedClassId = 0; classIdx = 0; }
        if (classificationDropdown != null) { classificationDropdown.SetValueWithoutNotify(classIdx); classificationDropdown.RefreshShownValue(); }

        // ── Artist ──────────────────────────────────────────────────
        _artistIds = new List<int> { 0 };
        var artistLabels = new List<string> { "Artist/Maker" };
        if (filters.artist != null)
            foreach (var a in filters.artist)
                if (!string.IsNullOrEmpty(a.name)) { _artistIds.Add(a.id); artistLabels.Add(a.name); }
        RefreshDropdownOptions(artistDropdown, artistLabels);
        int artistIdx = _artistIds.IndexOf(_selectedArtistId);
        if (artistIdx < 0) { _selectedArtistId = 0; artistIdx = 0; }
        if (artistDropdown != null) { artistDropdown.SetValueWithoutNotify(artistIdx); artistDropdown.RefreshShownValue(); }

        // ── Culture ─────────────────────────────────────────────────
        _cultureValues = new List<string> { "" };
        var cultureLabels = new List<string> { "Place of origin" };
        if (filters.culture != null)
            foreach (var c in filters.culture)
                if (!string.IsNullOrEmpty(c.culture)) { _cultureValues.Add(c.culture); cultureLabels.Add(c.culture); }
        RefreshDropdownOptions(cultureDropdown, cultureLabels);
        int cultureIdx = _cultureValues.IndexOf(_selectedCulture);
        if (cultureIdx < 0) { _selectedCulture = ""; cultureIdx = 0; }
        if (cultureDropdown != null) { cultureDropdown.SetValueWithoutNotify(cultureIdx); cultureDropdown.RefreshShownValue(); }

        // ── Date ────────────────────────────────────────────────────
        _dateValues = new List<string> { "" };
        var dateLabels = new List<string> { "Date" };
        if (filters.date != null)
            foreach (var d in filters.date)
                if (!string.IsNullOrEmpty(d.date)) { _dateValues.Add(d.date); dateLabels.Add(d.date); }
        RefreshDropdownOptions(dateDropdown, dateLabels);
        int dateIdx = _dateValues.IndexOf(_selectedDate);
        if (dateIdx < 0) { _selectedDate = ""; dateIdx = 0; }
        if (dateDropdown != null) { dateDropdown.SetValueWithoutNotify(dateIdx); dateDropdown.RefreshShownValue(); }
    }

    /// <summary>
    /// Re-populates a dropdown's option list WITHOUT reconfiguring its template.
    /// Template layout (VerticalLayoutGroup, ContentSizeFitter, etc.) is already
    /// set up from the first load via SetDropdown → ConfigureDropdownTemplate.
    /// </summary>
    private static void RefreshDropdownOptions(TMP_Dropdown dd, List<string> options)
    {
        if (dd == null) return;
        dd.ClearOptions();
        dd.AddOptions(options);
    }

    private static void SetDropdown(TMP_Dropdown dd, List<string> options, bool addSearch = false)
    {
        if (dd == null) return;
        dd.ClearOptions();
        dd.AddOptions(options);
        dd.SetValueWithoutNotify(0);
        dd.RefreshShownValue();
        // Auto-calculate popup max height: show up to 8 items (~52 px each) + padding
        int visible = Mathf.Min(options.Count, 8);
        float maxH = visible * 52f + 16f;
        ConfigureDropdownTemplate(dd, maxH, addSearch);
    }

    /// <summary>
    /// Configures a TMP_Dropdown template so that:
    ///  • Content stacks items via VerticalLayoutGroup + ContentSizeFitter.
    ///  • Each Item auto-heights based on its label text (word-wrap, same font size).
    ///  • Item Background and Checkmark are excluded from layout (ignoreLayout) so
    ///    they stay correctly anchored while the Item itself resizes.
    /// </summary>
    private static void ConfigureDropdownTemplate(TMP_Dropdown dd, float maxHeight = 320f, bool addSearch = false)
    {
        if (dd == null || dd.template == null) return;

        RectTransform template = dd.template;
        Vector2 sd = template.sizeDelta;
        template.sizeDelta = new Vector2(sd.x, maxHeight);

        Transform viewport = template.Find("Viewport");
        if (viewport == null) return;
        Transform content = viewport.Find("Content");
        if (content == null) return;
        GameObject contentGO = content.gameObject;

        // ── Content: VerticalLayoutGroup ────────────────────────────
        VerticalLayoutGroup vlg = contentGO.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = false;  // Items self-size via their own ContentSizeFitter
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing  = 2f;
        vlg.padding  = new RectOffset(4, 4, 4, 4);

        // ── Content: ContentSizeFitter ───────────────────────────────
        ContentSizeFitter csf = contentGO.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // ── Item template (cloned once per option) ───────────────────
        Transform item = content.Find("Item");
        if (item == null) return;

        // LayoutElement: only minHeight; height driven by ContentSizeFitter below
        LayoutElement le = item.GetComponent<LayoutElement>();
        if (le == null) le = item.gameObject.AddComponent<LayoutElement>();
        le.minHeight       = 40f;
        le.preferredHeight = -1f;   // unconstrained — let ContentSizeFitter decide

        // ContentSizeFitter on Item so every clone auto-sizes to its label text
        ContentSizeFitter itemCsf = item.GetComponent<ContentSizeFitter>();
        if (itemCsf == null) itemCsf = item.gameObject.AddComponent<ContentSizeFitter>();
        itemCsf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        itemCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // VerticalLayoutGroup on Item — drives preferred height from Item Label text
        // (left padding reserves space for the Checkmark icon)
        VerticalLayoutGroup itemVLG = item.GetComponent<VerticalLayoutGroup>();
        if (itemVLG == null) itemVLG = item.gameObject.AddComponent<VerticalLayoutGroup>();
        itemVLG.childControlWidth      = true;
        itemVLG.childControlHeight     = true;
        itemVLG.childForceExpandWidth  = true;
        itemVLG.childForceExpandHeight = false;
        itemVLG.padding = new RectOffset(36, 8, 8, 8); // 36 left keeps room for checkmark

        // Item Background — stays full-fill via anchors, excluded from layout
        Transform bg = item.Find("Item Background");
        if (bg != null)
        {
            LayoutElement bgLE = bg.GetComponent<LayoutElement>();
            if (bgLE == null) bgLE = bg.gameObject.AddComponent<LayoutElement>();
            bgLE.ignoreLayout = true;
            RectTransform bgRT = bg.GetComponent<RectTransform>();
            if (bgRT != null)
            {
                bgRT.anchorMin = Vector2.zero;
                bgRT.anchorMax = Vector2.one;
                bgRT.offsetMin = Vector2.zero;
                bgRT.offsetMax = Vector2.zero;
            }
        }

        // Item Checkmark — stays at its anchor position, excluded from layout
        Transform checkmark = item.Find("Item Checkmark");
        if (checkmark != null)
        {
            LayoutElement cmLE = checkmark.GetComponent<LayoutElement>();
            if (cmLE == null) cmLE = checkmark.gameObject.AddComponent<LayoutElement>();
            cmLE.ignoreLayout = true;
        }

        // Item Label — enable word wrap so long text flows to multiple lines
        // Font size is NOT changed — consistency maintained across all dropdowns
        Transform labelT = item.Find("Item Label");
        if (labelT != null)
        {
            TextMeshProUGUI label = labelT.GetComponent<TextMeshProUGUI>();
            if (label != null)
            {
                label.enableWordWrapping = true;
                label.overflowMode       = TextOverflowModes.Overflow;
            }
            // Reset anchors so the Item's VerticalLayoutGroup can control the label
            RectTransform labelRT = labelT.GetComponent<RectTransform>();
            if (labelRT != null)
            {
                labelRT.anchorMin = new Vector2(0f, 1f);
                labelRT.anchorMax = new Vector2(1f, 1f);
                labelRT.pivot     = new Vector2(0.5f, 1f);
                labelRT.offsetMin = Vector2.zero;
                labelRT.offsetMax = Vector2.zero;
            }
        }

        // ── Search field + spinner overlay injected into template ────────
        if (addSearch)
            InjectSearchAndSpinnerIntoTemplate(template, viewport?.GetComponent<RectTransform>());
    }

    /// <summary>
    /// Injects a search InputField at the top of the dropdown template and a
    /// semi-transparent spinner overlay that covers the popup during its 2-frame
    /// layout-rebuild phase. Both are cloned each time the dropdown opens (because
    /// TMP_Dropdown instantiates the entire template). The DropdownSearchController
    /// on the same GO connects and drives both children.
    /// </summary>
    private static void InjectSearchAndSpinnerIntoTemplate(
        RectTransform template, RectTransform viewportRT)
    {
        const float kSearchH = 48f;

        // Guard: already injected
        if (template.Find("SearchField") != null) return;

        // Expand template height to accommodate the search bar
        var tsd = template.sizeDelta;
        template.sizeDelta = new Vector2(tsd.x, tsd.y + kSearchH);

        // Push the Viewport down so the search bar sits above it
        if (viewportRT != null)
            viewportRT.offsetMax = new Vector2(viewportRT.offsetMax.x, -kSearchH);

        // ── SearchField ───────────────────────────────────────────────────
        var sfGO = new GameObject("SearchField",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        sfGO.transform.SetParent(template, false);
        sfGO.transform.SetSiblingIndex(0);   // first child = drawn first (below popup items)

        var sfRT = sfGO.GetComponent<RectTransform>();
        sfRT.anchorMin        = new Vector2(0f, 1f);
        sfRT.anchorMax        = new Vector2(1f, 1f);
        sfRT.pivot            = new Vector2(0.5f, 1f);
        sfRT.sizeDelta        = new Vector2(0f, kSearchH);
        sfRT.anchoredPosition = Vector2.zero;

        var sfImg = sfGO.GetComponent<Image>();
        sfImg.color         = new Color(0.12f, 0.12f, 0.12f, 1f);
        sfImg.raycastTarget = true;

        // ── InputField inside SearchField ─────────────────────────────────
        var infGO = new GameObject("InputField", typeof(RectTransform), typeof(CanvasRenderer));
        infGO.transform.SetParent(sfGO.transform, false);

        var infRT = infGO.GetComponent<RectTransform>();
        infRT.anchorMin = Vector2.zero;
        infRT.anchorMax = Vector2.one;
        infRT.offsetMin = new Vector2(12f, 6f);
        infRT.offsetMax = new Vector2(-12f, -6f);

        // Text Area
        var taGO = new GameObject("Text Area", typeof(RectTransform));
        taGO.transform.SetParent(infGO.transform, false);
        var taRT = taGO.GetComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero;
        taRT.anchorMax = Vector2.one;
        taRT.offsetMin = taRT.offsetMax = Vector2.zero;

        // Placeholder text
        var phGO = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer));
        phGO.transform.SetParent(taGO.transform, false);
        var phRT = phGO.GetComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero;
        phRT.anchorMax = Vector2.one;
        phRT.offsetMin = phRT.offsetMax = Vector2.zero;
        var phTMP = phGO.AddComponent<TextMeshProUGUI>();
        phTMP.text      = "Search...";
        phTMP.fontSize  = 16f;
        phTMP.color     = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        phTMP.fontStyle = FontStyles.Italic;
        phTMP.alignment = TextAlignmentOptions.Left;
        phTMP.raycastTarget = false;

        // Editable text
        var txGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer));
        txGO.transform.SetParent(taGO.transform, false);
        var txRT = txGO.GetComponent<RectTransform>();
        txRT.anchorMin = Vector2.zero;
        txRT.anchorMax = Vector2.one;
        txRT.offsetMin = txRT.offsetMax = Vector2.zero;
        var txTMP = txGO.AddComponent<TextMeshProUGUI>();
        txTMP.fontSize  = 16f;
        txTMP.color     = Color.white;
        txTMP.alignment = TextAlignmentOptions.Left;
        txTMP.raycastTarget = false;

        // Wire TMP_InputField
        var inf = infGO.AddComponent<TMP_InputField>();
        inf.textViewport  = taRT;
        inf.textComponent = txTMP;
        inf.placeholder   = phTMP;
        inf.lineType      = TMP_InputField.LineType.SingleLine;

        // ── SpinnerOverlay (covers popup during layout rebuild) ────────────
        var soGO = new GameObject("SpinnerOverlay",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        soGO.transform.SetParent(template, false);

        var soRT = soGO.GetComponent<RectTransform>();
        soRT.anchorMin = Vector2.zero;
        soRT.anchorMax = Vector2.one;
        soRT.offsetMin = soRT.offsetMax = Vector2.zero;

        var soImg = soGO.GetComponent<Image>();
        soImg.color         = new Color(0f, 0f, 0f, 0.72f);
        soImg.raycastTarget = true;

        // "Loading…" label centred on the overlay
        var ltGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
        ltGO.transform.SetParent(soGO.transform, false);
        var ltRT = ltGO.GetComponent<RectTransform>();
        ltRT.anchorMin = new Vector2(0.5f, 0.5f);
        ltRT.anchorMax = new Vector2(0.5f, 0.5f);
        ltRT.sizeDelta = new Vector2(220f, 50f);
        var ltTMP = ltGO.AddComponent<TextMeshProUGUI>();
        ltTMP.text          = "Loading…";
        ltTMP.fontSize      = 18f;
        ltTMP.color         = Color.white;
        ltTMP.alignment     = TextAlignmentOptions.Center;
        ltTMP.raycastTarget = false;

        soGO.SetActive(false); // hidden; DropdownSearchController shows/hides it

    }

    // ─────────────────────────────────────────────────────────────────
    // Event handlers — each triggers a new API call
    // ─────────────────────────────────────────────────────────────────

    private void OnSearchSubmit()
    {
        _searchQuery = searchInputField != null ? searchInputField.text.Trim() : "";
        _currentPage = 1;
        UpdateClearSearchVisibility();
        FetchCollection();
    }

    private void OnClearSearch()
    {
        if (searchInputField != null) searchInputField.text = "";
        _searchQuery = "";
        _currentPage = 1;
        UpdateClearSearchVisibility();
        FetchCollection();
    }

    private void UpdateClearSearchVisibility()
    {
        if (clearSearchButton != null)
            clearSearchButton.gameObject.SetActive(!string.IsNullOrEmpty(_searchQuery));
    }

    private void OnDepartmentChanged(int idx)
    {
        _selectedDeptId = idx < _deptIds.Count ? _deptIds[idx] : 0;
        _currentPage = 1;
        SetClearBtnVisible(_deptClearBtn, idx > 0);
        FetchCollection();
    }

    private void OnClassificationChanged(int idx)
    {
        _selectedClassId = idx < _classIds.Count ? _classIds[idx] : 0;
        _currentPage = 1;
        SetClearBtnVisible(_classClearBtn, idx > 0);
        FetchCollection();
    }

    private void OnArtistChanged(int idx)
    {
        _selectedArtistId = idx < _artistIds.Count ? _artistIds[idx] : 0;
        _currentPage = 1;
        SetClearBtnVisible(_artistClearBtn, idx > 0);
        FetchCollection();
    }

    private void OnCultureChanged(int idx)
    {
        _selectedCulture = idx < _cultureValues.Count ? _cultureValues[idx] : "";
        _currentPage = 1;
        SetClearBtnVisible(_cultureClearBtn, idx > 0);
        FetchCollection();
    }

    private void OnDateChanged(int idx)
    {
        _selectedDate = idx < _dateValues.Count ? _dateValues[idx] : "";
        _currentPage = 1;
        SetClearBtnVisible(_dateClearBtn, idx > 0);
        FetchCollection();
    }

    private static void SetClearBtnVisible(Button btn, bool visible)
    {
        if (btn != null) btn.gameObject.SetActive(visible);
    }

    private void OnClearFilters()
    {
        // Reset all filter dropdowns
        if (departmentDropdown != null) departmentDropdown.SetValueWithoutNotify(0);
        if (classificationDropdown != null) classificationDropdown.SetValueWithoutNotify(0);
        if (artistDropdown != null) artistDropdown.SetValueWithoutNotify(0);
        if (cultureDropdown != null) cultureDropdown.SetValueWithoutNotify(0);
        if (dateDropdown != null) dateDropdown.SetValueWithoutNotify(0);

        // Hide all individual clear buttons
        SetClearBtnVisible(_deptClearBtn,    false);
        SetClearBtnVisible(_classClearBtn,   false);
        SetClearBtnVisible(_artistClearBtn,  false);
        SetClearBtnVisible(_cultureClearBtn, false);
        SetClearBtnVisible(_dateClearBtn,    false);

        // Reset filter state
        _selectedDeptId = 0;
        _selectedClassId = 0;
        _selectedArtistId = 0;
        _selectedCulture = "";
        _selectedDate = "";

        // Reset search
        if (searchInputField != null) searchInputField.text = "";
        _searchQuery = "";
        UpdateClearSearchVisibility();

        // Reset sort
        if (sortByDropdown != null) sortByDropdown.SetValueWithoutNotify(0);
        _sortMode = SortMode.Default;

        // Reset per-page
        if (perPageDropdown != null) perPageDropdown.SetValueWithoutNotify(0);
        _itemsPerPage = 20;

        _currentPage = 1;
        FetchCollection();
    }

    private void OnPerPageChanged(int index)
    {
        int[] values = { 20, 40, 80 };
        _itemsPerPage = index < values.Length ? values[index] : 20;
        _currentPage = 1;
        FetchCollection();
    }

    private void OnSortChanged(int index)
    {
        _sortMode = (SortMode)index;
        _currentPage = 1;
        FetchCollection();
    }

    // ─────────────────────────────────────────────────────────────────
    // Card pool & display
    // ─────────────────────────────────────────────────────────────────

    private void EnsureCardPool()
    {
        while (_cardPool.Count < maxPoolSize)
        {
            CardItemUI card = CreateCardGameObject(_cardPool.Count);
            card.gameObject.SetActive(false);
            _cardPool.Add(card);
        }
    }

    private CardItemUI CreateCardGameObject(int index)
    {
        // Card root
        GameObject cardGO = new GameObject($"Card_{index:000}",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        cardGO.transform.SetParent(cardGridContent, false);
        cardGO.layer = gameObject.layer;

        Image cardBg = cardGO.GetComponent<Image>();
        cardBg.color = Color.white;

        Button cardBtn = cardGO.GetComponent<Button>();
        cardBtn.targetGraphic = cardBg;

        // Vertical layout
        VerticalLayoutGroup vlg = cardGO.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 8f;
        vlg.padding = new RectOffset(10, 10, 10, 10);

        // ── Artwork Image ───────────────────────────────────────────
        GameObject imgGO = new GameObject("ArtworkImage",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imgGO.transform.SetParent(cardGO.transform, false);

        Image artworkImg = imgGO.GetComponent<Image>();
        artworkImg.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        artworkImg.preserveAspect = true;
        artworkImg.raycastTarget = false;

        LayoutElement imgLE = imgGO.AddComponent<LayoutElement>();
        imgLE.preferredHeight = 360f;
        imgLE.flexibleWidth = 1f;

        // ── Title ───────────────────────────────────────────────────
        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer));
        titleGO.transform.SetParent(cardGO.transform, false);
        TextMeshProUGUI titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.fontSize = 28f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = Color.black;
        titleTMP.alignment = TextAlignmentOptions.TopLeft;
        titleTMP.enableWordWrapping = true;
        titleTMP.overflowMode = TextOverflowModes.Ellipsis;
        titleTMP.maxVisibleLines = 2;
        titleTMP.raycastTarget = false;
        LayoutElement titleLE = titleGO.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 70f;
        titleLE.flexibleWidth = 1f;

        // ── Artist ──────────────────────────────────────────────────
        GameObject artistGO = new GameObject("Artist", typeof(RectTransform), typeof(CanvasRenderer));
        artistGO.transform.SetParent(cardGO.transform, false);
        TextMeshProUGUI artistTMP = artistGO.AddComponent<TextMeshProUGUI>();
        artistTMP.fontSize = 24f;
        artistTMP.color = new Color(0.4f, 0.4f, 0.4f);
        artistTMP.alignment = TextAlignmentOptions.TopLeft;
        artistTMP.overflowMode = TextOverflowModes.Ellipsis;
        artistTMP.maxVisibleLines = 1;
        artistTMP.raycastTarget = false;
        LayoutElement artistLE = artistGO.AddComponent<LayoutElement>();
        artistLE.preferredHeight = 35f;
        artistLE.flexibleWidth = 1f;

        // ── Accession ───────────────────────────────────────────────
        GameObject accGO = new GameObject("Accession", typeof(RectTransform), typeof(CanvasRenderer));
        accGO.transform.SetParent(cardGO.transform, false);
        TextMeshProUGUI accTMP = accGO.AddComponent<TextMeshProUGUI>();
        accTMP.fontSize = 22f;
        accTMP.color = new Color(0.55f, 0.55f, 0.55f);
        accTMP.alignment = TextAlignmentOptions.TopLeft;
        accTMP.raycastTarget = false;
        LayoutElement accLE = accGO.AddComponent<LayoutElement>();
        accLE.preferredHeight = 30f;
        accLE.flexibleWidth = 1f;

        // ── CardItemUI component ────────────────────────────────────
        CardItemUI cardUI = cardGO.AddComponent<CardItemUI>();
        cardUI.AssignReferences(artworkImg, titleTMP, artistTMP, accTMP, cardBtn, cardBg);

        return cardUI;
    }

    private void DisplayCards(List<ResultsData> items)
    {
        if (items == null) items = new List<ResultsData>();

        // Bind active cards; collect them for masonry
        var activeCards = new List<CardItemUI>(items.Count);
        for (int i = 0; i < _cardPool.Count; i++)
        {
            if (i < items.Count)
            {
                _cardPool[i].Bind(items[i], mediaManager, OnCardClicked);
                activeCards.Add(_cardPool[i]);
            }
            else
            {
                _cardPool[i].Clear();
            }
        }

        // Scroll to top before masonry repositions cards
        if (cardScrollView != null)
            cardScrollView.normalizedPosition = new Vector2(0f, 1f);

        // Hand active cards to masonry; it will position them in LateUpdate
        if (masonryLayout != null)
            masonryLayout.SetCards(activeCards);
    }

    // ─────────────────────────────────────────────────────────────────
    // Result count & pagination
    // ─────────────────────────────────────────────────────────────────

    private void UpdateResultCount(Pagination pagination)
    {
        if (resultCountText == null) return;

        if (pagination != null && pagination.total > 0)
            resultCountText.text = $"{pagination.from} to {pagination.to} of total {pagination.total} results";
        else
            resultCountText.text = "No results found";
    }

    private void UpdatePaginationUI()
    {
        if (prevPageButton != null) prevPageButton.interactable = _currentPage > 1;
        if (nextPageButton != null) nextPageButton.interactable = _currentPage < _lastPage;

        if (pageInfoText != null)
            pageInfoText.text = $" ... {_lastPage}";
            //pageInfoText.text = $"Page {_currentPage} of {_lastPage}";

        BuildPageNumberButtons();
    }

    private void BuildPageNumberButtons()
    {
        if (pageNumbersContainer == null) return;

        for (int i = pageNumbersContainer.childCount - 1; i >= 0; i--)
            Destroy(pageNumbersContainer.GetChild(i).gameObject);

        int start = Mathf.Max(1, _currentPage - 3);
        int end = Mathf.Min(_lastPage, start + 6);
        start = Mathf.Max(1, end - 6);

        for (int p = start; p <= end; p++)
        {
            int pageIndex = p;
            GameObject btnGO = new GameObject($"Page_{p}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(pageNumbersContainer, false);

            RectTransform rt = btnGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(80f, 70f);

            Image bg = btnGO.GetComponent<Image>();
            bg.color = pageIndex == _currentPage
                ? new Color(0.85f,0.15f,0.45f,1f)
                : new Color(0.25f,0.25f,0.25f,1f);

            //bg.color = pageIndex == _currentPage ? Color.white : new Color(0,0,0, 0f);
            //bg.sprite = pageIndex == _currentPage ? selectedPageSprite : null;

            Button btn = btnGO.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => GoToPage(pageIndex));

            GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
            labelGO.transform.SetParent(btnGO.transform, false);
            RectTransform labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = p.ToString();
            tmp.fontSize = 28f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            //tmp.font = TMP_FontAsset;
        }
    }

    private void GoToPage(int page)
    {
        _currentPage = page;
        FetchCollection();
    }

    private void NextPage()
    {
        if (_currentPage < _lastPage)
            GoToPage(_currentPage + 1);
    }

    private void PreviousPage()
    {
        if (_currentPage > 1)
            GoToPage(_currentPage - 1);
    }

    // ─────────────────────────────────────────────────────────────────
    // Card click → screen transition
    // ─────────────────────────────────────────────────────────────────

    private void OnCardClicked(ResultsData data)
    {
        if (_isDragging) return;
        if (data == null || string.IsNullOrEmpty(data.primary_image)) return;
        Debug.Log($"[CollectionUI] Selected: {data.title}");
        LoadAndOpenCropScreen(data);
    }

    private void LoadAndOpenCropScreen(ResultsData data)
    {
        PopupManager.Instance.ShowLoading();
        PopupManager.Instance.SetProgressText("Loading image...");

        // Pass artwork metadata to CropScreenController before loading
        if (cropScreenController != null)
            cropScreenController.SetArtworkData(data);

        if (mediaManager != null && imageToCropTarget != null)
        {
            mediaManager.LoadSprite(data.primary_image, sprite =>
            {
                PopupManager.Instance.HideLoading();
                if (sprite != null)
                {
                    imageToCropTarget.sprite         = sprite;
                    imageToCropTarget.preserveAspect = true;
                }

                if (ScreenManager.Instance != null)
                {
                    ScreenManager.Instance.ShowScreen(cropImageScreen);
                }
                else
                {
                    if (collectionScreen != null) collectionScreen.SetActive(false);
                    if (cropImageScreen  != null) cropImageScreen.SetActive(true);
                }
            });
        }
    }

    private void OnBackPressed()
    {
        if (ScreenManager.Instance != null)
            ScreenManager.Instance.GoBack();
    }

    // ─────────────────────────────────────────────────────────────────
    // Drag detection — prevent accidental card clicks while scrolling
    // ─────────────────────────────────────────────────────────────────

    private void WireScrollDragEvents()
    {
        if (cardScrollView == null) return;

        EventTrigger trigger = cardScrollView.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = cardScrollView.gameObject.AddComponent<EventTrigger>();

        // BeginDrag
        var beginEntry = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
        beginEntry.callback.AddListener(_ => OnScrollBeginDrag());
        trigger.triggers.Add(beginEntry);

        // EndDrag
        var endEntry = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
        endEntry.callback.AddListener(_ => OnScrollEndDrag());
        trigger.triggers.Add(endEntry);
    }

    private void OnScrollBeginDrag()
    {
        _isDragging = true;
        SetCardButtonsInteractable(false);
    }

    private void OnScrollEndDrag()
    {
        // Small delay before re-enabling so the pointer-up from the drag
        // doesn't register as a click on the card underneath.
        StartCoroutine(ReEnableCardsAfterDrag());
    }

    private IEnumerator ReEnableCardsAfterDrag()
    {
        yield return null; // wait one frame
        _isDragging = false;
        SetCardButtonsInteractable(true);
    }

    private void SetCardButtonsInteractable(bool interactable)
    {
        foreach (var card in _cardPool)
        {
            if (card != null && card.CardButton != null)
                card.CardButton.interactable = interactable;
        }
    }
}
