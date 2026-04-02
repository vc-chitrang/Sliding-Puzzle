using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// One-shot editor utility: creates BoardPanel and wires Phase 3 references.
/// Run via Tools → Sliding Puzzle → Setup Phase 3 Scene.
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

        // ── Wire ImageZoomController.pinchableScrollRect ────────────────
        var zcs = Resources.FindObjectsOfTypeAll<ImageZoomController>();
        var psrs = Resources.FindObjectsOfTypeAll<PinchableScrollRect>();
        if (zcs.Length > 0 && psrs.Length > 0)
        {
            var so2 = new SerializedObject(zcs[0]);
            var psr = so2.FindProperty("pinchableScrollRect");
            if (psr != null)
            {
                psr.objectReferenceValue = psrs[0];
                so2.ApplyModifiedProperties();
                Debug.Log("[SceneSetup] Wired ImageZoomController.pinchableScrollRect → " + psrs[0].gameObject.name);
            }
        }

        // ── Mark dirty ──────────────────────────────────────────────────
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log("[SceneSetup] Done! Save the scene.");
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
