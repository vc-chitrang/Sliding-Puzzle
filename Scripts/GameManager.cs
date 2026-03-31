using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Transform gameTransform;
    [SerializeField] private Transform piecePrefab;
    [SerializeField] private Camera gameplayCamera;

    [Header("Board")]
    [SerializeField] private Vector2 boardWorldSize = new Vector2(6f, 6f);
    [SerializeField, Range(0f, 0.45f)] private float tileGapRatio = 0.08f;
    [SerializeField] private float tileMoveDuration = 0.14f;
    [SerializeField] private int shuffleMoveMultiplier = 3;
    [SerializeField] private string imagesFolderRelativeToAssets = "Games/Sliding-Puzzle/Textures";

    private readonly Dictionary<Vector2Int, TileController> _tilesByCell = new Dictionary<Vector2Int, TileController>();
    private readonly List<TileController> _spawnedTiles = new List<TileController>();
    private readonly List<Texture2D> _availableTextures = new List<Texture2D>();
    private readonly Dictionary<Vector2Int, ArrowController> _arrows = new Dictionary<Vector2Int, ArrowController>();

    private Vector2Int _size;
    private Vector2Int _emptyCell;
    private Texture2D _currentTexture;
    private Material _runtimeMaterial;
    private UIManager _uiManager;
    private Vector2 _normalizedBoardScale = Vector2.one;
    private bool _isAnimating;
    private bool _isSolved;
    private bool _timerRunning;
    private float _elapsedSeconds;
    private int _moveCount;

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    public Vector2Int GridSize => _size;
    public Vector2Int EmptyCell => _emptyCell;
    public Texture CurrentTexture => _currentTexture;
    public bool IsPreviewVisible => _uiManager != null && _uiManager.IsPreviewVisible;

    private void Awake()
    {
        if (gameTransform == null)
        {
            gameTransform = GameObject.Find("GameBoard")?.transform;
        }

        if (gameplayCamera == null)
        {
            gameplayCamera = Camera.main;
        }

        _uiManager = GetComponent<UIManager>();
        if (_uiManager == null)
        {
            _uiManager = gameObject.AddComponent<UIManager>();
        }

        _uiManager.Initialize(this);
        CreateRuntimeMaterial();
        CreateArrowButtons();
    }

    private void Start()
    {
        LoadAvailableTextures();
        Texture2D initialTexture = SelectInitialTexture();
        if (initialTexture == null)
        {
            Debug.LogError("Sliding Puzzle: No textures were found to build the puzzle.");
            return;
        }

        BuildPuzzle(initialTexture, true);
    }

    private void Update()
    {
        if (_timerRunning && !_isSolved)
        {
            _elapsedSeconds += Time.deltaTime;
            _uiManager.SetTimer(_elapsedSeconds);
        }

        if (_isAnimating || IsPreviewVisible)
        {
            return;
        }

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            TryMoveTileFromScreenPosition(Input.GetTouch(0).position);
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            TryMoveTileFromScreenPosition(Input.mousePosition);
        }
    }

    public void ResetPuzzle()
    {
        if (_currentTexture != null)
        {
            BuildPuzzle(_currentTexture, true);
        }
    }

    public void LoadRandomImage()
    {
        if (_availableTextures.Count == 0)
        {
            return;
        }

        Texture2D nextTexture = _currentTexture;
        if (_availableTextures.Count == 1)
        {
            nextTexture = _availableTextures[0];
        }
        else
        {
            while (nextTexture == _currentTexture)
            {
                nextTexture = _availableTextures[Random.Range(0, _availableTextures.Count)];
            }
        }

        BuildPuzzle(nextTexture, true);
    }

    public void TogglePreview()
    {
        bool shouldShowPreview = !_uiManager.IsPreviewVisible;
        _uiManager.SetPreview(shouldShowPreview, _currentTexture);
        UpdateArrowButtons();
    }

    public bool TryMoveFromArrow(Vector2Int directionFromEmpty)
    {
        return TryMoveTileAt(_emptyCell + directionFromEmpty, true);
    }

    public string GetCurrentImageLabel()
    {
        return _currentTexture == null ? "Sliding Puzzle" : NicifyLabel(_currentTexture.name);
    }

    public float GetHighScoreSeconds()
    {
        return PlayerPrefs.GetFloat(GetHighScoreKey(), -1f);
    }

    private void CreateRuntimeMaterial()
    {
        if (piecePrefab == null)
        {
            Debug.LogError("Sliding Puzzle: Piece prefab is not assigned.");
            return;
        }

        MeshRenderer prefabRenderer = piecePrefab.GetComponent<MeshRenderer>();
        if (prefabRenderer == null || prefabRenderer.sharedMaterial == null)
        {
            Debug.LogError("Sliding Puzzle: The piece prefab needs a MeshRenderer with a shared material.");
            return;
        }

        _runtimeMaterial = new Material(prefabRenderer.sharedMaterial);
        _runtimeMaterial.name = $"{prefabRenderer.sharedMaterial.name} Runtime";
    }

    private void CreateArrowButtons()
    {
        _arrows.Clear();
        CreateArrow(Vector2Int.up, "▲");
        CreateArrow(Vector2Int.down, "▼");
        CreateArrow(Vector2Int.left, "◀");
        CreateArrow(Vector2Int.right, "▶");
    }

    private void CreateArrow(Vector2Int direction, string label)
    {
        ArrowController arrow = ArrowController.Create(_uiManager.ArrowLayer, direction, label, TryMoveFromArrow);
        _arrows[direction] = arrow;
    }

    private void LoadAvailableTextures()
    {
        _availableTextures.Clear();
        string imageFolderPath = Path.Combine(Application.dataPath, imagesFolderRelativeToAssets);

        if (!Directory.Exists(imageFolderPath))
        {
            Debug.LogWarning($"Sliding Puzzle: Texture directory not found at {imageFolderPath}");
            return;
        }

        string[] files = Directory.GetFiles(imageFolderPath);
        for (int i = 0; i < files.Length; i++)
        {
            string extension = Path.GetExtension(files[i]).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
            {
                continue;
            }

            byte[] fileBytes = File.ReadAllBytes(files[i]);
            Texture2D loadedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!loadedTexture.LoadImage(fileBytes, false))
            {
                Destroy(loadedTexture);
                continue;
            }

            loadedTexture.name = Path.GetFileNameWithoutExtension(files[i]);
            loadedTexture.wrapMode = TextureWrapMode.Clamp;
            loadedTexture.filterMode = FilterMode.Bilinear;
            _availableTextures.Add(loadedTexture);
        }
    }

    private Texture2D SelectInitialTexture()
    {
        Texture2D materialTexture = _runtimeMaterial != null ? _runtimeMaterial.mainTexture as Texture2D : null;
        if (materialTexture != null)
        {
            string materialTextureName = materialTexture.name.ToLowerInvariant();
            for (int i = 0; i < _availableTextures.Count; i++)
            {
                if (_availableTextures[i].name.ToLowerInvariant() == materialTextureName)
                {
                    return _availableTextures[i];
                }
            }
        }

        if (_availableTextures.Count > 0)
        {
            return _availableTextures[0];
        }

        return materialTexture;
    }

    private void BuildPuzzle(Texture2D texture, bool shuffleBoard)
    {
        if (texture == null || gameTransform == null || piecePrefab == null || _runtimeMaterial == null)
        {
            return;
        }

        _currentTexture = texture;
        _runtimeMaterial.mainTexture = _currentTexture;
        _size = DetermineGridSize(_currentTexture.width, _currentTexture.height);
        ApplyBoardAspect(_currentTexture.width, _currentTexture.height);
        ClearBoard();
        SpawnBoard();

        if (shuffleBoard)
        {
            ShuffleBoard();
        }

        _isSolved = false;
        _timerRunning = false;
        _elapsedSeconds = 0f;
        _moveCount = 0;
        _uiManager.SetTitle(GetCurrentImageLabel());
        _uiManager.SetBestTime(GetHighScoreSeconds());
        _uiManager.SetTimer(_elapsedSeconds);
        _uiManager.SetStatus("Arrange the picture");
        _uiManager.SetPreview(false, _currentTexture);
        UpdateArrowButtons();
    }

    private Vector2Int DetermineGridSize(int width, int height)
    {
        if (width > height)
        {
            return new Vector2Int(4, 3);
        }

        if (height > width)
        {
            return new Vector2Int(3, 4);
        }

        return new Vector2Int(4, 4);
    }

    private void ApplyBoardAspect(int width, int height)
    {
        float aspect = height == 0 ? 1f : width / (float)height;

        if (width > height)
        {
            _normalizedBoardScale = new Vector2(1f, 1f / aspect);
        }
        else if (height > width)
        {
            _normalizedBoardScale = new Vector2(aspect, 1f);
        }
        else
        {
            _normalizedBoardScale = Vector2.one;
        }

        gameTransform.localScale = new Vector3(
            boardWorldSize.x * _normalizedBoardScale.x,
            boardWorldSize.y * _normalizedBoardScale.y,
            1f);
    }

    private void ClearBoard()
    {
        for (int i = 0; i < _spawnedTiles.Count; i++)
        {
            if (_spawnedTiles[i] != null)
            {
                Destroy(_spawnedTiles[i].gameObject);
            }
        }

        _spawnedTiles.Clear();
        _tilesByCell.Clear();
    }

    private void SpawnBoard()
    {
        _emptyCell = new Vector2Int(_size.x - 1, _size.y - 1);
        int tileIndex = 0;

        for (int y = 0; y < _size.y; y++)
        {
            for (int x = 0; x < _size.x; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (cell == _emptyCell)
                {
                    continue;
                }

                Transform tileTransform = Instantiate(piecePrefab, gameTransform);
                TileController tile = tileTransform.GetComponent<TileController>() ?? tileTransform.gameObject.AddComponent<TileController>();
                tile.Initialize(
                    tileIndex,
                    cell,
                    cell,
                    GetCellLocalPosition(cell),
                    GetTileLocalScale(),
                    BuildUvCoordinates(cell),
                    _runtimeMaterial);

                _spawnedTiles.Add(tile);
                _tilesByCell[cell] = tile;
                tileIndex++;
            }
        }
    }

    private Vector3 GetTileLocalScale()
    {
        float width = (1f / _size.x) * (1f - tileGapRatio);
        float height = (1f / _size.y) * (1f - tileGapRatio);
        return new Vector3(width, height, 1f);
    }

    private Vector3 GetCellLocalPosition(Vector2Int cell)
    {
        float tileWidth = 1f / _size.x;
        float tileHeight = 1f / _size.y;

        float x = -0.5f + (tileWidth * 0.5f) + (cell.x * tileWidth);
        float y = 0.5f - (tileHeight * 0.5f) - (cell.y * tileHeight);
        return new Vector3(x, y, 0f);
    }

    private Vector2[] BuildUvCoordinates(Vector2Int correctCell)
    {
        float uvWidth = 1f / _size.x;
        float uvHeight = 1f / _size.y;
        float paddingU = 0.0025f / _size.x;
        float paddingV = 0.0025f / _size.y;

        float minU = (correctCell.x * uvWidth) + paddingU;
        float maxU = ((correctCell.x + 1) * uvWidth) - paddingU;
        float maxV = 1f - (correctCell.y * uvHeight) - paddingV;
        float minV = 1f - ((correctCell.y + 1) * uvHeight) + paddingV;

        return new[]
        {
            new Vector2(minU, maxV),
            new Vector2(maxU, maxV),
            new Vector2(minU, minV),
            new Vector2(maxU, minV)
        };
    }

    private void TryMoveTileFromScreenPosition(Vector2 screenPosition)
    {
        if (gameplayCamera == null)
        {
            return;
        }

        Vector3 worldPoint = gameplayCamera.ScreenToWorldPoint(screenPosition);
        RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);
        if (!hit)
        {
            return;
        }

        TileController clickedTile = hit.transform.GetComponent<TileController>();
        if (clickedTile != null)
        {
            TryMoveTileAt(clickedTile.CurrentCell, true);
        }
    }

    private bool TryMoveTileAt(Vector2Int cell, bool playerMove)
    {
        if (_isAnimating || _isSolved || !_tilesByCell.TryGetValue(cell, out TileController tile))
        {
            return false;
        }

        if (!AreAdjacent(cell, _emptyCell))
        {
            return false;
        }

        StartCoroutine(MoveTileRoutine(tile, cell, playerMove));
        return true;
    }

    private System.Collections.IEnumerator MoveTileRoutine(TileController tile, Vector2Int sourceCell, bool playerMove)
    {
        _isAnimating = true;
        Vector2Int destinationCell = _emptyCell;

        _tilesByCell.Remove(sourceCell);
        _tilesByCell[destinationCell] = tile;
        _emptyCell = sourceCell;
        tile.SetCurrentCell(destinationCell);

        if (playerMove && !_timerRunning)
        {
            _timerRunning = true;
        }

        yield return tile.AnimateTo(GetCellLocalPosition(destinationCell), tileMoveDuration);

        if (playerMove)
        {
            _moveCount++;
        }

        _isSolved = CheckSolved();
        if (_isSolved)
        {
            _timerRunning = false;
            UpdateHighScore();
            _uiManager.SetStatus($"Solved in {_moveCount} moves");
        }
        else
        {
            _uiManager.SetStatus($"Moves: {_moveCount}");
        }

        UpdateArrowButtons();
        _isAnimating = false;
    }

    private void ShuffleBoard()
    {
        int shuffleMoves = Mathf.Max(12, _size.x * _size.y * shuffleMoveMultiplier);
        Vector2Int previousEmpty = new Vector2Int(-100, -100);

        for (int i = 0; i < shuffleMoves; i++)
        {
            List<Vector2Int> candidates = GetMovableCells(_emptyCell);
            if (candidates.Count > 1)
            {
                candidates.Remove(previousEmpty);
            }

            if (candidates.Count == 0)
            {
                continue;
            }

            Vector2Int chosenCell = candidates[Random.Range(0, candidates.Count)];
            previousEmpty = _emptyCell;
            MoveTileInstant(chosenCell);
        }

        if (CheckSolved())
        {
            ShuffleBoard();
        }
    }

    private void MoveTileInstant(Vector2Int sourceCell)
    {
        if (!_tilesByCell.TryGetValue(sourceCell, out TileController tile))
        {
            return;
        }

        Vector2Int destinationCell = _emptyCell;
        _tilesByCell.Remove(sourceCell);
        _tilesByCell[destinationCell] = tile;
        _emptyCell = sourceCell;
        tile.SetCurrentCell(destinationCell);
        tile.SnapTo(GetCellLocalPosition(destinationCell));
    }

    private List<Vector2Int> GetMovableCells(Vector2Int emptyCell)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        for (int i = 0; i < Directions.Length; i++)
        {
            Vector2Int candidate = emptyCell + Directions[i];
            if (IsInsideGrid(candidate) && _tilesByCell.ContainsKey(candidate))
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    private void UpdateArrowButtons()
    {
        if (_uiManager == null || gameplayCamera == null)
        {
            return;
        }

        Vector3 cellWorldPosition = gameTransform.TransformPoint(GetCellLocalPosition(_emptyCell));
        Vector3 screenPoint = gameplayCamera.WorldToScreenPoint(cellWorldPosition);

        for (int i = 0; i < Directions.Length; i++)
        {
            Vector2Int direction = Directions[i];
            Vector2Int candidate = _emptyCell + direction;
            bool isValid = !_isSolved && !_isAnimating && !IsPreviewVisible && IsInsideGrid(candidate) && _tilesByCell.ContainsKey(candidate);

            ArrowController arrow = _arrows[direction];
            arrow.SetVisible(isValid);
            if (isValid)
            {
                arrow.SetScreenPosition(screenPoint, GetArrowOffset(direction));
            }
        }
    }

    private Vector2 GetArrowOffset(Vector2Int direction)
    {
        const float distance = 92f;
        return new Vector2(direction.x * distance, direction.y * distance);
    }

    private bool CheckSolved()
    {
        for (int i = 0; i < _spawnedTiles.Count; i++)
        {
            if (!_spawnedTiles[i].IsInCorrectPosition)
            {
                return false;
            }
        }

        return true;
    }

    private void UpdateHighScore()
    {
        float existingBest = GetHighScoreSeconds();
        if (existingBest < 0f || _elapsedSeconds < existingBest)
        {
            PlayerPrefs.SetFloat(GetHighScoreKey(), _elapsedSeconds);
            PlayerPrefs.Save();
        }

        _uiManager.SetBestTime(GetHighScoreSeconds());
    }

    private string GetHighScoreKey()
    {
        string textureName = _currentTexture == null ? "default" : _currentTexture.name;
        return $"SlidingPuzzle.HighScore.{textureName}.{_size.x}x{_size.y}";
    }

    private static bool AreAdjacent(Vector2Int first, Vector2Int second)
    {
        return Mathf.Abs(first.x - second.x) + Mathf.Abs(first.y - second.y) == 1;
    }

    private bool IsInsideGrid(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < _size.x && cell.y >= 0 && cell.y < _size.y;
    }

    private static string NicifyLabel(string rawLabel)
    {
        if (string.IsNullOrWhiteSpace(rawLabel))
        {
            return "Sliding Puzzle";
        }

        string sanitized = rawLabel.Replace("_", " ").Replace("-", " ").Trim();
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(sanitized.ToLowerInvariant());
    }
}
