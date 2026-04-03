using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// One-shot editor utility: creates BoardPanel, wires Phase 3 references,
/// and sets up the crop grid resizer system.
/// Run via Tools > Sliding Puzzle > Setup Phase 3 Scene.
/// </summary>
public static class SceneSetupHelper
{
    [MenuItem("Tools/Sliding Puzzle/Setup Phase 3 Scene")]
    public static void SetupScene()
    {
        // ── Find GamePlayScreen (may be inactive) ───────────────────────
        GameObject gamePlayScreen = null;
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            gamePlayScreen = FindDeep(root.transform, "GamePlayScreen");
            if (gamePlayScreen != null) break;
        }

        if (gamePlayScreen == null)
        {
            Debug.LogError("[SceneSetup] GamePlayScreen not found.");
            return;
        }

        // ── Delete any existing BoardPanel(s) ───────────────────────────
        for (int i = gamePlayScreen.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = gamePlayScreen.transform.GetChild(i);
            if (child.name == "BoardPanel")
                Undo.DestroyObjectImmediate(child.gameObject);
        }

        // ── Create BoardPanel (UI RectTransform) ────────────────────────
        GameObject bp = new GameObject("BoardPanel", typeof(RectTransform));
        bp.transform.SetParent(gamePlayScreen.transform, false);
        bp.layer = gamePlayScreen.layer;
        Undo.RegisterCreatedObjectUndo(bp, "Create BoardPanel");

        RectTransform boardRT = bp.GetComponent<RectTransform>();
        boardRT.anchorMin = Vector2.zero;
        boardRT.anchorMax = Vector2.one;
        boardRT.offsetMin = Vector2.zero;
        boardRT.offsetMax = Vector2.zero;
        boardRT.pivot = new Vector2(0.5f, 0.5f);
        Debug.Log("[SceneSetup] Created BoardPanel under GamePlayScreen.");

        // ── Wire GameManager.boardPanel ─────────────────────────────────
        GameManager gm = Resources.FindObjectsOfTypeAll<GameManager>()[0];
        if (gm != null)
        {
            var so = new SerializedObject(gm);
            var prop = so.FindProperty("boardPanel");
            if (prop != null)
            {
                prop.objectReferenceValue = boardRT;
                so.ApplyModifiedProperties();
                Debug.Log("[SceneSetup] Wired GameManager.boardPanel → BoardPanel.");
            }
        }
        else
        {
            Debug.LogError("[SceneSetup] GameManager not found.");
        }

        // ── Wire CropGridResizer system ─────────────────────────────────
        SetupCropGridResizer();

        // ── Mark dirty ──────────────────────────────────────────────────
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log("[SceneSetup] Done! Save the scene.");
    }

    /// <summary>
    /// Finds CropImageScreen, adds CropGridResizer, and wires all references
    /// for the crop grid system (ImageCropper.cropViewPort, ImageZoomController, etc.).
    /// </summary>
    private static void SetupCropGridResizer()
    {
        // Find CropImageScreen
        GameObject cropScreen = null;
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            cropScreen = FindDeep(root.transform, "CropImageScreen");
            if (cropScreen != null) break;
        }

        if (cropScreen == null)
        {
            Debug.LogWarning("[SceneSetup] CropImageScreen not found. Skipping crop grid setup.");
            return;
        }

        // Find CropAreaReferenceGrid and ImageToCrop
        GameObject gridGO = FindDeep(cropScreen.transform, "CropAreaReferenceGrid");
        GameObject imageGO = FindDeep(cropScreen.transform, "ImageToCrop");

        if (gridGO == null || imageGO == null)
        {
            Debug.LogWarning("[SceneSetup] CropAreaReferenceGrid or ImageToCrop not found.");
            return;
        }

        RectTransform gridRT = gridGO.GetComponent<RectTransform>();
        RectTransform imageRT = imageGO.GetComponent<RectTransform>();
        Image imageImg = imageGO.GetComponent<Image>();
        Image gridImg = gridGO.GetComponent<Image>();

        // ── Add/Get CropGridResizer on CropImageScreen ──────────────────
        CropGridResizer resizer = cropScreen.GetComponent<CropGridResizer>();
        if (resizer == null)
            resizer = Undo.AddComponent<CropGridResizer>(cropScreen);

        {
            var so = new SerializedObject(resizer);
            SetRef(so, "gridRect", gridRT);
            SetRef(so, "imageRect", imageRT);
            SetRef(so, "imageToCropImage", imageImg);
            so.ApplyModifiedProperties();
            Debug.Log("[SceneSetup] Wired CropGridResizer references.");
        }

        // ── Wire ImageCropper.cropViewPort → CropAreaReferenceGrid Image ─
        ImageCropper cropper = cropScreen.GetComponent<ImageCropper>();
        if (cropper != null && gridImg != null)
        {
            var so = new SerializedObject(cropper);
            SetRef(so, "cropViewPort", gridImg);
            so.ApplyModifiedProperties();
            Debug.Log("[SceneSetup] Wired ImageCropper.cropViewPort → CropAreaReferenceGrid.");
        }

        // ── Wire ImageZoomController.cropGridResizer ────────────────────
        ImageZoomController zoomCtrl = cropScreen.GetComponent<ImageZoomController>();
        if (zoomCtrl != null)
        {
            var so = new SerializedObject(zoomCtrl);
            SetRef(so, "cropGridResizer", resizer);
            so.ApplyModifiedProperties();
            Debug.Log("[SceneSetup] Wired ImageZoomController.cropGridResizer.");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Utility
    // ─────────────────────────────────────────────────────────────────

    private static void SetRef(SerializedObject so, string propName, Object value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null)
            prop.objectReferenceValue = value;
        else
            Debug.LogWarning($"[SceneSetup] Property '{propName}' not found on {so.targetObject.GetType().Name}.");
    }

    private static GameObject FindDeep(Transform root, string targetName)
    {
        if (root.name == targetName) return root.gameObject;
        for (int i = 0; i < root.childCount; i++)
        {
            GameObject result = FindDeep(root.GetChild(i), targetName);
            if (result != null) return result;
        }
        return null;
    }
}
