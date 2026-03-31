using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class TileController : MonoBehaviour
{
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private BoxCollider2D _collider;
    private Coroutine _moveRoutine;

    public Vector2Int CorrectCell { get; private set; }
    public Vector2Int CurrentCell { get; private set; }
    public bool IsInCorrectPosition => CurrentCell == CorrectCell;

    public void Initialize(
        int tileIndex,
        Vector2Int correctCell,
        Vector2Int currentCell,
        Vector3 localPosition,
        Vector3 localScale,
        Vector2[] uvCoordinates,
        Material sharedMaterial)
    {
        CacheComponents();
        CorrectCell = correctCell;
        CurrentCell = currentCell;
        name = $"Tile_{tileIndex:00}";

        transform.localPosition = localPosition;
        transform.localScale = localScale;
        _meshRenderer.sharedMaterial = sharedMaterial;
        _collider.size = Vector2.one;
        SetUvCoordinates(uvCoordinates);
    }

    public void SetCurrentCell(Vector2Int cell)
    {
        CurrentCell = cell;
    }

    public void SnapTo(Vector3 localPosition)
    {
        if (_moveRoutine != null)
        {
            StopCoroutine(_moveRoutine);
            _moveRoutine = null;
        }

        transform.localPosition = localPosition;
    }

    public IEnumerator AnimateTo(Vector3 targetLocalPosition, float duration)
    {
        if (_moveRoutine != null)
        {
            StopCoroutine(_moveRoutine);
        }

        _moveRoutine = StartCoroutine(AnimateRoutine(targetLocalPosition, duration));
        yield return _moveRoutine;
        _moveRoutine = null;
    }

    private IEnumerator AnimateRoutine(Vector3 targetLocalPosition, float duration)
    {
        Vector3 start = transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            transform.localPosition = Vector3.LerpUnclamped(start, targetLocalPosition, eased);
            yield return null;
        }

        transform.localPosition = targetLocalPosition;
    }

    private void SetUvCoordinates(Vector2[] uvCoordinates)
    {
        Mesh sourceMesh = _meshFilter.sharedMesh;
        Mesh meshCopy = Instantiate(sourceMesh);
        meshCopy.name = $"{name}_Mesh";
        meshCopy.uv = uvCoordinates;
        _meshFilter.mesh = meshCopy;
    }

    private void CacheComponents()
    {
        if (_meshFilter == null)
        {
            _meshFilter = GetComponent<MeshFilter>();
        }

        if (_meshRenderer == null)
        {
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        if (_collider == null)
        {
            _collider = GetComponent<BoxCollider2D>();
        }
    }

    private void OnDestroy()
    {
        if (_meshFilter != null && _meshFilter.mesh != null)
        {
            Destroy(_meshFilter.mesh);
        }
    }
}
