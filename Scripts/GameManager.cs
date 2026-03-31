using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour {
  [SerializeField] private Transform gameTransform;
  [SerializeField] private Transform piecePrefab;
  [SerializeField] private float gapThickness = 0.01f;

  private readonly List<Transform> pieces = new List<Transform>();
  private Vector3 initialBoardScale;
  private int emptyLocation;
  private Vector2Int size;
  private bool shuffling = false;

  private void Awake() {
    if (gameTransform != null) {
      initialBoardScale = gameTransform.localScale;
    }
  }

  private void Start() {
    pieces.Clear();
    ConfigureBoardFromTexture();
    CreateGamePieces(gapThickness);
  }

  private void Update() {
    if (!shuffling && CheckCompletion()) {
      shuffling = true;
      StartCoroutine(WaitShuffle(0.5f));
    }

    if (Input.GetMouseButtonDown(0)) {
      RaycastHit2D hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
      if (hit) {
        for (int i = 0; i < pieces.Count; i++) {
          if (pieces[i] == hit.transform) {
            if (SwapIfValid(i, -size.x, -1)) { break; }
            if (SwapIfValid(i, +size.x, -1)) { break; }
            if (SwapIfValid(i, -1, 0)) { break; }
            if (SwapIfValid(i, +1, size.x - 1)) { break; }
          }
        }
      }
    }
  }

  private void ConfigureBoardFromTexture() {
    Texture texture = GetPuzzleTexture();
    if ((texture == null) || (texture.width <= 0) || (texture.height <= 0)) {
      size = new Vector2Int(4, 4);
      ApplyBoardScale(1f);
      Debug.LogWarning("GameManager could not read a valid main texture. Falling back to a 4x4 square puzzle.");
      return;
    }

    size = DetermineGridSize(texture.width, texture.height);
    ApplyBoardScale((float)texture.width / texture.height);
    Debug.Log($"Resolution: {texture.width}x{texture.height}, Grid: {size}");
  }

  private Texture GetPuzzleTexture() {
    if (piecePrefab == null) {
      return null;
    }

    MeshRenderer meshRenderer = piecePrefab.GetComponent<MeshRenderer>();
    if ((meshRenderer == null) || (meshRenderer.sharedMaterial == null)) {
      return null;
    }

    return meshRenderer.sharedMaterial.mainTexture;
  }

  private Vector2Int DetermineGridSize(int width, int height) {
    if (width > height) {
      return new Vector2Int(4, 3);
    }

    if (height > width) {
      return new Vector2Int(3, 4);
    }

    return new Vector2Int(4, 4);
  }

  private void ApplyBoardScale(float aspect) {
    if (gameTransform == null) {
      return;
    }

    if (initialBoardScale == Vector3.zero) {
      initialBoardScale = gameTransform.localScale;
    }

    float baseScale = Mathf.Max(initialBoardScale.x, initialBoardScale.y);
    Vector3 scaledBoard = initialBoardScale;

    if (aspect > 1f) {
      scaledBoard.x = baseScale;
      scaledBoard.y = baseScale / aspect;
    } else if (aspect < 1f) {
      scaledBoard.x = baseScale * aspect;
      scaledBoard.y = baseScale;
    } else {
      scaledBoard.x = baseScale;
      scaledBoard.y = baseScale;
    }

    gameTransform.localScale = scaledBoard;
  }

  // Create the game setup using the current rectangular grid.
  private void CreateGamePieces(float gap) {
    float tileWidth = 1f / size.x;
    float tileHeight = 1f / size.y;
    float uvWidth = 1f / size.x;
    float uvHeight = 1f / size.y;
    float uvGapX = Mathf.Min(uvWidth * 0.45f, gap * 0.5f);
    float uvGapY = Mathf.Min(uvHeight * 0.45f, gap * 0.5f);

    for (int row = 0; row < size.y; row++) {
      for (int col = 0; col < size.x; col++) {
        int index = GetIndex(row, col);
        Transform piece = Instantiate(piecePrefab, gameTransform);
        pieces.Add(piece);

        piece.localPosition = new Vector3(
          -1f + (2f * tileWidth * col) + tileWidth,
          +1f - (2f * tileHeight * row) - tileHeight,
          0f
        );

        piece.localScale = new Vector3(
          Mathf.Max(0.01f, (2f * tileWidth) - gap),
          Mathf.Max(0.01f, (2f * tileHeight) - gap),
          1f
        );

        piece.name = index.ToString();

        if ((row == size.y - 1) && (col == size.x - 1)) {
          emptyLocation = index;
          piece.gameObject.SetActive(false);
        } else {
          Mesh mesh = piece.GetComponent<MeshFilter>().mesh;
          Vector2[] uv = new Vector2[4];

          uv[0] = new Vector2((uvWidth * col) + uvGapX, 1f - ((uvHeight * (row + 1)) - uvGapY));
          uv[1] = new Vector2((uvWidth * (col + 1)) - uvGapX, 1f - ((uvHeight * (row + 1)) - uvGapY));
          uv[2] = new Vector2((uvWidth * col) + uvGapX, 1f - ((uvHeight * row) + uvGapY));
          uv[3] = new Vector2((uvWidth * (col + 1)) - uvGapX, 1f - ((uvHeight * row) + uvGapY));

          mesh.uv = uv;
        }
      }
    }
  }

  private int GetIndex(int row, int col) {
    return (row * size.x) + col;
  }

  // blockedColumn stops horizontal moves from wrapping into the next row.
  private bool SwapIfValid(int index, int offset, int blockedColumn) {
    if ((blockedColumn >= 0) && ((index % size.x) == blockedColumn)) {
      return false;
    }

    int targetIndex = index + offset;
    if ((targetIndex < 0) || (targetIndex >= pieces.Count) || (targetIndex != emptyLocation)) {
      return false;
    }

    (pieces[index], pieces[targetIndex]) = (pieces[targetIndex], pieces[index]);
    (pieces[index].localPosition, pieces[targetIndex].localPosition) = (pieces[targetIndex].localPosition, pieces[index].localPosition);
    emptyLocation = index;
    return true;
  }

  private bool CheckCompletion() {
    for (int i = 0; i < pieces.Count; i++) {
      if (pieces[i].name != i.ToString()) {
        return false;
      }
    }

    return true;
  }

  private IEnumerator WaitShuffle(float duration) {
    yield return new WaitForSeconds(duration);
    Shuffle();
    shuffling = false;
  }

  // Brute force shuffling using only legal moves keeps the board reachable.
  private void Shuffle() {
    int count = 0;
    int last = emptyLocation;
    int shuffleTarget = pieces.Count * Mathf.Max(size.x, size.y);

    while (count < shuffleTarget) {
      int rnd = Random.Range(0, pieces.Count);
      if (rnd == last) {
        continue;
      }

      last = emptyLocation;
      if (SwapIfValid(rnd, -size.x, -1)) {
        count++;
      } else if (SwapIfValid(rnd, +size.x, -1)) {
        count++;
      } else if (SwapIfValid(rnd, -1, 0)) {
        count++;
      } else if (SwapIfValid(rnd, +1, size.x - 1)) {
        count++;
      }
    }
  }
}
