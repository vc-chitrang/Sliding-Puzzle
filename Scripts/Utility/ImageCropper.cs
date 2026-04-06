using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generic, reusable image cropping system for Unity UI.
/// Works with any image, resolution, and UI layout.
///
/// Setup:
///   1. Place ImageToCrop inside a ScrollRect (with PinchableScrollRect for zoom/pan).
///   2. CropViewPort is the viewport / mask area the user sees through.
///   3. Wire a UI Button's OnClick to call Crop().
///   4. Optionally assign OutputImage to display the result.
/// </summary>
public class ImageCropper : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The UI Image acting as the visible crop window / mask.")]
    [SerializeField] private Image cropViewPort;

    [Tooltip("The UI Image the user can zoom, pan, and adjust.")]
    [SerializeField] private Image imageToCrop;

    [Tooltip("(Optional) UI Image where the cropped result will be displayed.")]
    [SerializeField] private Image outputImage;

    [Header("Output Settings")]
    [Tooltip("Final output is always square. This controls the resolution (e.g. 1024 → 1024x1024).")]
    [SerializeField] private int outputSize = 1024;

    [Header("Screen Flow")]
    [Tooltip("The crop screen root — hidden after cropping.")]
    [SerializeField] private GameObject cropImageScreen;

    [Tooltip("The gameplay screen root — shown after cropping.")]
    [SerializeField] private GameObject gamePlayScreen;

    [Tooltip("GameManager that receives the cropped sprite to build the puzzle.")]
    [SerializeField] private GameManager gameManager;

    [Tooltip("Controls post-crop UI state (hides edit controls, shows START button).")]
    [SerializeField] private CropScreenController cropScreenController;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    /// <summary>
    /// The last cropped sprite. Read this after calling Crop().
    /// </summary>
    public Sprite CroppedSprite { get; private set; }

    /// <summary>
    /// Crops the visible portion of <see cref="imageToCrop"/> that falls inside
    /// <see cref="cropViewPort"/> and produces a NEW sprite.
    /// The original texture is never modified.
    /// </summary>
    public void Crop()
    {
        // ── Validate references ──────────────────────────────────────────
        if (cropViewPort == null || imageToCrop == null)
        {
            Debug.LogError("ImageCropper: CropViewPort or ImageToCrop is not assigned.");
            return;
        }

        Sprite sourceSprite = imageToCrop.sprite;
        if (sourceSprite == null || sourceSprite.texture == null)
        {
            Debug.LogError("ImageCropper: ImageToCrop has no sprite or texture.");
            return;
        }

        Texture2D sourceTex = sourceSprite.texture;

        // ── STEP 1: Get world-space corners of both rects ────────────────
        Vector3[] viewportCorners = new Vector3[4];
        cropViewPort.rectTransform.GetWorldCorners(viewportCorners);

        // World-space bounds (min/max)
        Vector2 vpMin = viewportCorners[0]; // bottom-left
        Vector2 vpMax = viewportCorners[2]; // top-right

        // Use visible image bounds (preserveAspect-aware) instead of
        // raw RectTransform corners — prevents incorrect crop coordinates
        // when the rendered image is smaller than its RectTransform.
        Vector2 imgMin, imgMax;
        ComputeVisibleImageBounds(imageToCrop, out imgMin, out imgMax);

        if (enableDebugLogs)
        {
            Debug.Log($"[ImageCropper] Viewport world: min={vpMin}, max={vpMax}");
            Debug.Log($"[ImageCropper] Image world:    min={imgMin}, max={imgMax}");
        }

        // ── STEP 2: Calculate overlap in world space ─────────────────────
        float overlapMinX = Mathf.Max(vpMin.x, imgMin.x);
        float overlapMinY = Mathf.Max(vpMin.y, imgMin.y);
        float overlapMaxX = Mathf.Min(vpMax.x, imgMax.x);
        float overlapMaxY = Mathf.Min(vpMax.y, imgMax.y);

        float overlapWidth = overlapMaxX - overlapMinX;
        float overlapHeight = overlapMaxY - overlapMinY;

        if (overlapWidth <= 0f || overlapHeight <= 0f)
        {
            Debug.LogWarning("ImageCropper: No visible overlap between viewport and image.");
            return;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[ImageCropper] Overlap world: ({overlapMinX},{overlapMinY}) to ({overlapMaxX},{overlapMaxY})");
        }

        // ── STEP 3: Convert overlap to normalized image-space [0..1] ─────
        float imageWorldWidth = imgMax.x - imgMin.x;
        float imageWorldHeight = imgMax.y - imgMin.y;

        if (imageWorldWidth <= 0f || imageWorldHeight <= 0f)
        {
            Debug.LogWarning("ImageCropper: Image has zero world-space dimensions.");
            return;
        }

        float normMinX = (overlapMinX - imgMin.x) / imageWorldWidth;
        float normMinY = (overlapMinY - imgMin.y) / imageWorldHeight;
        float normMaxX = (overlapMaxX - imgMin.x) / imageWorldWidth;
        float normMaxY = (overlapMaxY - imgMin.y) / imageWorldHeight;

        // Clamp to [0,1] for safety
        normMinX = Mathf.Clamp01(normMinX);
        normMinY = Mathf.Clamp01(normMinY);
        normMaxX = Mathf.Clamp01(normMaxX);
        normMaxY = Mathf.Clamp01(normMaxY);

        // ── STEP 4: Map normalized coords to pixel coords ────────────────
        // Account for the sprite's textureRect (handles packed atlases too)
        Rect spriteRect = sourceSprite.textureRect;

        int pixelX = Mathf.FloorToInt(spriteRect.x + normMinX * spriteRect.width);
        int pixelY = Mathf.FloorToInt(spriteRect.y + normMinY * spriteRect.height);
        int pixelW = Mathf.FloorToInt((normMaxX - normMinX) * spriteRect.width);
        int pixelH = Mathf.FloorToInt((normMaxY - normMinY) * spriteRect.height);

        // Clamp within texture bounds
        pixelX = Mathf.Clamp(pixelX, 0, sourceTex.width - 1);
        pixelY = Mathf.Clamp(pixelY, 0, sourceTex.height - 1);
        pixelW = Mathf.Clamp(pixelW, 1, sourceTex.width - pixelX);
        pixelH = Mathf.Clamp(pixelH, 1, sourceTex.height - pixelY);

        if (enableDebugLogs)
        {
            Debug.Log($"[ImageCropper] Pixel rect: x={pixelX}, y={pixelY}, w={pixelW}, h={pixelH}");
            Debug.Log($"[ImageCropper] Source texture: {sourceTex.width}x{sourceTex.height}");
        }

        // ── STEP 5: GPU-accelerated crop + square resize ─────────────────
        // Blit only the crop region from the source texture into a square
        // RenderTexture at the desired output resolution.  This avoids
        // needing a readable source texture AND produces a 1:1 result in
        // a single GPU pass — no CPU GetPixels required.

        int squareSize = Mathf.Clamp(outputSize, 64, 4096);

        // Normalized UV rect of the crop region within the full texture
        float uvX = (float)pixelX / sourceTex.width;
        float uvY = (float)pixelY / sourceTex.height;
        float uvW = (float)pixelW / sourceTex.width;
        float uvH = (float)pixelH / sourceTex.height;

        RenderTexture rt = RenderTexture.GetTemporary(
            squareSize, squareSize, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);

        // Blit the crop sub-rect into the full square RT
        // scale  = size of source region,  offset = bottom-left corner
        Graphics.Blit(sourceTex, rt, new Vector2(uvW, uvH), new Vector2(uvX, uvY));

        // Read the square result back to a Texture2D
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D croppedTex = new Texture2D(squareSize, squareSize, TextureFormat.RGBA32, false);
        croppedTex.filterMode = FilterMode.Bilinear;
        croppedTex.wrapMode = TextureWrapMode.Clamp;
        croppedTex.ReadPixels(new Rect(0, 0, squareSize, squareSize), 0, 0);
        croppedTex.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        // ── STEP 6: Create a new sprite from the square texture ──────────
        CroppedSprite = Sprite.Create(
            croppedTex,
            new Rect(0, 0, squareSize, squareSize),
            new Vector2(0.5f, 0.5f)
        );
        CroppedSprite.name = "CroppedSprite";

        if (enableDebugLogs)
        {
            Debug.Log($"[ImageCropper] Cropped {pixelW}x{pixelH} → resized to {squareSize}x{squareSize} square");
        }

        // ── STEP 7: Assign to output image if provided ───────────────────
        if (outputImage != null)
        {
            outputImage.sprite = CroppedSprite;
            outputImage.preserveAspect = true;
        }

        // ── STEP 8: Post-crop state ───────────────────────────────────────
        // If a CropScreenController is wired, hand off the sprite to it so it
        // can show the START button and hide the edit controls.
        // Fallback: original direct-switch behaviour (no controller assigned).
        if (cropScreenController != null)
        {
            cropScreenController.OnCropComplete(CroppedSprite);

            if (enableDebugLogs)
                Debug.Log("[ImageCropper] Crop complete — notified CropScreenController.");
        }
        else if (gameManager != null)
        {
            if (cropImageScreen != null) cropImageScreen.SetActive(false);
            if (gamePlayScreen  != null) gamePlayScreen.SetActive(true);
            gameManager.StartPuzzleWithCroppedSprite(CroppedSprite);

            if (enableDebugLogs)
                Debug.Log("[ImageCropper] Switched to GamePlayScreen and started puzzle.");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes the world-space bounds of the actual rendered image,
    /// accounting for <c>preserveAspect</c> on the Image component.
    /// When preserveAspect is true, the rendered image can be smaller
    /// than the RectTransform — this returns the true visible bounds.
    /// </summary>
    private static void ComputeVisibleImageBounds(Image img, out Vector2 visMin, out Vector2 visMax)
    {
        Vector3[] corners = new Vector3[4];
        img.rectTransform.GetWorldCorners(corners);
        Vector2 rtMin = corners[0];
        Vector2 rtMax = corners[2];
        float rtW = rtMax.x - rtMin.x;
        float rtH = rtMax.y - rtMin.y;

        if (img.sprite == null || !img.preserveAspect || rtW <= 0f || rtH <= 0f)
        {
            visMin = rtMin;
            visMax = rtMax;
            return;
        }

        float spriteAspect = img.sprite.rect.width / img.sprite.rect.height;
        float rtAspect = rtW / rtH;
        float visW, visH;

        if (spriteAspect > rtAspect)
        {
            // Wider than container — width fills, height shrinks
            visW = rtW;
            visH = rtW / spriteAspect;
        }
        else
        {
            // Taller — height fills, width shrinks
            visH = rtH;
            visW = rtH * spriteAspect;
        }

        Vector2 center = (rtMin + rtMax) * 0.5f;
        visMin = center - new Vector2(visW, visH) * 0.5f;
        visMax = center + new Vector2(visW, visH) * 0.5f;
    }
}
