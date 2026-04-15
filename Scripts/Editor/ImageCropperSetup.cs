using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor utility that creates the full ImageCropper scene setup under the existing
/// CropImageScreen in the PuzzleCanvas. Run from menu: Tools > Setup Image Cropper.
/// </summary>
public static class ImageCropperSetup
{
    [MenuItem("Tools/Setup Image Cropper")]
    public static void SetupImageCropper()
    {
        // ── Find CropImageScreen (already exists in the scene) ───────────
        GameObject cropImageScreen = GameObject.Find("CropImageScreen");
        if (cropImageScreen == null)
        {
            Debug.LogError("ImageCropperSetup: CropImageScreen not found in scene. "
                         + "Make sure SampleScene is loaded.");
            return;
        }

        // ── Find CropImageSV (the PinchableScrollRect container) ────────
        Transform cropImageSV = cropImageScreen.transform.Find("CropImageSV");
        if (cropImageSV == null)
        {
            Debug.LogError("ImageCropperSetup: CropImageSV not found under CropImageScreen.");
            return;
        }

        // ── Find or create the Viewport under CropImageSV ───────────────
        Transform viewport = cropImageSV.Find("Viewport");
        if (viewport == null)
        {
            // Try alternate name
            viewport = cropImageSV.Find("CropViewPort");
        }

        Image cropViewPortImage = null;
        if (viewport != null)
        {
            cropViewPortImage = viewport.GetComponent<Image>();
            if (cropViewPortImage == null)
            {
                cropViewPortImage = viewport.gameObject.AddComponent<Image>();
                cropViewPortImage.color = new Color(1, 1, 1, 0.01f); // Nearly invisible
            }

            // Ensure it has a Mask component for visual clipping
            if (viewport.GetComponent<Mask>() == null)
            {
                Mask mask = viewport.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;
            }

            Debug.Log($"ImageCropperSetup: Using existing viewport '{viewport.name}' as CropViewPort.");
        }
        else
        {
            // Create CropViewPort
            GameObject vpGO = new GameObject("CropViewPort", typeof(RectTransform));
            vpGO.transform.SetParent(cropImageSV, false);

            RectTransform vpRect = vpGO.GetComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.sizeDelta = Vector2.zero;
            vpRect.anchoredPosition = Vector2.zero;

            cropViewPortImage = vpGO.AddComponent<Image>();
            cropViewPortImage.color = new Color(1, 1, 1, 0.01f);

            Mask mask = vpGO.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            Debug.Log("ImageCropperSetup: Created CropViewPort under CropImageSV.");
            viewport = vpGO.transform;
        }

        // ── Find or create the ImageToCrop inside the viewport ───────────
        Transform contentTransform = null;
        ScrollRect scrollRect = cropImageSV.GetComponent<ScrollRect>();
        if (scrollRect != null && scrollRect.content != null)
        {
            contentTransform = scrollRect.content;
        }

        Image imageToCropImage = null;
        if (contentTransform != null)
        {
            imageToCropImage = contentTransform.GetComponent<Image>();
            if (imageToCropImage == null)
            {
                imageToCropImage = contentTransform.gameObject.AddComponent<Image>();
                imageToCropImage.preserveAspect = true;
            }
            Debug.Log($"ImageCropperSetup: Using ScrollRect content '{contentTransform.name}' as ImageToCrop.");
        }
        else
        {
            // Create ImageToCrop as content inside viewport
            GameObject imgGO = new GameObject("ImageToCrop", typeof(RectTransform));
            imgGO.transform.SetParent(viewport, false);

            RectTransform imgRect = imgGO.GetComponent<RectTransform>();
            imgRect.anchorMin = Vector2.zero;
            imgRect.anchorMax = Vector2.one;
            imgRect.sizeDelta = Vector2.zero;
            imgRect.anchoredPosition = Vector2.zero;

            imageToCropImage = imgGO.AddComponent<Image>();
            imageToCropImage.preserveAspect = true;

            // Wire it as the ScrollRect content
            if (scrollRect != null)
            {
                scrollRect.content = imgRect;
            }

            Debug.Log("ImageCropperSetup: Created ImageToCrop under viewport.");
            contentTransform = imgGO.transform;
        }

        // NOTE: CropResultPanel has been removed. Post-crop display is handled by
        // Artwork_Focus_Screen (ArtworkFocusScreenController). The ImageCropper.outputImage
        // field is intentionally left null — the crop result flows through
        // CropScreenController.OnCropComplete → ArtworkFocusScreenController.SetData.

        // ── Create Crop Button ───────────────────────────────────────────
        GameObject cropButtonGO = GameObject.Find("CropImageButton");
        Button cropButton;
        if (cropButtonGO == null)
        {
            cropButtonGO = new GameObject("CropImageButton", typeof(RectTransform));
            cropButtonGO.transform.SetParent(cropImageScreen.transform, false);

            RectTransform btnRect = cropButtonGO.GetComponent<RectTransform>();
            // Bottom-center of CropImageScreen
            btnRect.anchorMin = new Vector2(0.3f, 0.01f);
            btnRect.anchorMax = new Vector2(0.7f, 0.08f);
            btnRect.sizeDelta = Vector2.zero;
            btnRect.anchoredPosition = Vector2.zero;

            Image btnBg = cropButtonGO.AddComponent<Image>();
            btnBg.color = new Color(0.2f, 0.6f, 0.3f, 1f); // Green

            cropButton = cropButtonGO.AddComponent<Button>();
            cropButton.targetGraphic = btnBg;

            // Add label text
            GameObject labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(cropButtonGO.transform, false);

            RectTransform labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            labelRect.anchoredPosition = Vector2.zero;

            // Try TMP first, fallback to legacy Text
            var tmpText = labelGO.AddComponent<TMPro.TextMeshProUGUI>();
            tmpText.text = "Crop Image";
            tmpText.alignment = TMPro.TextAlignmentOptions.Center;
            tmpText.fontSize = 24;
            tmpText.color = Color.white;

            Debug.Log("ImageCropperSetup: Created CropImageButton.");
        }
        else
        {
            cropButton = cropButtonGO.GetComponent<Button>();
            if (cropButton == null)
            {
                cropButton = cropButtonGO.AddComponent<Button>();
            }
        }

        // ── Attach ImageCropper component ────────────────────────────────
        ImageCropper cropper = cropImageScreen.GetComponent<ImageCropper>();
        if (cropper == null)
        {
            cropper = cropImageScreen.AddComponent<ImageCropper>();
        }

        // Wire serialized fields via SerializedObject
        SerializedObject so = new SerializedObject(cropper);
        so.FindProperty("cropViewPort").objectReferenceValue = cropViewPortImage;
        so.FindProperty("imageToCrop").objectReferenceValue = imageToCropImage;
        so.FindProperty("outputImage").objectReferenceValue = null;  // CropResultPanel removed
        so.FindProperty("enableDebugLogs").boolValue = true;
        so.ApplyModifiedProperties();

        // Wire button OnClick → ImageCropper.Crop()
        // Clear any existing persistent listeners first
        int listenerCount = cropButton.onClick.GetPersistentEventCount();
        for (int i = listenerCount - 1; i >= 0; i--)
        {
            UnityEditor.Events.UnityEventTools.RemovePersistentListener(cropButton.onClick, i);
        }
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            cropButton.onClick,
            cropper.Crop
        );

        // Mark scene dirty so the user can save
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
        );

        Debug.Log("<color=green>ImageCropperSetup: Setup complete! "
                + "CropImageScreen now has ImageCropper with all references wired.</color>");

        // Select the CropImageScreen so user can inspect
        Selection.activeGameObject = cropImageScreen;
    }
}
