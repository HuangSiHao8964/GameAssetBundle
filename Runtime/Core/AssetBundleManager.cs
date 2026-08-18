using Cysharp.Threading.Tasks;
#if USE_TTSDK
using TTSDK.UNBridgeLib.LitJson;
#else
using LitJson;
#endif
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace GameAssetBundle
{
    public partial class AssetBundleManager
    {
        private static readonly AssetBundleManager instance = new AssetBundleManager();

        public static AssetBundleManager Instance => instance;

        private AssetBundleRecord assetBundleRecord = null;
        private Dictionary<string, List<string>> dependencies = new Dictionary<string, List<string>>();
        private Dictionary<string, string> assetBundleDifference = new Dictionary<string, string>();
        public Dictionary<string, string> AssetBundleDifference { get { return assetBundleDifference; } }
        public AssetBundleRecord AssetBundleRecord { set { assetBundleRecord = value; } }
        public bool IsAssetBundleEncrypted { get; private set; } = true;

        internal async UniTask<ABInfo> InitAssetBundleWithAssetName(string assetName, CancellationToken cancellationToken = default(CancellationToken))
        {
            ABInfo assetBundleInfo = null;
            string realName = GetAssetRecordInfoByAsset(assetName);
            if (string.IsNullOrEmpty(realName) == false)
            {
                assetBundleInfo = await ABInfoMgr.Instance.CreateABInfoAsync(realName, cancellationToken);
            }
            return assetBundleInfo;
        }

        public AssetBundlePathType LoadPathType { get; private set; } = AssetBundlePathType.InitData;


        #region 初始化
        public bool IsReady { private set; get; }

        public void ShutDown()
        {
            IsReady = false;
            IsAssetBundleEncrypted = true;
        }

        public async UniTask StartUp()
        {
#if DISABLE_HOTFIX
            LoadPathType = AssetBundlePathType.InitData;
#else
            LoadPathType = AssetBundlePathType.Remote;
#endif
            AssetBundleRuntimeContext.Log("AssetBundleManager:StartUp");
            if (IsReady == true)
                return;
            await LoadAssetRecord();
            AssetBundleRuntimeContext.Log("LoadAssetRecord Over");

            //await LoadDifference();
            //m_CacheAssetBundleInfoDict = new Dictionary<string, AssetBundleInfo>();
            //m_CacheAssetBundleInfoKeys = new HashSet<string>();
            // 加载MainManifest
            if (!await LoadAllDependencies())
            {
                throw new InvalidOperationException("Failed to load AssetBundle dependencies manifest.");
            }
            AssetBundleRuntimeContext.Log("LoadAllDependencies Over");
            IsReady = true;
            //Debug.LogError("LoadLocalAssetBundleMainfest");

        }

        async UniTask LoadAssetRecord()
        {
#if !USE_WECHAT && !USE_TTSDK
            var content = AssetBundleRuntimeContext.ReadFileBytes(
                AssetBundleRuntimeContext.AssetRecordsFileName,
                AssetBundlePathType.Local);
            AssetBundleRuntimeContext.LogErrorFormat("FileUtility.ReadFileBytes Content:{0}", content);
            if (content == null)
            {
                var path = AssetBundleRuntimeContext.GetAssetFilePath(
                    AssetBundleRuntimeContext.AssetRecordsFileName,
                    AssetBundlePathType.InitData);
                AssetBundleRuntimeContext.LogErrorFormat("Try Request Bytes:{0}", path);
                content = await AssetBundleRuntimeContext.GetBytesAsync(path);
            }
#else
            var path = AssetBundleRuntimeContext.GetAssetFilePath(
                AssetBundleRuntimeContext.AssetRecordsFileName,
                AssetBundlePathType.Remote);
            AssetBundleRuntimeContext.LogErrorFormat("Try Request Bytes:{0}", path);
            var content = await AssetBundleRuntimeContext.GetBytesAsync(path);
#endif
            AssetBundleRuntimeContext.LogErrorFormat("LoadAssetRecord:{0}", content);
            assetBundleRecord = new AssetBundleRecord();
            assetBundleRecord.LoadRecord(content);
            IsAssetBundleEncrypted = assetBundleRecord.IsEncrypted;
        }

        async UniTask LoadDifference()
        {
            if (assetBundleDifference.Count > 0)
                return;
            string path = AssetBundleRuntimeContext.GetAssetFilePath(
                AssetBundleRuntimeContext.AssetBundleDifferenceFileName,
                LoadPathType);
            string difference = await AssetBundleRuntimeContext.GetTextAsync(path);

            //string difference = FileUtility.ReadFileText(AppDefine.AssetBundleDifference, PathType.Local);
            if (string.IsNullOrEmpty(difference))
                return;
            SetAssetBundleDifference(difference);
        }

        public void SetAssetBundleDifference(string difference)
        {
            if (string.IsNullOrEmpty(difference))
                return;
            var data = JsonMapper.ToObject<Dictionary<string, string>>(difference);
            // Debug.LogError(data);
            IEnumerator<string> keys = data.Keys.GetEnumerator();
            while (keys.MoveNext())
            {
                assetBundleDifference.Add(keys.Current, data[keys.Current]);
                // Debug.LogError(data[keys.Current]);
            }
        }

        AssetBundlePathType GetAssetBundlePathType(string assetBundle)
        {
            return assetBundleDifference.ContainsKey(assetBundle) ? AssetBundlePathType.Local : LoadPathType;
        }

        public string GetAssetBundlePath(string assetBundle)
        {
            AssetBundlePathType pathType = GetAssetBundlePathType(assetBundle);
            return AssetBundleRuntimeContext.GetAssetFilePath(
                assetBundleRecord.GetAssetBundleRealName(assetBundle),
                pathType);
        }

        public async UniTask<bool> LoadAllDependencies()
        {
            var mainfestPath = AssetBundleRuntimeContext.GetAssetFilePath(
                AssetBundleRuntimeContext.MainManifestPath,
                AssetBundleRuntimeContext.HasLocalManifest ? AssetBundlePathType.Local : LoadPathType);
            AssetBundle assetBundle = await AssetBundleRuntimeContext.GetAssetBundleAsync(
                mainfestPath,
                IsAssetBundleEncrypted);

            if (assetBundle == null)
            {
                Debug.LogError("Load MainManifest Error");
                return false;
            }

            try
            {
                var mainManifest = assetBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                if (mainManifest == null)
                {
                    Debug.LogError("AssetBundleManifest asset was not found in MainManifest bundle");
                    return false;
                }

                dependencies.Clear();
                var assetBundles = mainManifest.GetAllAssetBundles();
                foreach (var assetBundleName in assetBundles)
                {
                    LoadDependencies(assetBundleName, mainManifest);
                }

                return true;
            }
            finally
            {
                assetBundle.Unload(true);
            }
        }

        private List<string> LoadDependencies(string name, AssetBundleManifest mainManifest)
        {
            if (dependencies.ContainsKey(name))
                return dependencies[name];

            var directDependencies = mainManifest.GetDirectDependencies(name);
            List<string> dependencyList = new List<string>(directDependencies);
            dependencies.Add(name, dependencyList);
            return dependencyList;
        }

        public List<string> GetDependencies(string name)
        {

            if (dependencies.TryGetValue(name, out List<string> output) == false)
            {
                return null;
            }
            return output;
        }

        public bool CheckAsset(string assetName)
        {
            return assetBundleRecord != null && assetBundleRecord.assetMap.ContainsKey(assetName);
        }

        public string GetAssetRecordInfoByAsset(string assetName)
        {
            if (assetBundleRecord == null)
            {
                Debug.LogError("AssetBundle record is not initialized.");
                return string.Empty;
            }

            int index = -1;
            if (assetBundleRecord.assetMap.TryGetValue(assetName, out index) == false)
            {
                Debug.LogError(string.Format("Can't find asset record key[{0}] in AssetRecords, please check it!", assetName));
                return string.Empty;
            }
            return assetBundleRecord.bundleName[index];
        }

        public string GetAssetRecordInfoByIndex(int index)
        {
            if (assetBundleRecord == null || index < 0 || index >= assetBundleRecord.bundleName.Length)
            {
                return string.Empty;
            }
            return assetBundleRecord.bundleName[index];
        }

        public int GetAssetIndex(string assetName)
        {
            if (assetBundleRecord == null)
            {
                Debug.LogError("AssetBundle record is not initialized.");
                return -1;
            }

            int index = -1;
            if (assetBundleRecord.assetMap.TryGetValue(assetName, out index) == false)
            {
                Debug.LogError(string.Format("Can't find asset record key[{0}] in AssetRecords, please check it!", assetName));
            }
            return index;
        }

        #endregion


        #region Load Asset

        /// <summary>
        /// 同步加载一个资源
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="asset">资源名称，不关注大小写</param>
        /// <param name="unloadAssetBundle"></param>
        /// <param name="unloadAllLoadedObjects"></param>
        /// <returns></returns>


        internal T LoadAsset<T>(string assetName) where T : UnityEngine.Object
        {
            string realName = GetAssetRecordInfoByAsset(assetName);
            if (string.IsNullOrEmpty(realName))
            {
                Debug.LogErrorFormat("Can not Find {0} in AssetRecord", assetName);
                return null;
            }

            ABInfo assetBundleInfo = ABInfoMgr.Instance.CreateABInfo(realName);
            if (assetBundleInfo == null)
                return null;

            T asset = assetBundleInfo.LoadAsset<T>(assetName);
            if (asset == null)
                assetBundleInfo.Release();
            return asset;
        }

        /// <summary>
        /// 异步加载一个资源
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assetName"></param>
        /// <param name="unloadAssetBundle"></param>
        /// <param name="unloadAllLoadedObjects"></param>
        /// <returns></returns>

        internal async UniTask<T> LoadAssetAsync<T>(string assetName, CancellationToken cancellationToken = default(CancellationToken), bool release = false) where T : UnityEngine.Object
        {
            string realName = GetAssetRecordInfoByAsset(assetName);
            if (string.IsNullOrEmpty(realName))
                return null;

            ABInfo assetBundleInfo = await ABInfoMgr.Instance.CreateABInfoAsync(realName, cancellationToken);
            if (assetBundleInfo == null)
            {
                Debug.LogErrorFormat("AssetBundleInfoMgr.CreateABInfoAsync Faild: {0}", assetName);
                return null;
            }
            T t = assetBundleInfo.LoadAsset<T>(assetName);
            if (t == null)
            {
                Debug.LogErrorFormat("AssetBundleInfoMgr.LoadAsset Faild: {0}", assetName);
                assetBundleInfo.Release();
                return null;
            }
            if (release)
                assetBundleInfo.Release();
            return t;
        }

        internal async UniTask ReleaseAssetAsync<T>(string assetName) where T : UnityEngine.Object
        {
            string realName = GetAssetRecordInfoByAsset(assetName);
            if (string.IsNullOrEmpty(realName))
            {
                await UniTask.CompletedTask;
                return;
            }

            ABInfoMgr.Instance.ReleaseLoadedInfo(realName);
            await UniTask.CompletedTask;
        }

        internal async UniTask<T> LoadAssetByName<T>(string asset, CancellationToken cancellationToken = default(CancellationToken), bool release = false) where T : UnityEngine.Object
        {
            // string recordName = string.Empty;
            // if (AssetName.CheckAssetName(asset, typeof(T), out recordName) == false)
            // {
            //     RuntimeDebug.LogErrorFormat("Asset: {0} has not found", asset);
            //     return null;
            // }
            T t = null;
#if UNITY_EDITOR
            if (AssetBundleOption.SimulateAssetBundleInEditor)
            {
                await UniTask.Delay(1);
                string assetPath = GetAssetRecordInfoByAsset(asset);
                if (string.IsNullOrEmpty(assetPath))
                {
                    Debug.LogErrorFormat("Can not Find {0}", asset);
                    return null;
                }

                t = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assetPath);
            }
            else
#endif
                t = await LoadAssetAsync<T>(asset, cancellationToken, release);
            return t;
        }

        #endregion

        #region Unload AssetBundle
        internal void UnloadAsset(string asset)
        {
            if (IsReady == false)
                return;
            string abn = GetAssetRecordInfoByAsset(asset);
            if (string.IsNullOrEmpty(abn) == false)
            {
                ABInfoMgr.Instance.ReleaseInfo(abn);
            }
        }

        internal void UnloadAsset(int id)
        {
            if (IsReady == false)
                return;
            string abn = GetAssetRecordInfoByIndex(id);
            if (string.IsNullOrEmpty(abn) == false)
            {
                ABInfoMgr.Instance.ReleaseInfo(abn);
            }
        }

        #endregion
    }
}
