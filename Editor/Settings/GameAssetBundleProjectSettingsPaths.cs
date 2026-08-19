#if UNITY_EDITOR
using UnityEditor;

namespace GameAssetBundle.Edit
{
    internal static class GameAssetBundleProjectSettingsPaths
    {
        internal const string EditorFolderPath = "Assets/Editor";
        internal const string SettingsAssetPath = AssetBundleSettings.ProjectAssetPath;
        internal const string BuildSettingsAssetPath = EditorFolderPath + "/GameAssetBundleBuildSettings.asset";
        internal const string CollectConfigAssetPath = EditorFolderPath + "/GameAssetBundleCollectConfig.asset";

        internal static bool EnsureEditorFolder()
        {
            if (AssetDatabase.IsValidFolder(EditorFolderPath))
            {
                return true;
            }

            if (!AssetDatabase.IsValidFolder("Assets"))
            {
                return false;
            }

            AssetDatabase.CreateFolder("Assets", "Editor");
            return AssetDatabase.IsValidFolder(EditorFolderPath);
        }
    }
}
#endif
