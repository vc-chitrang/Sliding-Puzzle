using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// Editor utility: creates the full Collection browsing screen hierarchy
/// under PuzzleCanvas with proper layout, spacing, clear/search buttons.
/// Run via Tools > Sliding Puzzle > Setup Collection Screen.
/// </summary>
public static class CollectionScreenSetup
{
    private const float REF_W = 2160f;
    private const float REF_H = 3840f;

    // Colors
    private static readonly Color DarkBg    = new Color(0.12f, 0.12f, 0.12f, 1f);
    private static readonly Color MedGrey   = new Color(0.3f, 0.3f, 0.3f, 1f);
    private static readonly Color LightGrey = new Color(0.93f, 0.93f, 0.93f, 1f);
    private static readonly Color AccentPink = new Color(0.85f, 0.15f, 0.45f, 1f);
    private static readonly Color TextWhite = Color.white;
    private static readonly Color TextDark  = new Color(0.15f, 0.15f, 0.15f, 1f);

    [MenuItem("Tools/Sliding Puzzle/Setup Collection Screen")]
    public static void Setup()
    {
        GameObject canvasGO = FindInScene("PuzzleCanvas");
        if (canvasGO == null) { Debug.LogError("[CollectionSetup] PuzzleCanvas not found!"); return; }

        // Remove old
        Transform existing = canvasGO.transform.Find("CollectionScreen");
        if (existing != null) { Undo.DestroyObjectImmediate(existing.gameObject); }

        // Root
        GameObject screen = CreateUI("CollectionScreen", canvasGO.transform, true);
        Stretch(screen);
        screen.AddComponent<CanvasRenderer>();
        screen.AddComponent<Image>().color = DarkBg;
        screen.layer = canvasGO.layer;

        float yOff = 0f;

        // ═════════════════════════════════════════════════════════════
        // 1. HEADER  (160px)
        // ═════════════════════════════════════════════════════════════
        float headerH = 160f;
        GameObject header = Section("Header", screen.transform, yOff, headerH);
        header.AddComponent<Image>().color = DarkBg;
        MakeTMP("TitleText", header.transform, "The Collection at MAP",
            56f, FontStyles.Bold, TextWhite, TextAlignmentOptions.MidlineLeft, 40, 0, -40, 0);
        yOff += headerH;

        // ═════════════════════════════════════════════════════════════
        // 2. SEARCH BAR  (120px)
        //    [ SearchInputField  [✕] ]  [🔍 Search]
        // ═════════════════════════════════════════════════════════════
        float searchH = 120f;
        GameObject searchBar = Section("SearchBar", screen.transform, yOff, searchH);
        searchBar.AddComponent<Image>().color = DarkBg;
        HorizontalLayoutGroup searchHLG = searchBar.AddComponent<HorizontalLayoutGroup>();
        searchHLG.childAlignment = TextAnchor.MiddleCenter;
        searchHLG.spacing = 15f;
        searchHLG.padding = new RectOffset(40, 40, 15, 15);
        searchHLG.childForceExpandWidth = false;
        searchHLG.childForceExpandHeight = true;

        // -- Input field container
        GameObject inputContainer = CreateUI("SearchInputField", searchBar.transform);
        inputContainer.AddComponent<CanvasRenderer>();
        Image inputBg = inputContainer.AddComponent<Image>();
        inputBg.color = LightGrey;
        LayoutElement inputLE = inputContainer.AddComponent<LayoutElement>();
        inputLE.flexibleWidth = 1f;

        // Text Area
        GameObject textArea = CreateUI("Text Area", inputContainer.transform);
        RectTransform textAreaRT = Stretch(textArea);
        textAreaRT.offsetMin = new Vector2(20f, 0f);
        textAreaRT.offsetMax = new Vector2(-60f, 0f); // leave room for ✕
        textArea.AddComponent<RectMask2D>();

        GameObject phGO = CreateUI("Placeholder", textArea.transform); Stretch(phGO);
        TextMeshProUGUI phTMP = phGO.AddComponent<TextMeshProUGUI>();
        phTMP.text = "Search..."; phTMP.fontSize = 34f; phTMP.fontStyle = FontStyles.Italic;
        phTMP.color = new Color(0.5f, 0.5f, 0.5f, 0.7f); phTMP.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject txtGO = CreateUI("Text", textArea.transform); Stretch(txtGO);
        TextMeshProUGUI txtTMP = txtGO.AddComponent<TextMeshProUGUI>();
        txtTMP.fontSize = 34f; txtTMP.color = TextDark; txtTMP.alignment = TextAlignmentOptions.MidlineLeft;

        TMP_InputField inputField = inputContainer.AddComponent<TMP_InputField>();
        inputField.textViewport = textAreaRT;
        inputField.textComponent = txtTMP;
        inputField.placeholder = phTMP;

        // -- Clear ✕ button (inside input, right side)
        GameObject clearBtnGO = CreateUI("ClearSearchBtn", inputContainer.transform);
        RectTransform clearRT = clearBtnGO.GetComponent<RectTransform>();
        clearRT.anchorMin = new Vector2(1f, 0f); clearRT.anchorMax = new Vector2(1f, 1f);
        clearRT.pivot = new Vector2(1f, 0.5f);
        clearRT.anchoredPosition = new Vector2(-8f, 0f); clearRT.sizeDelta = new Vector2(55f, 0f);
        clearBtnGO.AddComponent<CanvasRenderer>();
        Image clearImg = clearBtnGO.AddComponent<Image>(); clearImg.color = new Color(0, 0, 0, 0);
        Button clearBtn = clearBtnGO.AddComponent<Button>(); clearBtn.targetGraphic = clearImg;
        MakeTMPChild("✕", clearBtnGO.transform, 36f, FontStyles.Bold, new Color(0.4f, 0.4f, 0.4f));

        // -- Search button
        GameObject searchBtnGO = CreateUI("SearchButton", searchBar.transform);
        searchBtnGO.AddComponent<CanvasRenderer>();
        Image sBg = searchBtnGO.AddComponent<Image>(); sBg.color = AccentPink;
        Button searchBtn = searchBtnGO.AddComponent<Button>(); searchBtn.targetGraphic = sBg;
        LayoutElement sLE = searchBtnGO.AddComponent<LayoutElement>();
        sLE.preferredWidth = 200f;
        MakeTMPChild("Search", searchBtnGO.transform, 32f, FontStyles.Bold, TextWhite);

        yOff += searchH;

        // ═════════════════════════════════════════════════════════════
        // 3. FILTER BAR  (120px)
        //    [Dept ▼] [Class ▼] [Artist ▼] [Culture ▼] [Date ▼] [Clear Filters]
        // ═════════════════════════════════════════════════════════════
        float filterH = 120f;
        GameObject filterBar = Section("FilterBar", screen.transform, yOff, filterH);
        filterBar.AddComponent<Image>().color = DarkBg;
        HorizontalLayoutGroup fHLG = filterBar.AddComponent<HorizontalLayoutGroup>();
        fHLG.childAlignment = TextAnchor.MiddleCenter;
        fHLG.spacing = 12f;
        fHLG.padding = new RectOffset(30, 30, 12, 12);
        fHLG.childForceExpandWidth = false;
        fHLG.childForceExpandHeight = true;

        TMP_Dropdown deptDD  = FilterDD("DepartmentDD",     filterBar.transform, "Department");
        TMP_Dropdown classDD = FilterDD("ClassificationDD", filterBar.transform, "Classification");
        TMP_Dropdown artDD   = FilterDD("ArtistDD",         filterBar.transform, "Artist/Maker");
        TMP_Dropdown culDD   = FilterDD("CultureDD",        filterBar.transform, "Place of origin");
        TMP_Dropdown dateDD  = FilterDD("DateDD",           filterBar.transform, "Date");

        // Clear Filters button
        GameObject cfGO = CreateUI("ClearFiltersBtn", filterBar.transform);
        cfGO.AddComponent<CanvasRenderer>();
        Image cfBg = cfGO.AddComponent<Image>(); cfBg.color = new Color(0.5f, 0.15f, 0.15f, 1f);
        Button cfBtn = cfGO.AddComponent<Button>(); cfBtn.targetGraphic = cfBg;
        LayoutElement cfLE = cfGO.AddComponent<LayoutElement>(); cfLE.preferredWidth = 220f;
        MakeTMPChild("Clear Filters", cfGO.transform, 24f, FontStyles.Normal, TextWhite);

        yOff += filterH;

        // ═════════════════════════════════════════════════════════════
        // 4. RESULT INFO BAR  (90px)
        //    "1-20 of 28626 results"   [Results Per Page ▼]  Sort By [Default ▼]
        // ═════════════════════════════════════════════════════════════
        float infoH = 90f;
        GameObject infoBar = Section("ResultInfoBar", screen.transform, yOff, infoH);
        infoBar.AddComponent<Image>().color = DarkBg;
        HorizontalLayoutGroup iHLG = infoBar.AddComponent<HorizontalLayoutGroup>();
        iHLG.childAlignment = TextAnchor.MiddleLeft; iHLG.spacing = 15f;
        iHLG.padding = new RectOffset(40, 40, 8, 8);
        iHLG.childForceExpandHeight = true; iHLG.childForceExpandWidth = false;

        // Count text
        GameObject cntGO = CreateUI("ResultCountText", infoBar.transform);
        TextMeshProUGUI cntTMP = cntGO.AddComponent<TextMeshProUGUI>();
        cntTMP.text = "Loading..."; cntTMP.fontSize = 28f; cntTMP.color = TextWhite;
        cntTMP.alignment = TextAlignmentOptions.MidlineLeft;
        cntGO.AddComponent<LayoutElement>().flexibleWidth = 1f;

        // Spacer
        CreateUI("Spacer", infoBar.transform).AddComponent<LayoutElement>().flexibleWidth = 1f;

        // "Results Per Page" label
        MakeLabel("Results Per Page", infoBar.transform, 120f);

        // Per-page dropdown
        TMP_Dropdown ppDD = SmallDD("PerPageDD", infoBar.transform, "20", 130f);

        // "Sort By" label
        MakeLabel("Sort By", infoBar.transform, 110f);

        // Sort dropdown
        TMP_Dropdown sortDD = SmallDD("SortByDD", infoBar.transform, "Default", 260f);

        yOff += infoH;

        // ═════════════════════════════════════════════════════════════
        // 5. CARD SCROLL VIEW  (fills middle)
        // ═════════════════════════════════════════════════════════════
        float pagH = 110f;
        GameObject scrollGO = CreateUI("CardScrollView", screen.transform);
        RectTransform scRT = scrollGO.GetComponent<RectTransform>();
        scRT.anchorMin = Vector2.zero; scRT.anchorMax = Vector2.one;
        scRT.offsetMin = new Vector2(0f, pagH); scRT.offsetMax = new Vector2(0f, -yOff);
        scrollGO.AddComponent<CanvasRenderer>();
        scrollGO.AddComponent<Image>().color = LightGrey;
        scrollGO.AddComponent<RectMask2D>();

        ScrollRect sr = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Elastic; sr.scrollSensitivity = 50f;

        // Content
        GameObject gridGO = CreateUI("CardGrid", scrollGO.transform);
        RectTransform gridRT = gridGO.GetComponent<RectTransform>();
        gridRT.anchorMin = new Vector2(0, 1); gridRT.anchorMax = new Vector2(1, 1);
        gridRT.pivot = new Vector2(0.5f, 1f); gridRT.sizeDelta = Vector2.zero;

        GridLayoutGroup glg = gridGO.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(490f, 600f);
        glg.spacing = new Vector2(20f, 20f);
        glg.padding = new RectOffset(25, 25, 15, 15);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 4;
        glg.childAlignment = TextAnchor.UpperLeft;
        glg.startCorner = GridLayoutGroup.Corner.UpperLeft;

        gridGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = gridRT; sr.viewport = scRT;

        // ═════════════════════════════════════════════════════════════
        // 6. PAGINATION BAR  (110px, anchored bottom)
        // ═════════════════════════════════════════════════════════════
        GameObject pagBar = CreateUI("PaginationBar", screen.transform);
        RectTransform pRT = pagBar.GetComponent<RectTransform>();
        pRT.anchorMin = Vector2.zero; pRT.anchorMax = new Vector2(1, 0);
        pRT.pivot = new Vector2(0.5f, 0f); pRT.anchoredPosition = Vector2.zero;
        pRT.sizeDelta = new Vector2(0f, pagH);
        pagBar.AddComponent<CanvasRenderer>(); pagBar.AddComponent<Image>().color = DarkBg;

        HorizontalLayoutGroup pHLG = pagBar.AddComponent<HorizontalLayoutGroup>();
        pHLG.childAlignment = TextAnchor.MiddleCenter; pHLG.spacing = 10f;
        pHLG.padding = new RectOffset(40, 40, 8, 8);
        pHLG.childForceExpandWidth = false; pHLG.childForceExpandHeight = true;

        Button prevBtn = PagButton("PrevButton", pagBar.transform, "<");

        // Page numbers container
        GameObject pnGO = CreateUI("PageNumbers", pagBar.transform);
        HorizontalLayoutGroup pnH = pnGO.AddComponent<HorizontalLayoutGroup>();
        pnH.spacing = 8f; pnH.childForceExpandWidth = false; pnH.childForceExpandHeight = true;
        pnH.childAlignment = TextAnchor.MiddleCenter;
        pnGO.AddComponent<LayoutElement>().flexibleWidth = 1f;
        RectTransform pnRT = pnGO.GetComponent<RectTransform>();

        // Page info text
        GameObject piGO = CreateUI("PageInfoText", pagBar.transform);
        TextMeshProUGUI piTMP = piGO.AddComponent<TextMeshProUGUI>();
        piTMP.text = ""; piTMP.fontSize = 24f; piTMP.color = TextWhite;
        piTMP.alignment = TextAlignmentOptions.Center;
        piGO.AddComponent<LayoutElement>().preferredWidth = 220f;

        Button nextBtn = PagButton("NextButton", pagBar.transform, ">");

        // ═════════════════════════════════════════════════════════════
        // 7. WIRE REFERENCES
        // ═════════════════════════════════════════════════════════════
        CollectionUIManager mgr = screen.AddComponent<CollectionUIManager>();

        MediaManager mm = Object.FindFirstObjectByType<MediaManager>(FindObjectsInactive.Include);

        // Load spinner prefab from known asset path
        GameObject spinnerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Games/Sliding-Puzzle/ThirdPartyPlugins/Animated Loading Icons/Prefabs/Spinner/Spinner 4.prefab");
        if (spinnerPrefab == null)
            Debug.LogWarning("[CollectionSetup] Spinner prefab not found at expected path.");

        GameObject cropScr = null; Image imgTarget = null;
        foreach (GameObject r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (cropScr == null) { var f = FindChild(r.transform, "CropImageScreen"); if (f != null) cropScr = f; }
        }
        if (cropScr != null) { var ig = FindChild(cropScr.transform, "ImageToCrop"); if (ig != null) imgTarget = ig.GetComponent<Image>(); }

        SerializedObject so = new SerializedObject(mgr);
        so.FindProperty("mediaManager").objectReferenceValue = mm;
        so.FindProperty("searchInputField").objectReferenceValue = inputField;
        so.FindProperty("searchButton").objectReferenceValue = searchBtn;
        so.FindProperty("clearSearchButton").objectReferenceValue = clearBtn;
        so.FindProperty("departmentDropdown").objectReferenceValue = deptDD;
        so.FindProperty("classificationDropdown").objectReferenceValue = classDD;
        so.FindProperty("artistDropdown").objectReferenceValue = artDD;
        so.FindProperty("cultureDropdown").objectReferenceValue = culDD;
        so.FindProperty("dateDropdown").objectReferenceValue = dateDD;
        so.FindProperty("clearFiltersButton").objectReferenceValue = cfBtn;
        so.FindProperty("resultCountText").objectReferenceValue = cntTMP;
        so.FindProperty("perPageDropdown").objectReferenceValue = ppDD;
        so.FindProperty("sortByDropdown").objectReferenceValue = sortDD;
        so.FindProperty("cardScrollView").objectReferenceValue = sr;
        so.FindProperty("cardGridContent").objectReferenceValue = gridRT;
        so.FindProperty("prevPageButton").objectReferenceValue = prevBtn;
        so.FindProperty("nextPageButton").objectReferenceValue = nextBtn;
        so.FindProperty("pageNumbersContainer").objectReferenceValue = pnRT;
        so.FindProperty("pageInfoText").objectReferenceValue = piTMP;
        so.FindProperty("collectionScreen").objectReferenceValue = screen;
        so.FindProperty("cropImageScreen").objectReferenceValue = cropScr;
        so.FindProperty("imageToCropTarget").objectReferenceValue = imgTarget;
        so.FindProperty("spinnerPrefab").objectReferenceValue = spinnerPrefab;
        so.ApplyModifiedProperties();

        screen.transform.SetAsFirstSibling();
        EditorUtility.SetDirty(screen);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("<color=green>[CollectionSetup] Done! Save the scene.</color>");
        Selection.activeGameObject = screen;
    }

    // ═════════════════════════════════════════════════════════════════
    // Helpers
    // ═════════════════════════════════════════════════════════════════

    private static GameObject Section(string n, Transform p, float top, float h)
    {
        GameObject go = CreateUI(n, p);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -top); rt.sizeDelta = new Vector2(0, h);
        return go;
    }

    private static TextMeshProUGUI MakeTMP(string n, Transform p, string t,
        float fs, FontStyles st, Color c, TextAlignmentOptions a,
        float oMinX = 0, float oMinY = 0, float oMaxX = 0, float oMaxY = 0)
    {
        GameObject go = CreateUI(n, p); RectTransform rt = Stretch(go);
        rt.offsetMin = new Vector2(oMinX, oMinY); rt.offsetMax = new Vector2(oMaxX, oMaxY);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = t; tmp.fontSize = fs; tmp.fontStyle = st; tmp.color = c;
        tmp.alignment = a; tmp.raycastTarget = false; return tmp;
    }

    private static void MakeTMPChild(string text, Transform parent, float fs, FontStyles st, Color c)
    {
        GameObject go = CreateUI("Label", parent); Stretch(go);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = fs; tmp.fontStyle = st; tmp.color = c;
        tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
    }

    private static void MakeLabel(string text, Transform parent, float width)
    {
        GameObject go = CreateUI(text.Replace(" ", ""), parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = 24f; tmp.color = TextWhite;
        tmp.alignment = TextAlignmentOptions.MidlineRight; tmp.raycastTarget = false;
        go.AddComponent<LayoutElement>().preferredWidth = width;
    }

    // ── Filter dropdown with flexibleWidth = 1 ──────────────────────
    private static TMP_Dropdown FilterDD(string name, Transform parent, string label)
    {
        TMP_Dropdown dd = BuildDropdown(name, parent, label);
        dd.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        return dd;
    }

    // ── Small fixed-width dropdown ──────────────────────────────────
    private static TMP_Dropdown SmallDD(string name, Transform parent, string label, float w)
    {
        TMP_Dropdown dd = BuildDropdown(name, parent, label);
        dd.gameObject.AddComponent<LayoutElement>().preferredWidth = w;
        return dd;
    }

    // ── Core dropdown builder ───────────────────────────────────────
    private static TMP_Dropdown BuildDropdown(string name, Transform parent, string label)
    {
        GameObject go = CreateUI(name, parent);
        go.AddComponent<CanvasRenderer>();
        Image bg = go.AddComponent<Image>(); bg.color = MedGrey;

        // Caption
        GameObject capGO = CreateUI("Label", go.transform);
        RectTransform capRT = Stretch(capGO);
        capRT.offsetMin = new Vector2(12f, 0); capRT.offsetMax = new Vector2(-28f, 0);
        TextMeshProUGUI capTMP = capGO.AddComponent<TextMeshProUGUI>();
        capTMP.text = label; capTMP.fontSize = 24f; capTMP.color = TextWhite;
        capTMP.alignment = TextAlignmentOptions.MidlineLeft;
        capTMP.overflowMode = TextOverflowModes.Ellipsis;

        // Arrow ▼
        GameObject arGO = CreateUI("Arrow", go.transform);
        RectTransform arRT = arGO.GetComponent<RectTransform>();
        arRT.anchorMin = new Vector2(1, 0.5f); arRT.anchorMax = new Vector2(1, 0.5f);
        arRT.pivot = new Vector2(1, 0.5f); arRT.anchoredPosition = new Vector2(-8, 0);
        arRT.sizeDelta = new Vector2(26, 26);
        arGO.AddComponent<CanvasRenderer>();
        TextMeshProUGUI arT = arGO.AddComponent<TextMeshProUGUI>();
        arT.text = "\u25BC"; arT.fontSize = 18f; arT.color = TextWhite;
        arT.alignment = TextAlignmentOptions.Center;

        // Template
        GameObject tplGO = CreateUI("Template", go.transform);
        RectTransform tplRT = tplGO.GetComponent<RectTransform>();
        tplRT.anchorMin = new Vector2(0, 0); tplRT.anchorMax = new Vector2(1, 0);
        tplRT.pivot = new Vector2(0.5f, 1f); tplRT.anchoredPosition = Vector2.zero;
        tplRT.sizeDelta = new Vector2(0, 400);
        tplGO.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f, 0.97f);
        ScrollRect tplScr = tplGO.AddComponent<ScrollRect>(); tplScr.horizontal = false;

        GameObject vpGO = CreateUI("Viewport", tplGO.transform); Stretch(vpGO);
        vpGO.AddComponent<RectMask2D>();
        vpGO.AddComponent<Image>().color = Color.clear;
        tplScr.viewport = vpGO.GetComponent<RectTransform>();

        GameObject cGO = CreateUI("Content", vpGO.transform);
        RectTransform cRT = cGO.GetComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0, 1); cRT.anchorMax = new Vector2(1, 1);
        cRT.pivot = new Vector2(0.5f, 1); cRT.sizeDelta = Vector2.zero;
        tplScr.content = cRT;

        // Item
        GameObject itGO = CreateUI("Item", cGO.transform);
        RectTransform itRT = itGO.GetComponent<RectTransform>();
        itRT.anchorMin = new Vector2(0, 0.5f); itRT.anchorMax = new Vector2(1, 0.5f);
        itRT.sizeDelta = new Vector2(0, 55);
        itGO.AddComponent<CanvasRenderer>();
        Image itBg = itGO.AddComponent<Image>(); itBg.color = Color.clear;
        Toggle tgl = itGO.AddComponent<Toggle>();

        GameObject ilGO = CreateUI("Item Label", itGO.transform);
        RectTransform ilRT = Stretch(ilGO); ilRT.offsetMin = new Vector2(12, 0);
        TextMeshProUGUI ilT = ilGO.AddComponent<TextMeshProUGUI>();
        ilT.fontSize = 24f; ilT.color = TextWhite; ilT.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject ckGO = CreateUI("Item Checkmark", itGO.transform);
        RectTransform ckRT = ckGO.GetComponent<RectTransform>();
        ckRT.anchorMin = new Vector2(1, 0.5f); ckRT.anchorMax = new Vector2(1, 0.5f);
        ckRT.pivot = new Vector2(1, 0.5f); ckRT.anchoredPosition = new Vector2(-8, 0);
        ckRT.sizeDelta = new Vector2(36, 36);
        ckGO.AddComponent<CanvasRenderer>();
        TextMeshProUGUI ckT = ckGO.AddComponent<TextMeshProUGUI>();
        ckT.text = "\u2713"; ckT.fontSize = 26f; ckT.color = AccentPink;
        ckT.alignment = TextAlignmentOptions.Center;

        tgl.targetGraphic = itBg; tgl.graphic = ckT;
        tplGO.SetActive(false);

        TMP_Dropdown dd = go.AddComponent<TMP_Dropdown>();
        dd.targetGraphic = bg; dd.template = tplRT;
        dd.captionText = capTMP; dd.itemText = ilT;
        dd.ClearOptions();
        dd.AddOptions(new System.Collections.Generic.List<string> { label });
        return dd;
    }

    private static Button PagButton(string name, Transform parent, string label)
    {
        GameObject go = CreateUI(name, parent);
        go.AddComponent<CanvasRenderer>();
        Image bg = go.AddComponent<Image>(); bg.color = MedGrey;
        Button btn = go.AddComponent<Button>(); btn.targetGraphic = bg;
        go.AddComponent<LayoutElement>().preferredWidth = 75f;
        MakeTMPChild(label, go.transform, 34f, FontStyles.Bold, TextWhite);
        return btn;
    }

    // ── Base helpers ─────────────────────────────────────────────────
    private static GameObject CreateUI(string n, Transform p, bool undo = false)
    {
        GameObject go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        if (undo) Undo.RegisterCreatedObjectUndo(go, "Create " + n);
        return go;
    }

    private static RectTransform Stretch(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static GameObject FindInScene(string name)
    {
        foreach (GameObject r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        { var f = FindChild(r.transform, name); if (f != null) return f; }
        return null;
    }

    private static GameObject FindChild(Transform p, string name)
    {
        if (p.name == name) return p.gameObject;
        for (int i = 0; i < p.childCount; i++)
        { var r = FindChild(p.GetChild(i), name); if (r != null) return r; }
        return null;
    }
}
