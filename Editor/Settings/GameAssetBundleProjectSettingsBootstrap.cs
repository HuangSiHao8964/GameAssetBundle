#if UNITY_EDITOR
using UnityEditor;

namespace GameAssetBundle.Edit
{
    [InitializeOnLoad]
    internal static class GameAssetBundleProjectSettingsBootstrap
    {
        static GameAssetBundleProjectSettingsBootstrap()
        {
            EditorApplication.delayCall += EnsureProjectSettingsAssets;
        }

        private static void EnsureProjectSettingsAssets()
        {
            EditorApplication.delayCall -= EnsureProjectSettingsAssets;
            AssetBundleSettingsProvider.GetOrCreateSettings();
            AssetBundleBuildSettingProvider.GetOrCreateSetting();
            AssetBundleCollectData.GetOrCreateConfig();
        }
    }
}
#endif
