using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// Editor utility: adds CropGridResizer to CropImageScreen and wires all
/// crop grid references. Run via Tools > Sliding Puzzle > Setup Crop Grid.
/// </summary>
public static class CropGridSetup
{
    [MenuItem("Tools/Sliding Puzzle/Setup Crop Grid")]
    public static void Setup()
    {
        // ── Find CropImageScreen ────────────────────────────────────────
        GameObject cropScreen = FindInScene("CropImageScreen");
        if (cropScreen == null)
        {
            Debug.LogError("[CropGridSetup] CropImageScreen not found!");
            return;
        }

        Debug.Log("[CropGridSetup] Found CropImageScreen.");

        // ── Find children ───────────────────────────────────────────────
        GameObject gridGO = FindChild(cropScreen.transform, "CropAreaRefrenceGrid");
        GameObject imageGO = FindChild(cropScreen.transform, "ImageToCrop");

        if (gridGO == null)
        {
            Debug.LogError("[CropGridSetup] CropAreaRefrenceGrid not found!");
            return;
        }
        if (imageGO == null)
        {
            Debug.LogError("[CropGridSetup] ImageToCrop not found!");
            return;
        }

        Debug.Log($"[CropGridSetup] Found grid={gridGO.name}, image={imageGO.name}");

        // ── Add CropGridResizer ─────────────────────────────────────────
        CropGridResizer resizer = cropScreen.GetComponent<CropGridResizer>();
        if (resizer == null)
        {
            resizer = cropScreen.AddComponent<CropGridResizer>();
            Debug.Log("[CropGridSetup] Added CropGridResizer component.");
        }
        else
        {
            Debug.Log("[CropGridSetup] CropGridResizer already exists.");
        }

        // Wire serialized fields
        SerializedObject soResizer = new SerializedObject(resizer);
        soResizer.FindProperty("gridRect").objectReferenceValue = gridGO.GetComponent<RectTransform>();
        soResizer.FindProperty("imageRect").objectReferenceValue = imageGO.GetComponent<RectTransform>();
        soResizer.FindProperty("imageToCropImage").objectReferenceValue = imageGO.GetComponent<Image>();
        soResizer.ApplyModifiedProperties();
        Debug.Log("[CropGridSetup] Wired CropGridResizer fields.");

        // ── Wire ImageCropper.cropViewPort → CropAreaRefrenceGrid ──────
        ImageCropper cropper = cropScreen.GetComponent<ImageCropper>();
        if (cropper != null)
        {
            Image gridImage = gridGO.GetComponent<Image>();
            if (gridImage != null)
            {
                SerializedObject soCropper = new SerializedObject(cropper);
                soCropper.FindProperty("cropViewPort").objectReferenceValue = gridImage;
                soCropper.ApplyModifiedProperties();
                Debug.Log("[CropGridSetup] Wired ImageCropper.cropViewPort → CropAreaRefrenceGrid.");
            }
        }

        // ── Wire ImageZoomController.cropGridResizer ────────────────────
        ImageZoomController zoomCtrl = cropScreen.GetComponent<ImageZoomController>();
        if (zoomCtrl != null)
        {
            SerializedObject soZoom = new SerializedObject(zoomCtrl);
            soZoom.FindProperty("cropGridResizer").objectReferenceValue = resizer;
            soZoom.ApplyModifiedProperties();
            Debug.Log("[CropGridSetup] Wired ImageZoomController.cropGridResizer.");
        }

        // ── Save ────────────────────────────────────────────────────────
        EditorUtility.SetDirty(cropScreen);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("<color=green>[CropGridSetup] All done! Save the scene.</color>");
    }

    private static GameObject FindInScene(string name)
    {
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            GameObject found = FindChild(root.transform, name);
            if (found != null) return found;
        }
        return null;
    }

    private static GameObject FindChild(Transform parent, string name)
    {
        if (parent.name == name) return parent.gameObject;
        for (int i = 0; i < parent.childCount; i++)
        {
            GameObject result = FindChild(parent.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }
}
