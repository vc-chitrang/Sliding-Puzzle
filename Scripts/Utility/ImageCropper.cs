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
        Vector3[] imageCorners = new Vector3[4];
        cropViewPort.rectTransform.GetWorldCorners(viewportCorners);
        imageToCrop.rectTransform.GetWorldCorners(imageCorners);

        // World-space bounds (min/max)
        Vector2 vpMin = viewportCorners[0]; // bottom-left
        Vector2 vpMax = viewportCorners[2]; // top-right
        Vector2 imgMin = imageCorners[0];
        Vector2 imgMax = imageCorners[2];

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
    }

}
