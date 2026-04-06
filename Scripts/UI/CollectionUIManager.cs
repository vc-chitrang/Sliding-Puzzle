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

    [Header("Pagination")]
    [SerializeField] private Button prevPageButton;
    [SerializeField] private Button nextPageButton;
    [SerializeField] private RectTransform pageNumbersContainer;
    [SerializeField] private TextMeshProUGUI pageInfoText;

    [Header("Screen Flow")]
    [SerializeField] private GameObject collectionScreen;
    [SerializeField] private GameObject cropImageScreen;
    [SerializeField] private Image imageToCropTarget;

    [Header("Pool Settings")]
    [SerializeField] private int maxPoolSize = 80;

    [Header("Card Loading")]
    [Tooltip("Spinner prefab shown on each card while its image loads.")]
    [SerializeField] private GameObject spinnerPrefab;

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
            perPageDropdown.onValueChanged.AddListener(OnPerPageChanged);
            ConfigureDropdownTemplate(perPageDropdown, 140f); // 3 items × ~44px ≈ 132px
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
            sortByDropdown.onValueChanged.AddListener(OnSortChanged);
            ConfigureDropdownTemplate(sortByDropdown, 300f); // 7 items × ~44px ≈ 308px
        }

        // Pagination
        if (prevPageButton != null) prevPageButton.onClick.AddListener(PreviousPage);
        if (nextPageButton != null) nextPageButton.onClick.AddListener(NextPage);

        // Drag detection — disable card buttons while scrolling to prevent accidental clicks
        WireScrollDragEvents();

        UpdateClearSearchVisibility();

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
    }

    // ─────────────────────────────────────────────────────────────────
    // Initial data
    // ─────────────────────────────────────────────────────────────────

    private void OnInitialDataReceived(MAPData data)
    {
        OnFetchSuccess(data);
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

        // Populate filter dropdowns once (from the first response)
        if (!_filtersPopulated && data.filters != null)
        {
            PopulateFilterDropdowns(data.filters);
            _filtersPopulated = true;
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
        SetDropdown(departmentDropdown, deptLabels);

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
        SetDropdown(classificationDropdown, classLabels);

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
        SetDropdown(artistDropdown, artistLabels);

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
        SetDropdown(cultureDropdown, cultureLabels);

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
        SetDropdown(dateDropdown, dateLabels);
    }

    private static void SetDropdown(TMP_Dropdown dd, List<string> options)
    {
        if (dd == null) return;
        dd.ClearOptions();
        dd.AddOptions(options);
        dd.SetValueWithoutNotify(0);
        dd.RefreshShownValue();
        // Auto-calculate popup max height: show up to 8 items (~52 px each) + padding
        int visible = Mathf.Min(options.Count, 8);
        float maxH = visible * 52f + 16f;
        ConfigureDropdownTemplate(dd, maxH);
    }

    /// <summary>
    /// Configures a TMP_Dropdown template so that:
    ///  • Content stacks items via VerticalLayoutGroup + ContentSizeFitter.
    ///  • Each Item auto-heights based on its label text (word-wrap, same font size).
    ///  • Item Background and Checkmark are excluded from layout (ignoreLayout) so
    ///    they stay correctly anchored while the Item itself resizes.
    /// </summary>
    private static void ConfigureDropdownTemplate(TMP_Dropdown dd, float maxHeight = 320f)
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
        FetchCollection();
    }

    private void OnClassificationChanged(int idx)
    {
        _selectedClassId = idx < _classIds.Count ? _classIds[idx] : 0;
        _currentPage = 1;
        FetchCollection();
    }

    private void OnArtistChanged(int idx)
    {
        _selectedArtistId = idx < _artistIds.Count ? _artistIds[idx] : 0;
        _currentPage = 1;
        FetchCollection();
    }

    private void OnCultureChanged(int idx)
    {
        _selectedCulture = idx < _cultureValues.Count ? _cultureValues[idx] : "";
        _currentPage = 1;
        FetchCollection();
    }

    private void OnDateChanged(int idx)
    {
        _selectedDate = idx < _dateValues.Count ? _dateValues[idx] : "";
        _currentPage = 1;
        FetchCollection();
    }

    private void OnClearFilters()
    {
        // Reset all filter dropdowns
        if (departmentDropdown != null) departmentDropdown.SetValueWithoutNotify(0);
        if (classificationDropdown != null) classificationDropdown.SetValueWithoutNotify(0);
        if (artistDropdown != null) artistDropdown.SetValueWithoutNotify(0);
        if (cultureDropdown != null) cultureDropdown.SetValueWithoutNotify(0);
        if (dateDropdown != null) dateDropdown.SetValueWithoutNotify(0);

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

        for (int i = 0; i < _cardPool.Count; i++)
        {
            if (i < items.Count)
                _cardPool[i].Bind(items[i], mediaManager, OnCardClicked);
            else
                _cardPool[i].Clear();
        }

        // Scroll to top
        if (cardScrollView != null)
            cardScrollView.normalizedPosition = new Vector2(0f, 1f);
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
            pageInfoText.text = $"Page {_currentPage} of {_lastPage}";

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
                ? new Color(0.85f, 0.15f, 0.45f, 1f)
                : new Color(0.25f, 0.25f, 0.25f, 1f);

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

        PopupManager.Instance.ShowLoading();
        PopupManager.Instance.SetProgressText("Loading image...");

        if (mediaManager != null && imageToCropTarget != null)
        {
            mediaManager.LoadSprite(data.primary_image, sprite =>
            {
                PopupManager.Instance.HideLoading();
                if (sprite != null)
                {
                    imageToCropTarget.sprite = sprite;
                    imageToCropTarget.preserveAspect = true;
                }
                if (collectionScreen != null) collectionScreen.SetActive(false);
                if (cropImageScreen != null) cropImageScreen.SetActive(true);
            });
        }
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
