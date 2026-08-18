using UnityEngine;

namespace GameAssetBundle
{
    /// <summary>
    /// Project-wide names and extensions used by the asset bundle pipeline.
    /// The asset is loaded from Resources so the same values are available in
    /// the editor, standalone player and hot-update runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "setting", menuName = "HaoFangTools/GameAssetBundle/Settings")]
    public sealed class AssetBundleSettings : ScriptableObject
    {
        public const string ResourcesPath = "setting";

        public const string DefaultAssetRecordsFileName = "ARecords.json";
        public const string DefaultMainHybridName = "HotUpdate";
        public const string DefaultHybridFileExtension = ".bytes";
        public const string DefaultHybridDllExtension = ".dll";
        public const string DefaultAssetBundleDifference = "difference.json";
        public const string DefaultAssetBundleNameMap = "AssetBundleNameMap.json";
        public const string DefaultAssetBundleExt = ".u";
        public const string DefaultAssetPackFileName = "assets.upk";
        public const string DefaultVersionFilename = "Version.json";
        public const string DefaultPreloadFile = "preload.json";
        public const string DefaultMd5Filename = "md5.json";
        public const string DefaultFileListName = "FileList.json";
        public const string DefaultLocalAbrPath = "ABR";

        private static AssetBundleSettings s_Instance;

        [SerializeField] private string m_assetRecordsFileName = DefaultAssetRecordsFileName;
        [SerializeField] private string m_mainHybridName = DefaultMainHybridName;
        [SerializeField] private string m_hybridFileExtension = DefaultHybridFileExtension;
        [SerializeField] private string m_hybridDllExtension = DefaultHybridDllExtension;
        [SerializeField] private string m_assetBundleDifference = DefaultAssetBundleDifference;
        [SerializeField] private string m_assetBundleNameMap = DefaultAssetBundleNameMap;
        [SerializeField] private string m_assetBundleExt = DefaultAssetBundleExt;
        [SerializeField] private string m_assetPackFileName = DefaultAssetPackFileName;
        [SerializeField] private string m_versionFilename = DefaultVersionFilename;
        [SerializeField] private string m_preloadFile = DefaultPreloadFile;
        [SerializeField] private string m_md5Filename = DefaultMd5Filename;
        [SerializeField] private string m_fileListName = DefaultFileListName;
        [SerializeField] private string m_localAbrPath = DefaultLocalAbrPath;

        public static AssetBundleSettings Instance
        {
            get
            {
                if (s_Instance == null)
                    s_Instance = Resources.Load<AssetBundleSettings>(ResourcesPath);

                return s_Instance;
            }
        }

        public string AssetRecordsFileName => ValueOrDefault(m_assetRecordsFileName, DefaultAssetRecordsFileName);
        public string MainHybridName => ValueOrDefault(m_mainHybridName, DefaultMainHybridName);
        public string HybridFileExtension => ValueOrDefault(m_hybridFileExtension, DefaultHybridFileExtension);
        public string HybridDllExtension => ValueOrDefault(m_hybridDllExtension, DefaultHybridDllExtension);
        public string AssetBundleDifference => ValueOrDefault(m_assetBundleDifference, DefaultAssetBundleDifference);
        public string AssetBundleNameMap => ValueOrDefault(m_assetBundleNameMap, DefaultAssetBundleNameMap);
        public string AssetBundleExt => ValueOrDefault(m_assetBundleExt, DefaultAssetBundleExt);
        public string AssetPackFileName => ValueOrDefault(m_assetPackFileName, DefaultAssetPackFileName);
        public string VersionFilename => ValueOrDefault(m_versionFilename, DefaultVersionFilename);
        public string PreloadFile => ValueOrDefault(m_preloadFile, DefaultPreloadFile);
        public string Md5Filename => ValueOrDefault(m_md5Filename, DefaultMd5Filename);
        public string FileListName => ValueOrDefault(m_fileListName, DefaultFileListName);
        public string LocalAbrPath => ValueOrDefault(m_localAbrPath, DefaultLocalAbrPath);

        public void ResetToDefaults()
        {
            m_assetRecordsFileName = DefaultAssetRecordsFileName;
            m_mainHybridName = DefaultMainHybridName;
            m_hybridFileExtension = DefaultHybridFileExtension;
            m_hybridDllExtension = DefaultHybridDllExtension;
            m_assetBundleDifference = DefaultAssetBundleDifference;
            m_assetBundleNameMap = DefaultAssetBundleNameMap;
            m_assetBundleExt = DefaultAssetBundleExt;
            m_assetPackFileName = DefaultAssetPackFileName;
            m_versionFilename = DefaultVersionFilename;
            m_preloadFile = DefaultPreloadFile;
            m_md5Filename = DefaultMd5Filename;
            m_fileListName = DefaultFileListName;
            m_localAbrPath = DefaultLocalAbrPath;
        }

        private static string ValueOrDefault(string value, string defaultValue)
        {
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }
    }
}
