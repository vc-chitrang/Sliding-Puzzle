using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using ViitorCloud.Utility.PopupManager;

/// <summary>
/// Sliding puzzle manager — UI-based.
///
/// Tiles are <see cref="UnityEngine.UI.Image"/> children of a
/// <see cref="RectTransform"/> board panel.
/// Board is always square (1:1) and auto-scales to fit the screen.
///
/// Arrow buttons sit on the edges of the empty cell, indicating
/// which adjacent tile can be moved.  Tapping an arrow (or the tile
/// itself) slides the tile into the empty slot.
/// </summary>
public class GameManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────
    // Inspector fields
    // ─────────────────────────────────────────────────────────────────

    [Header("UI Board")]
    [Tooltip("RectTransform that holds the tiles (must be inside a Canvas).")]
    [SerializeField] private RectTransform boardPanel;

    [Tooltip("Padding factor (0..1) for board inside available space. 0.9 = 90 % of min dimension.")]
    [SerializeField, Range(0.5f, 1f)] private float boardPaddingFactor = 0.9f;

    [Header("Board Settings")]
    [SerializeField, Range(0f, 20f)] private float tileSpacing = 2f;
    [SerializeField] private float tileMoveDuration = 0.14f;
    [SerializeField] private int shuffleMoveMultiplier = 3;
    [SerializeField] private string imagesFolderRelativeToAssets = "Games/Sliding-Puzzle/Textures";

    [Header("Board Size Override")]
    [Tooltip("When true, board is always predefinedBoardSize regardless of image aspect.")]
    [SerializeField] private bool isBoardSizePredefined = true;
    [SerializeField] private Vector2Int predefinedBoardSize = new Vector2Int(3, 3);

    [Header("Arrow Buttons")]
    [Tooltip("Arrow size relative to cell size (0.3 = 30 %).")]
    [SerializeField, Range(0.2f, 0.6f)] private float arrowSizeFactor = 0.38f;

    [Header("Startup")]
    [Tooltip("When true, puzzle auto-starts from Textures folder. False = wait for cropped sprite.")]
    [SerializeField] private bool autoStartOnLoad;

    // ─────────────────────────────────────────────────────────────────
    // Runtime state
    // ─────────────────────────────────────────────────────────────────

    private readonly Dictionary<Vector2Int, TileController> _tilesByCell = new();
    private readonly List<TileController> _spawnedTiles = new();
    private readonly List<Texture2D> _availableTextures = new();
    private readonly Dictionary<Vector2Int, Vector2> _cellPositions = new();

    private Vector2Int _size;
    private Vector2Int _emptyCell;
    private Texture2D _currentTexture;
    private UIManager _uiManager;
    private GridLayoutGroup _gridLayout;
    private bool _isAnimating;
    private bool _isSolved;
    private bool _timerRunning;
    private float _elapsedSeconds;
    private int _moveCount;
    private float _cellSize;

    // Launch mode — true until the first tile/arrow click or a cropped sprite is loaded
    private bool _isLaunchMode = true;

    // ── Auto-shuffle (launch mode only) ──────────────────────────────
    private Coroutine  _autoShuffleCoroutine;
    private Vector2Int _lastAutoMoveFrom = new Vector2Int(-999, -999);

    // ── Arrow system ─────────────────────────────────────────────────
    // Each direction = offset from empty cell to the adjacent tile
    // that the arrow can pull in.
    private ArrowController[] _arrowControllers;

    private static readonly Vector2Int[] ArrowGridDirections =
    {
        new Vector2Int(0, -1),  // tile visually ABOVE  (grid y-1)
        new Vector2Int(0, 1),   // tile visually BELOW  (grid y+1)
        new Vector2Int(-1, 0),  // tile to the LEFT
        new Vector2Int(1, 0),   // tile to the RIGHT
    };

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    // ─────────────────────────────────────────────────────────────────
    // Public properties
    // ─────────────────────────────────────────────────────────────────

    public Vector2Int GridSize => _size;
    public Vector2Int EmptyCell => _emptyCell;
    public Texture CurrentTexture => _currentTexture;
    public bool IsPreviewVisible => _uiManager != null && _uiManager.IsPreviewVisible;

    // ─────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _uiManager = GetComponent<UIManager>();
        if (_uiManager == null)
            _uiManager = gameObject.AddComponent<UIManager>();

        _uiManager.Initialize(this);
        _uiManager.EnterLaunchMode();   // start in launch mode

        // Ensure board panel has a GridLayoutGroup
        if (boardPanel != null)
        {
            _gridLayout = boardPanel.GetComponent<GridLayoutGroup>();
            if (_gridLayout == null)
                _gridLayout = boardPanel.gameObject.AddComponent<GridLayoutGroup>();
        }
    }

    private void OnEnable()
    {
        APIHandler.OnAPIDataFetchedEvent.AddListener(OnAPIDataForLaunch);
    }

    private void OnDisable()
    {
        APIHandler.OnAPIDataFetchedEvent.RemoveListener(OnAPIDataForLaunch);
    }

    private void Start()
    {
        LoadAvailableTextures();

        // Use API data for the launch preview if already available
        if (APIHandler.LastFetchedData != null)
        {
            OnAPIDataForLaunch(APIHandler.LastFetchedData);
            return;
        }

        // Fallback: local textures (autoStartOnLoad)
        if (!autoStartOnLoad) return;

        Texture2D tex = SelectInitialTexture();
        if (tex == null)
        {
            Debug.LogWarning("Sliding Puzzle: No local textures found for launch preview.");
            return;
        }

        BuildPuzzle(tex, true);
    }

    // ─────────────────────────────────────────────────────────────────
    // Launch-mode image loading from API
    // ─────────────────────────────────────────────────────────────────

    private void OnAPIDataForLaunch(MAPData data)
    {
        // Only load once per launch mode session and only if no tiles exist yet
        if (!_isLaunchMode || _spawnedTiles.Count > 0) return;
        if (data?.results?.data == null || data.results.data.Count == 0) return;

        var valid = data.results.data.FindAll(d => !string.IsNullOrEmpty(d.primary_image));
        if (valid.Count == 0) return;

        var chosen = valid[Random.Range(0, valid.Count)];
        StartCoroutine(LoadLaunchImageRoutine(chosen.primary_image));
    }

    private IEnumerator LoadLaunchImageRoutine(string url)
    {
        PopupManager.Instance.ShowLoading();

        using var req = UnityWebRequestTexture.GetTexture(url);
        yield return req.SendWebRequest();

        PopupManager.Instance.HideLoading();

        if (!_isLaunchMode) yield break;   // user already interacted — skip
        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[GameManager] Launch image load failed: {req.error}");
            yield break;
        }

        Texture2D tex = DownloadHandlerTexture.GetContent(req);
        if (tex == null) yield break;

        tex.name       = "LaunchPreview";
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        // Crop to 1:1 for the square board
        if (tex.width != tex.height)
            tex = CropToSquare(tex);

        BuildPuzzle(tex, true);
    }

    private void Update()
    {
        if (_timerRunning && !_isSolved)
        {
            _elapsedSeconds += Time.deltaTime;
            _uiManager.SetTimer(_elapsedSeconds);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────────
    // Launch / Gameplay mode
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by UIManager tile/arrow handlers on first interaction.
    /// Transitions the screen from "launch" to "gameplay" mode.
    /// </summary>
    public void ExitLaunchMode()
    {
        if (!_isLaunchMode) return;
        _isLaunchMode = false;
        StopAutoShuffle();
        _uiManager?.ExitLaunchMode();
    }

    // ─────────────────────────────────────────────────────────────────
    // Auto-shuffle (launch mode)
    // ─────────────────────────────────────────────────────────────────

    private void StartAutoShuffle()
    {
        StopAutoShuffle();
        _autoShuffleCoroutine = StartCoroutine(AutoShuffleRoutine());
    }

    private void StopAutoShuffle()
    {
        if (_autoShuffleCoroutine == null) return;
        StopCoroutine(_autoShuffleCoroutine);
        _autoShuffleCoroutine = null;
    }

    private IEnumerator AutoShuffleRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(1f);

        while (_isLaunchMode)
        {
            yield return wait;

            // Skip when board is not visible (e.g. user navigated to Browse screen)
            if (!boardPanel.gameObject.activeInHierarchy) continue;

            if (_isAnimating || _spawnedTiles.Count == 0) continue;

            List<Vector2Int> candidates = GetMovableCells(_emptyCell);
            // Avoid immediately reversing the last move
            if (candidates.Count > 1) candidates.Remove(_lastAutoMoveFrom);
            if (candidates.Count == 0) continue;

            Vector2Int chosen = candidates[Random.Range(0, candidates.Count)];
            _lastAutoMoveFrom = _emptyCell;

            if (_tilesByCell.TryGetValue(chosen, out TileController tile))
                StartCoroutine(AutoShuffleMoveRoutine(tile, chosen));
        }
    }

    /// <summary>
    /// Animates a single tile move for the auto-shuffle preview.
    /// Does NOT start the timer, count moves, or check for a solved state.
    /// </summary>
    private IEnumerator AutoShuffleMoveRoutine(TileController tile, Vector2Int sourceCell)
    {
        _isAnimating = true;
        HideAllArrows();

        Vector2Int destCell = _emptyCell;
        _tilesByCell.Remove(sourceCell);
        _tilesByCell[destCell] = tile;
        _emptyCell = sourceCell;
        tile.SetCurrentCell(destCell);

        yield return tile.AnimateTo(GetCellAnchoredPosition(destCell), tileMoveDuration);

        // Reposition arrows at the new empty cell (for visual consistency)
        UpdateArrows();
        _isAnimating = false;
    }

    /// <summary>
    /// Called by the "New Image" button.
    /// Reloads a random API image and returns to launch mode.
    /// </summary>
    public void ResetToLaunchMode()
    {
        _isLaunchMode = true;
        _uiManager?.EnterLaunchMode();

        // Reload from API
        if (APIHandler.LastFetchedData != null)
        {
            // Force reload even when tiles already exist
            var data  = APIHandler.LastFetchedData;
            var valid = data.results?.data?.FindAll(d => !string.IsNullOrEmpty(d.primary_image));
            if (valid != null && valid.Count > 0)
            {
                var chosen = valid[Random.Range(0, valid.Count)];
                StartCoroutine(LoadLaunchImageRoutine(chosen.primary_image));
                return;
            }
        }

        // Fallback: local textures
        LoadRandomImage();
    }

    // ─────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Start puzzle from a cropped sprite (called by ArtworkFocusScreen).</summary>
    public void StartPuzzleWithCroppedSprite(Sprite croppedSprite)
    {
        if (croppedSprite == null || croppedSprite.texture == null)
        {
            Debug.LogError("GameManager: Cropped sprite is null.");
            return;
        }

        // Entering gameplay from artwork selection — exit launch mode first
        ExitLaunchMode();

        Texture2D tex = croppedSprite.texture;
        tex.name = "CroppedImage";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        BuildPuzzle(tex, true);
    }

    public void ResetPuzzle()
    {
        if (_currentTexture != null)
            BuildPuzzle(_currentTexture, true);
    }

    public void LoadRandomImage()
    {
        if (_availableTextures.Count == 0) return;

        Texture2D next = _currentTexture;
        if (_availableTextures.Count == 1)
        {
            next = _availableTextures[0];
        }
        else
        {
            while (next == _currentTexture)
                next = _availableTextures[Random.Range(0, _availableTextures.Count)];
        }

        BuildPuzzle(next, true);
    }

    public void TogglePreview()
    {
        bool show = !_uiManager.IsPreviewVisible;
        _uiManager.SetPreview(show, _currentTexture);
    }

    public string GetCurrentImageLabel()
    {
        return _currentTexture == null ? "Sliding Puzzle" : NicifyLabel(_currentTexture.name);
    }

    public float GetHighScoreSeconds()
    {
        return PlayerPrefs.GetFloat(GetHighScoreKey(), -1f);
    }

    // ─────────────────────────────────────────────────────────────────
    // Board building
    // ─────────────────────────────────────────────────────────────────

    private void BuildPuzzle(Texture2D texture, bool shuffle)
    {
        if (texture == null || boardPanel == null)
        {
            Debug.LogError("GameManager: texture or boardPanel is null.");
            return;
        }

        _currentTexture = texture;
        _size = DetermineGridSize(texture.width, texture.height);

        // ── CRITICAL: force Canvas layout so parent rects are valid ──
        // (GamePlayScreen may have just been activated this frame)
        Canvas.ForceUpdateCanvases();

        // ── Size the board panel to a perfect square ─────────────────
        SizeBoardPanel();

        // ── Match PreviewPanel to board size & ensure it renders in front ─
        _uiManager.MatchPreviewToBoard(boardPanel);

        // ── Configure GridLayoutGroup ────────────────────────────────
        ConfigureGridLayout();

        // ── Clear old board & arrows ─────────────────────────────────
        ClearBoard();

        // Create readable texture via GPU blit (source may not be R/W)
        Texture2D readableTex = CreateReadableTexture(texture);

        // ── Spawn tiles ──────────────────────────────────────────────
        SpawnBoard(readableTex);

        // Disable GridLayoutGroup so tiles can be animated freely
        _gridLayout.enabled = false;

        // ── Create directional arrows on top of tiles ────────────────
        CreateArrows();

        if (shuffle) ShuffleBoard();

        // ── Position arrows at the empty cell edges ──────────────────
        UpdateArrows();

        // ── Reset game state ─────────────────────────────────────────
        _isSolved = false;
        _timerRunning = false;
        _elapsedSeconds = 0f;
        _moveCount = 0;

        _uiManager.SetTitle(GetCurrentImageLabel());
        _uiManager.SetBestTime(GetHighScoreSeconds());
        _uiManager.SetTimer(0f);
        _uiManager.SetStatus("Arrange the picture");
        _uiManager.SetPreview(false, _currentTexture);

        // Auto-shuffle tiles every second while in launch mode
        if (_isLaunchMode)
        {
            _lastAutoMoveFrom = new Vector2Int(-999, -999);
            StartAutoShuffle();
        }
    }

    /// <summary>
    /// Makes the board panel a perfect 1:1 square that fits the screen.
    /// </summary>
    private void SizeBoardPanel()
    {
        RectTransform parent = boardPanel.parent as RectTransform;
        if (parent == null) return;

        float parentW = parent.rect.width;
        float parentH = parent.rect.height;

        // Safety: if parent hasn't laid out yet, defer to next frame
        if (parentW <= 0f || parentH <= 0f)
        {
            Debug.LogWarning("GameManager: Parent rect not ready yet. Using screen size fallback.");
            parentW = Screen.width;
            parentH = Screen.height;
        }

        float size = Mathf.Min(parentW, parentH) * boardPaddingFactor;

        boardPanel.anchorMin = new Vector2(0.5f, 0.5f);
        boardPanel.anchorMax = new Vector2(0.5f, 0.5f);
        boardPanel.pivot = new Vector2(0.5f, 0.5f);
        boardPanel.sizeDelta = new Vector2(size, size);
        boardPanel.anchoredPosition = Vector2.zero;
    }

    private void ConfigureGridLayout()
    {
        if (_gridLayout == null) return;

        _gridLayout.enabled = true;

        float boardSize = boardPanel.rect.width;

        // Fallback if rect is still zero (shouldn't happen after ForceUpdateCanvases)
        if (boardSize <= 0f)
            boardSize = boardPanel.sizeDelta.x;

        float totalSpacing = tileSpacing * (_size.x - 1);
        _cellSize = (boardSize - totalSpacing) / _size.x;

        _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _gridLayout.constraintCount = _size.x;
        _gridLayout.cellSize = new Vector2(_cellSize, _cellSize);
        _gridLayout.spacing = new Vector2(tileSpacing, tileSpacing);
        _gridLayout.childAlignment = TextAnchor.UpperLeft;
        _gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        _gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        _gridLayout.padding = new RectOffset(0, 0, 0, 0);
    }

    private void SpawnBoard(Texture2D readableTex)
    {
        _emptyCell = new Vector2Int(_size.x - 1, _size.y - 1);
        int tileIndex = 0;

        // Pre-create all sprites from the texture
        Sprite[,] sprites = SliceTextureIntoSprites(readableTex);

        for (int y = 0; y < _size.y; y++)
        {
            for (int x = 0; x < _size.x; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);

                if (cell == _emptyCell)
                {
                    // Create invisible placeholder so GridLayout stays consistent
                    CreateEmptyPlaceholder();
                    continue;
                }

                TileController tile = CreateTile(tileIndex, cell, sprites[x, y]);
                _spawnedTiles.Add(tile);
                _tilesByCell[cell] = tile;
                tileIndex++;
            }
        }

        // Force layout rebuild so tiles get positioned by GridLayout
        LayoutRebuilder.ForceRebuildLayoutImmediate(boardPanel);

        // Capture the anchored positions set by GridLayout for animation use
        CacheGridPositions();
    }

    /// <summary>
    /// Slices a texture into NxM sprites for each grid cell.
    /// Uses integer pixel boundaries to avoid floating-point precision issues
    /// that can produce out-of-bounds rects (e.g. texY = -0.00002 for the
    /// last row when tex.height isn't evenly divisible by _size.y).
    /// </summary>
    private Sprite[,] SliceTextureIntoSprites(Texture2D tex)
    {
        Sprite[,] sprites = new Sprite[_size.x, _size.y];

        for (int y = 0; y < _size.y; y++)
        {
            for (int x = 0; x < _size.x; x++)
            {
                // Integer pixel boundaries (column)
                int left = Mathf.RoundToInt((float)x / _size.x * tex.width);
                int right = Mathf.RoundToInt((float)(x + 1) / _size.x * tex.width);

                // Integer pixel boundaries (row — texture Y is bottom-up, grid Y is top-down)
                int bottom = Mathf.RoundToInt((float)(_size.y - 1 - y) / _size.y * tex.height);
                int top = Mathf.RoundToInt((float)(_size.y - y) / _size.y * tex.height);

                // Clamp to texture bounds for absolute safety
                left = Mathf.Clamp(left, 0, tex.width);
                right = Mathf.Clamp(right, left + 1, tex.width);
                bottom = Mathf.Clamp(bottom, 0, tex.height);
                top = Mathf.Clamp(top, bottom + 1, tex.height);

                Rect rect = new Rect(left, bottom, right - left, top - bottom);
                sprites[x, y] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
                sprites[x, y].name = $"TileSprite_{x}_{y}";
            }
        }

        return sprites;
    }

    private TileController CreateTile(int index, Vector2Int cell, Sprite sprite)
    {
        GameObject go = new GameObject(
            $"Tile_{index:00}",
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Button));

        go.transform.SetParent(boardPanel, false);
        go.layer = boardPanel.gameObject.layer;

        TileController tile = go.AddComponent<TileController>();
        tile.Initialize(index, cell, cell, sprite);
        tile.OnTileClicked += OnTileClicked;

        return tile;
    }

    private void CreateEmptyPlaceholder()
    {
        GameObject go = new GameObject("EmptySlot", typeof(RectTransform));
        go.transform.SetParent(boardPanel, false);
        go.layer = boardPanel.gameObject.layer;
    }

    // ── Cached positions for animation after GridLayout is disabled ──

    private void CacheGridPositions()
    {
        _cellPositions.Clear();
        int childIndex = 0;

        for (int y = 0; y < _size.y; y++)
        {
            for (int x = 0; x < _size.x; x++)
            {
                if (childIndex < boardPanel.childCount)
                {
                    RectTransform rt = boardPanel.GetChild(childIndex) as RectTransform;
                    if (rt != null)
                    {
                        _cellPositions[new Vector2Int(x, y)] = rt.anchoredPosition;
                    }
                }

                childIndex++;
            }
        }
    }

    private Vector2 GetCellAnchoredPosition(Vector2Int cell)
    {
        return _cellPositions.TryGetValue(cell, out Vector2 pos) ? pos : Vector2.zero;
    }

    // ─────────────────────────────────────────────────────────────────
    // Arrow system — directional buttons on empty-cell edges
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates 4 arrow buttons as children of boardPanel.
    /// They use LayoutElement.ignoreLayout so GridLayout won't touch them,
    /// and render on top of tiles because they have higher sibling indices.
    /// </summary>
    private void CreateArrows()
    {
        DestroyArrows();
        _arrowControllers = new ArrowController[ArrowGridDirections.Length];

        for (int i = 0; i < ArrowGridDirections.Length; i++)
        {
            Vector2Int dir = ArrowGridDirections[i];

            ArrowController arrow = ArrowController.Create(
                boardPanel, dir, "", OnArrowPressed);

            // Ignore GridLayout positioning
            LayoutElement le = arrow.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            // Override anchors to upper-left (matching what GridLayout sets
            // on tile children) so anchoredPosition coordinates are consistent.
            RectTransform rt = arrow.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);

            // Ensure arrows render on top of all tiles
            rt.SetAsLastSibling();

            _arrowControllers[i] = arrow;
            arrow.SetVisible(false);
        }
    }

    /// <summary>
    /// Positions and shows/hides each arrow based on the current empty cell.
    /// Each arrow sits at the EDGE between the empty cell and its adjacent tile.
    /// </summary>
    private void UpdateArrows()
    {
        if (_arrowControllers == null) return;

        Vector2 emptyPos = GetCellAnchoredPosition(_emptyCell);
        float half = (_cellSize + tileSpacing) / 2f;
        float arrowSize = _cellSize * arrowSizeFactor;

        for (int i = 0; i < _arrowControllers.Length; i++)
        {
            if (_arrowControllers[i] == null) continue;

            Vector2Int dir = ArrowGridDirections[i];
            Vector2Int neighborCell = _emptyCell + dir;

            bool show = IsInsideGrid(neighborCell) && _tilesByCell.ContainsKey(neighborCell);
            _arrowControllers[i].SetVisible(show);

            if (show)
            {
                // Map grid direction to screen-space offset.
                // Grid x+ = screen x+,  grid y+ = screen y- (top-down grid).
                Vector2 offset = new Vector2(dir.x * half, -dir.y * half);
                _arrowControllers[i].SetLayout(emptyPos + offset, arrowSize);
            }
        }
    }

    private void HideAllArrows()
    {
        if (_arrowControllers == null) return;
        for (int i = 0; i < _arrowControllers.Length; i++)
        {
            if (_arrowControllers[i] != null)
                _arrowControllers[i].SetVisible(false);
        }
    }

    private void DestroyArrows()
    {
        if (_arrowControllers == null) return;
        for (int i = 0; i < _arrowControllers.Length; i++)
        {
            if (_arrowControllers[i] != null)
                Destroy(_arrowControllers[i].gameObject);
            _arrowControllers[i] = null;
        }

        _arrowControllers = null;
    }

    /// <summary>
    /// Called when any arrow button is pressed.
    /// Direction = grid offset from empty cell to the tile to move.
    /// </summary>
    private bool OnArrowPressed(Vector2Int gridDirection)
    {
        ExitLaunchMode();   // first click switches to gameplay mode
        if (_isAnimating || _isSolved || IsPreviewVisible)
            return false;

        Vector2Int tileCell = _emptyCell + gridDirection;
        return TryMoveTileAt(tileCell, true);
    }

    // ─────────────────────────────────────────────────────────────────
    // Tile click handler (secondary input — tap tile directly)
    // ─────────────────────────────────────────────────────────────────

    private void OnTileClicked(TileController tile)
    {
        ExitLaunchMode();   // first click switches to gameplay mode
        if (_isAnimating || _isSolved || IsPreviewVisible)
            return;

        TryMoveTileAt(tile.CurrentCell, true);
    }

    // ─────────────────────────────────────────────────────────────────
    // Movement
    // ─────────────────────────────────────────────────────────────────

    private bool TryMoveTileAt(Vector2Int cell, bool playerMove)
    {
        if (_isAnimating || _isSolved || !_tilesByCell.TryGetValue(cell, out TileController tile))
            return false;

        if (!AreAdjacent(cell, _emptyCell))
            return false;

        StartCoroutine(MoveTileRoutine(tile, cell, playerMove));
        return true;
    }

    private IEnumerator MoveTileRoutine(TileController tile, Vector2Int sourceCell, bool playerMove)
    {
        _isAnimating = true;

        // Hide arrows during animation for cleaner visuals
        HideAllArrows();

        Vector2Int destCell = _emptyCell;

        _tilesByCell.Remove(sourceCell);
        _tilesByCell[destCell] = tile;
        _emptyCell = sourceCell;
        tile.SetCurrentCell(destCell);

        if (playerMove && !_timerRunning)
            _timerRunning = true;

        yield return tile.AnimateTo(GetCellAnchoredPosition(destCell), tileMoveDuration);

        if (playerMove) _moveCount++;

        _isSolved = CheckSolved();
        if (_isSolved)
        {
            _timerRunning = false;
            UpdateHighScore();
            _uiManager.SetStatus($"Solved in {_moveCount} moves");
            // Keep arrows hidden on win
        }
        else
        {
            _uiManager.SetStatus($"Moves: {_moveCount}");
            // Reposition arrows at new empty cell
            UpdateArrows();
        }

        _isAnimating = false;
    }

    // ─────────────────────────────────────────────────────────────────
    // Shuffle
    // ─────────────────────────────────────────────────────────────────

    private void ShuffleBoard()
    {
        int moves = Mathf.Max(12, _size.x * _size.y * shuffleMoveMultiplier);
        Vector2Int prevEmpty = new Vector2Int(-100, -100);

        for (int i = 0; i < moves; i++)
        {
            List<Vector2Int> candidates = GetMovableCells(_emptyCell);
            if (candidates.Count > 1)
                candidates.Remove(prevEmpty);
            if (candidates.Count == 0)
                continue;

            Vector2Int chosen = candidates[Random.Range(0, candidates.Count)];
            prevEmpty = _emptyCell;
            MoveTileInstant(chosen);
        }

        if (CheckSolved()) ShuffleBoard();
    }

    private void MoveTileInstant(Vector2Int sourceCell)
    {
        if (!_tilesByCell.TryGetValue(sourceCell, out TileController tile))
            return;

        Vector2Int destCell = _emptyCell;
        _tilesByCell.Remove(sourceCell);
        _tilesByCell[destCell] = tile;
        _emptyCell = sourceCell;
        tile.SetCurrentCell(destCell);
        tile.SnapTo(GetCellAnchoredPosition(destCell));
    }

    // ─────────────────────────────────────────────────────────────────
    // Grid size
    // ─────────────────────────────────────────────────────────────────

    private Vector2Int DetermineGridSize(int width, int height)
    {
        if (isBoardSizePredefined) return predefinedBoardSize;

        if (width > height) return new Vector2Int(4, 3);
        if (height > width) return new Vector2Int(3, 4);
        return new Vector2Int(4, 4);
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private void ClearBoard()
    {
        // Destroy arrows first (they're also children of boardPanel)
        DestroyArrows();

        // Unsubscribe click events
        foreach (TileController tile in _spawnedTiles)
        {
            if (tile != null)
                tile.OnTileClicked -= OnTileClicked;
        }

        // Remove all children from boardPanel IMMEDIATELY (not end-of-frame)
        // so that GridLayout only sees newly-spawned children.
        for (int i = boardPanel.childCount - 1; i >= 0; i--)
        {
            GameObject child = boardPanel.GetChild(i).gameObject;
            child.transform.SetParent(null);  // un-parent now
            Destroy(child);                   // destroy end-of-frame
        }

        _spawnedTiles.Clear();
        _tilesByCell.Clear();
        _cellPositions.Clear();
    }

    private List<Vector2Int> GetMovableCells(Vector2Int emptyCell)
    {
        List<Vector2Int> result = new();
        for (int i = 0; i < Directions.Length; i++)
        {
            Vector2Int c = emptyCell + Directions[i];
            if (IsInsideGrid(c) && _tilesByCell.ContainsKey(c))
                result.Add(c);
        }

        return result;
    }

    private bool CheckSolved()
    {
        for (int i = 0; i < _spawnedTiles.Count; i++)
        {
            if (!_spawnedTiles[i].IsInCorrectPosition)
                return false;
        }

        return true;
    }

    private static bool AreAdjacent(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
    }

    private bool IsInsideGrid(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < _size.x && cell.y >= 0 && cell.y < _size.y;
    }

    // ─────────────────────────────────────────────────────────────────
    // Texture helpers
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Center-crops a texture to a 1:1 square via GPU blit.
    /// </summary>
    private static Texture2D CropToSquare(Texture2D source)
    {
        int size = Mathf.Min(source.width, source.height);
        int offsetX = (source.width - size) / 2;
        int offsetY = (source.height - size) / 2;

        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, rt);

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D cropped = new Texture2D(size, size, TextureFormat.RGBA32, false);
        cropped.ReadPixels(new Rect(offsetX, offsetY, size, size), 0, 0);
        cropped.Apply();

        cropped.name       = source.name;
        cropped.wrapMode   = TextureWrapMode.Clamp;
        cropped.filterMode = FilterMode.Bilinear;

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return cropped;
    }

    /// <summary>
    /// Creates a readable Texture2D via GPU blit (handles non-R/W textures).
    /// </summary>
    private static Texture2D CreateReadableTexture(Texture2D source)
    {
        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, rt);

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        readable.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return readable;
    }

    private void LoadAvailableTextures()
    {
        _availableTextures.Clear();
        string folder = Path.Combine(Application.dataPath, imagesFolderRelativeToAssets);
        if (!Directory.Exists(folder)) return;

        foreach (string file in Directory.GetFiles(folder))
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") continue;

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(file), false))
            {
                Destroy(tex);
                continue;
            }

            tex.name = Path.GetFileNameWithoutExtension(file);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            _availableTextures.Add(tex);
        }
    }

    private Texture2D SelectInitialTexture()
    {
        return _availableTextures.Count > 0 ? _availableTextures[0] : null;
    }

    private void UpdateHighScore()
    {
        float best = GetHighScoreSeconds();
        if (best < 0f || _elapsedSeconds < best)
        {
            PlayerPrefs.SetFloat(GetHighScoreKey(), _elapsedSeconds);
            PlayerPrefs.Save();
        }

        _uiManager.SetBestTime(GetHighScoreSeconds());
    }

    private string GetHighScoreKey()
    {
        string name = _currentTexture == null ? "default" : _currentTexture.name;
        return $"SlidingPuzzle.HighScore.{name}.{_size.x}x{_size.y}";
    }

    private static string NicifyLabel(string rawLabel)
    {
        if (string.IsNullOrWhiteSpace(rawLabel)) return "Sliding Puzzle";
        string s = rawLabel.Replace("_", " ").Replace("-", " ").Trim();
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
    }
}
