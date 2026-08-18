using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameAssetBundle
{
    public class PathUtility
    {
        /// <summary>
        /// 资源记录文件
        /// </summary>
        public static string AssetRecordsFileName
        {
            get
            {
                return Settings != null
                    ? Settings.AssetRecordsFileName
                    : AssetBundleSettings.DefaultAssetRecordsFileName;
            }
        }
        public static string MainHybridName
        {
            get
            {
                return Settings != null
                    ? Settings.MainHybridName
                    : AssetBundleSettings.DefaultMainHybridName;
            }
        }

        public static string HybridFileName(string name)
        {
            string extension = Settings != null
                ? Settings.HybridFileExtension
                : AssetBundleSettings.DefaultHybridFileExtension;
            return StringExtensions.Format("{0}{1}", name, extension);
        }

        public static string HybridDll(string name)
        {
            string extension = Settings != null
                ? Settings.HybridDllExtension
                : AssetBundleSettings.DefaultHybridDllExtension;
            return StringExtensions.Format("{0}{1}", name, extension);
        }

        public static string AssetBundleDifference
        {
            get
            {
                return Settings != null
                    ? Settings.AssetBundleDifference
                    : AssetBundleSettings.DefaultAssetBundleDifference;
            }
        }

        public static string AssetBundleNameMap
        {
            get
            {
                return Settings != null
                    ? Settings.AssetBundleNameMap
                    : AssetBundleSettings.DefaultAssetBundleNameMap;
            }
        }

        public static string AssetBundleExt
        {
            get
            {
                return Settings != null
                    ? Settings.AssetBundleExt
                    : AssetBundleSettings.DefaultAssetBundleExt;
            }
        }

        public static string AssetPackFileName
        {
            get
            {
                return Settings != null
                    ? Settings.AssetPackFileName
                    : AssetBundleSettings.DefaultAssetPackFileName;
            }
        }

        ///// <summary>
        ///// 资源记录文件
        ///// </summary>
        //public static string PackageInfoFileName
        //{
        //    get { return "PackageInfo.ab"; }
        //}


        /// <summary>
        /// 版本记录文件
        /// </summary>
        public static string VersionFilename
        {
            get
            {
                return Settings != null
                    ? Settings.VersionFilename
                    : AssetBundleSettings.DefaultVersionFilename;
            }
        }

        public static string PreloadFile
        {
            get
            {
                return Settings != null
                    ? Settings.PreloadFile
                    : AssetBundleSettings.DefaultPreloadFile;
            }
        }

        public static string Md5Filename
        {
            get
            {
                return Settings != null
                    ? Settings.Md5Filename
                    : AssetBundleSettings.DefaultMd5Filename;
            }
        }

        /// <summary>
        /// 文件md5记录文件
        /// </summary>
        public static string FileListName
        {
            get
            {
                return Settings != null
                    ? Settings.FileListName
                    : AssetBundleSettings.DefaultFileListName;
            }
        }

                /// <summary>
        /// 本地数据临时根目录
        /// </summary>
#if UNITY_EDITOR
        public static string LOCAL_TEMP_PATH
        {
            get
            {
                int lastIndex = Application.dataPath.IndexOf("/Assets");
                string tar = Application.dataPath.Substring(0, lastIndex);
                return StringExtensions.Format("{0}/LocalResources/{1}/Cache/Temp", tar, PlatformPath);
            }
        }
#else
        public static string LOCAL_TEMP_PATH {
            get {
                return StringExtensions.Format("{0}/{1}/Cache/Temp", Application.persistentDataPath, PlatformPath);
            }
        }
#endif

/// <summary>
        /// 平台路径
        /// </summary>
        public static string PlatformPath
        {
            get
            {
#if UNITY_EDITOR
                switch (UnityEditor.EditorUserBuildSettings.activeBuildTarget)
                {
                    case UnityEditor.BuildTarget.Android:
                        return "Android";
                    case UnityEditor.BuildTarget.iOS:
                        return "iOS";
                    case UnityEditor.BuildTarget.StandaloneWindows:
                    case UnityEditor.BuildTarget.StandaloneWindows64:
                    case UnityEditor.BuildTarget.StandaloneOSX:
                    case UnityEditor.BuildTarget.StandaloneLinux64:
                        return "Windows";
                    case UnityEditor.BuildTarget.WebGL:
                        return "WebGL";
#if TUANJIE_2022_3_OR_NEWER
                    case UnityEditor.BuildTarget.WeixinMiniGame:
                        return "WeixinMiniGame";
#endif
                    default:
                        return null;

                }
#else
                switch (Application.platform)
                {
                    case RuntimePlatform.Android:
                        return "Android";
                    case RuntimePlatform.IPhonePlayer:
                        return "iOS";
                    case RuntimePlatform.WindowsPlayer:
                    case RuntimePlatform.WindowsEditor:
                    case RuntimePlatform.LinuxEditor:
                    case RuntimePlatform.OSXEditor:
                        return "Windows";
                    case RuntimePlatform.WebGLPlayer:
                        return "WebGL";
#if TUANJIE_2022_3_OR_NEWER
                    case RuntimePlatform.WeixinMiniGamePlayer:
                        return "WeixinMiniGame";
#endif
                    default:
                        return null;
                }
#endif
            }
        }
    
        private static string mainfestPath = string.Empty;
        public static string MainfestPath
        {
            get
            {
                if (string.IsNullOrEmpty(mainfestPath))
                    mainfestPath = StringExtensions.Format("{0}.json", PlatformPath);
                return mainfestPath;
            }
        }
        /// <summary>
        /// 远程更新数据根目录
        /// </summary>
        private static string remoteDataPath = string.Empty;
        public static string REMOTE_DATA_PATH
        {
            get
            {
                return remoteDataPath;
            }
            set 
            {
                remoteDataPath = value;
            }
        }
        /// <summary>
        /// 本地初始数据根目录(streamingAssetsPath)
        /// </summary>
        public static string LOCAL_INIT_DATA_PATH
        {
            get
            {
#if UNITY_IPHONE
                return StringExtensions.Format("file://{0}", Application.streamingAssetsPath);
#else
                return Application.streamingAssetsPath;
#endif
            }
        }

        public static string LOCAL_INIT_DATA_EDITOR_PATH
        {
            get
            {
                return Application.streamingAssetsPath;
            }
        }

#if UNITY_EDITOR
        public static string LOCAL_DATA_PATH
        {
            get
            {
                int lastIndex = Application.dataPath.IndexOf("/Assets");
                string tar = Application.dataPath.Substring(0, lastIndex);
                return StringExtensions.Format("{0}/LocalResources/{1}/Cache/Data", tar, PlatformPath);
            }
        }
#else
        public static string LOCAL_DATA_PATH
        {
            get
            {
                return StringExtensions.Format("{0}/{1}/Cache/Data", Application.persistentDataPath, PlatformPath);
            }
        }
#endif




#if UNITY_EDITOR
        public static string LOCAL_RES_DIR
        {
            get
            {
                int lastIndex = Application.dataPath.IndexOf("/Assets");
                string tar = Application.dataPath.Substring(0, lastIndex);
                return StringExtensions.Format("{0}/LocalResources/{1}/Cache/", tar, PlatformPath);
            }
        }
#else
        public static string LOCAL_RES_DIR {
            get {
                return StringExtensions.Format("{0}/{1}/Cache/", Application.persistentDataPath, PlatformPath);
            }
        }
#endif


        /// <summary>
        /// 本地AssetBundleRes目录
        /// </summary>
        public static string LOCAL_ABR_PATH
        {
            get
            {
                return Settings != null
                    ? Settings.LocalAbrPath
                    : AssetBundleSettings.DefaultLocalAbrPath;
            }
        }

        private static AssetBundleSettings Settings => AssetBundleSettings.Instance;

        public static string GetARBPath(string path)
        {
            return StringExtensions.Format("Assets/{0}/{1}", LOCAL_ABR_PATH, path.Replace("\\", "/").TrimStart('/'));
        }
    }
}
