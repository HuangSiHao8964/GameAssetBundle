#if UNITY_EDITOR
using UnityEditor;

namespace GameAssetBundle.Edit
{
    public static class GameInEditor
    {
        [MenuItem("HaoFangTools/GameAssetBundle/Simulation/Reload Editor Resources %#L")]
        static void ReLoadEditorRes()
        {
            AssetBundleCollectData.GetAssetRecordInfoDict();
        }
    }

    internal sealed class GameInEditorInitializer : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths,
            bool didDomainReload)
        {
            if (didDomainReload)
            {
                AssetBundleCollectData.GetAssetRecordInfoDict();
            }
        }
    }
}

#endif
