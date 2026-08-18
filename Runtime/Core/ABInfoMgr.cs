using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameAssetBundle
{
    public class ABInfoMgr
    {
        private static readonly ABInfoMgr instance = new ABInfoMgr();

        public static ABInfoMgr Instance => instance;

        private float _currentTime = 0f;
        private float _currentDirtyAssetCheckPointTime = 0.0f;
        private readonly float DIRTY_ASSET_CHECK_POINT_TIME_GAP = 10.0f;

        private int _unloadLimit = 10;
        private int _unloadCount = 0;
        private bool _hasUnusedBundle = false;

        private List<string> _CacheAssetBundleInfoKeys = new List<string>();
        private Dictionary<string, ABInfo> _CacheAssetBundleInfoDict = new Dictionary<string, ABInfo>();

        internal void ReleaseInfo(string assetBundleName)
        {
            ABInfo info = null;
            if (_CacheAssetBundleInfoDict.TryGetValue(assetBundleName, out info))
            {
                info.Release();
            }
        }

        internal bool ReleaseLoadedInfo(string assetBundleName)
        {
            if (string.IsNullOrEmpty(assetBundleName))
                return false;

            if (_CacheAssetBundleInfoDict.TryGetValue(assetBundleName, out ABInfo info) == false)
                return false;

            info.Release();
            return true;
        }



        internal ABInfo CreateABInfo(string assetBundleName)
        {
            ABInfo info = null;
            if (_CacheAssetBundleInfoDict.TryGetValue(assetBundleName, out info))
            {
                if (info.CanRetain)
                {
                    info.Retain();
                    return info;
                }

                if (info.HasLoadFailed)
                    RemoveCachedInfo(assetBundleName, info);
                else
                    AssetBundleRuntimeContext.LogWarning(
                        $"AssetBundle {assetBundleName} is still loading and cannot be loaded synchronously.");

                return null;
            }

            info = new ABInfo();
            info.SetAssetBundleInfo(assetBundleName);
            _CacheAssetBundleInfoDict.Add(assetBundleName, info);

            List<string> dependencies = AssetBundleManager.Instance.GetDependencies(assetBundleName);
            var retainedDependencies = new List<string>();

            try
            {
                if (dependencies != null)
                {
                    foreach (var dependency in dependencies)
                    {
                        ABInfo dependencyInfo = CreateABInfo(dependency);
                        if (dependencyInfo == null)
                        {
                            throw new InvalidOperationException(
                                $"Failed to load dependency {dependency} for {assetBundleName}.");
                        }

                        retainedDependencies.Add(dependency);
                    }
                }

                if (!info.LoadABInfo())
                    throw new InvalidOperationException($"Failed to load AssetBundle {assetBundleName}.");

                return info;
            }
            catch (Exception e)
            {
                info.MarkLoadFailed(e);
                RemoveCachedInfo(assetBundleName, info);
                RollbackRetainedDependencies(retainedDependencies);
                AssetBundleRuntimeContext.LogError(
                    $"Failed to create AssetBundle info for {assetBundleName}: {e.Message}");
                return null;
            }
        }

        // Start is called before the first frame update
        internal async UniTask<ABInfo> CreateABInfoAsync(string assetBundleName, CancellationToken cancellationToken = default(CancellationToken))
        {
            ABInfo info = null;
            if (_CacheAssetBundleInfoDict.TryGetValue(assetBundleName, out info))
            {
                await info.GetIsReady(cancellationToken);
                info.Retain();
                return info;
            }

            info = new ABInfo();
            info.SetAssetBundleInfo(assetBundleName);
            _CacheAssetBundleInfoDict.Add(assetBundleName, info);

            List<string> dependencies = AssetBundleManager.Instance.GetDependencies(assetBundleName);
            bool[] retainedDependencies = dependencies == null
                ? Array.Empty<bool>()
                : new bool[dependencies.Count];

            try
            {
                if (dependencies != null && dependencies.Count > 0)
                {
                    List<UniTask> tasks = new List<UniTask>(dependencies.Count);
                    for (int i = 0; i < dependencies.Count; i++)
                    {
                        tasks.Add(RetainDependencyAsync(
                            dependencies[i],
                            i,
                            retainedDependencies,
                            cancellationToken));
                    }

                    await UniTask.WhenAll(tasks);
                }

                await info.LoadAssetBundleAsync(cancellationToken);
                return info;
            }
            catch (Exception e)
            {
                info.MarkLoadFailed(e);
                RemoveCachedInfo(assetBundleName, info);
                RollbackRetainedDependencies(dependencies, retainedDependencies);
                throw;
            }
        }

        private async UniTask RetainDependencyAsync(
            string assetBundleName,
            int dependencyIndex,
            bool[] retainedDependencies,
            CancellationToken cancellationToken)
        {
            ABInfo info = await CreateABInfoAsync(assetBundleName, cancellationToken);
            if (info == null)
                throw new InvalidOperationException($"Failed to load dependency {assetBundleName}.");

            retainedDependencies[dependencyIndex] = true;
        }

        private void RollbackRetainedDependencies(List<string> retainedDependencies)
        {
            for (int i = retainedDependencies.Count - 1; i >= 0; i--)
            {
                ReleaseInfo(retainedDependencies[i]);
            }
        }

        private void RollbackRetainedDependencies(List<string> dependencies, bool[] retainedDependencies)
        {
            if (dependencies == null)
                return;

            for (int i = dependencies.Count - 1; i >= 0; i--)
            {
                if (retainedDependencies[i])
                    ReleaseInfo(dependencies[i]);
            }
        }

        private void RemoveCachedInfo(string assetBundleName, ABInfo expectedInfo)
        {
            if (_CacheAssetBundleInfoDict.TryGetValue(assetBundleName, out ABInfo cachedInfo) &&
                ReferenceEquals(cachedInfo, expectedInfo))
            {
                _CacheAssetBundleInfoDict.Remove(assetBundleName);
            }
        }

        public static void DisposeABInfo(string assetBundleName)
        {

        }

        public void Update()
        {
            _currentTime = Time.time;
            // Check dirty asset.
            if (_currentTime - _currentDirtyAssetCheckPointTime >= DIRTY_ASSET_CHECK_POINT_TIME_GAP)
            {
                TryToUnloadAssetBundleAndAssets();
                _currentDirtyAssetCheckPointTime = _currentTime;
            }
        }

        public void AppendDiagnostics(StringBuilder builder)
        {
            if (builder == null)
                return;

            builder.AppendLine($"Asset bundles: {_CacheAssetBundleInfoDict.Count}");
            foreach (KeyValuePair<string, ABInfo> pair in _CacheAssetBundleInfoDict)
            {
                ABInfo info = pair.Value;
                builder.AppendLine(
                    $"  {pair.Key}: refs={info?.refCount ?? 0}, ready={info?.IsReady ?? false}, " +
                    $"failed={info?.IsLoadFailed ?? false}");
            }
        }

        private void TryToUnloadAssetBundleAndAssets()
        {
#if UNITY_EDITOR
            if (AssetBundleOption.SimulateAssetBundleInEditor)
            {
                return;
            }
            else
#endif
            //return;
            //if (_CacheAssetBundleInfoKeys.Count <= 0 )
            //    return;

            //List<string> CacheAssetBundleInfoKeys = _CacheAssetBundleInfoDict.Keys.ToList();
            //_hasUnusedBundle = false;
            //一次最多卸载的个数，防止卸载过多太卡
            {
                _unloadCount = 0;
                ABInfo abi = null;

                _hasUnusedBundle = false;

                var e = _CacheAssetBundleInfoDict.GetEnumerator();
                while (e.MoveNext() && _unloadCount < _unloadLimit)
                {
                    abi = e.Current.Value;
                    if (abi != null && abi.isUnused)
                    {
                        if (_CacheAssetBundleInfoKeys.Contains(abi.AssetBundleName))
                        {
                            AssetBundleRuntimeContext.LogFormat("重复添加需要卸载的ab：{0}", abi.AssetBundleName);
                        }
                        else
                        {
                            _hasUnusedBundle = true;
                            _unloadCount++;
                            _CacheAssetBundleInfoKeys.Add(abi.AssetBundleName);
                        }
                    }
                }

                if (_hasUnusedBundle)
                {
                    foreach (var v in _CacheAssetBundleInfoKeys)
                    {
                        UnloadAssetBundle(v);
                    }
                    _CacheAssetBundleInfoKeys.Clear();
                }
            }
        }

        void UnloadAssetBundle(string assetBundleName)
        {
            if (string.IsNullOrEmpty(assetBundleName))
            {
                return;
            }
            ABInfo asstBundleInfo = null;
            if (_CacheAssetBundleInfoDict.TryGetValue(assetBundleName, out asstBundleInfo))
            {
                AssetBundleRuntimeContext.LogErrorFormat("卸载ab：{0}", assetBundleName);
                _CacheAssetBundleInfoDict.Remove(assetBundleName);
                asstBundleInfo.Dispose();
                asstBundleInfo = null;
            }
        }
    }
}
