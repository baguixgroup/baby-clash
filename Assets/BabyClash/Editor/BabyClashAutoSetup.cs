using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BabyClash.EditorTools
{
    /// <summary>
    /// Keeps generated content in sync: when the project is opened (or scripts reload) and the content is missing or
    /// from an older version, it's regenerated; otherwise the Arena scene is opened if nothing else is. Never runs in
    /// batch mode or play mode.
    /// </summary>
    [InitializeOnLoad]
    static class BabyClashAutoSetup
    {
        static readonly string SessionKey = "BabyClash.AutoSetupDone.v" + BabyClashContentBuilder.ContentVersion;

        static BabyClashAutoSetup()
        {
            if (Application.isBatchMode || SessionState.GetBool(SessionKey, false))
                return;
            EditorApplication.delayCall += Run;
        }

        static void Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Run; // try again once the import settles
                return;
            }

            SessionState.SetBool(SessionKey, true);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!BabyClashContentBuilder.IsContentCurrent())
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                Debug.Log("[Baby Clash] Generating game content (art, prefabs, UI and the Arena scene)...");
                BabyClashContentBuilder.BuildAll();
            }
            else if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            {
                EditorSceneManager.OpenScene(BabyClashContentBuilder.ScenePath);
            }
        }
    }
}
