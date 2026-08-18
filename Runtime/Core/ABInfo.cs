using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;
#if USE_TTSDK
using TTSDK;
#endif
#if USE_WECHAT
using WeChatWASM;
#endif


namespace GameAssetBundle
{
    /// <summary>
    /// AssetBundle封装
    /// </summary>
    internal class ABInfo
    {
        public AssetBundle assetBundle
        {
            get;
            set;
        }

        public string AssetBundleName
        {
            get;
            set;
        }

        public ABInfo()
        {
            IsReady = false;
            assetBundle = null;
            AssetBundleName = string.Empty;
        }

        //public bool IsScene = false;
        /// <summary>
        /// 标记当前是否准备完毕
        /// </summary>
        internal bool IsReady { get; private set; }
        internal bool IsLoadFailed { get; private set; }
        private Exception LoadException { get; set; }

        internal bool CanRetain => IsReady && !IsLoadFailed;
        internal bool HasLoadFailed => IsLoadFailed;
        //public bool isReady { get { return _isReady; } }

        public async UniTask GetIsReady(CancellationToken cancellationToken = default(CancellationToken))
        {
            await UniTask.WaitUntil(() =>
            {
                return IsReady || IsLoadFailed;
            }, PlayerLoopTiming.Update, cancellationToken, true);

            if (IsLoadFailed)
            {
                throw new InvalidOperationException(
                    $"AssetBundle {AssetBundleName} failed to load.",
                    LoadException);
            }
        }

        internal void MarkLoadFailed(Exception exception)
        {
            IsReady = false;
            IsLoadFailed = true;
            LoadException = exception;
        }

        private void MarkReady()
        {
            IsLoadFailed = false;
            LoadException = null;
            IsReady = true;
        }

        /// <summary>
        /// 强制的引用计数
        /// </summary>
        public int refCount { get; private set; }


        public void SetAssetBundleInfo(string assetBundleName)
        {
            AssetBundleName = assetBundleName;
        }

        /// <summary>
        /// 优化的异步加载方法，避免无限递归
        /// </summary>
        public async UniTask LoadAssetBundleAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            const int maxRetryCount = 3;
            int retryCount = 0;

            while (retryCount < maxRetryCount)
            {
                try
                {
                    assetBundle = await AssetBundleRuntimeContext.GetAssetBundleAsync(
                        AssetBundleManager.Instance.GetAssetBundlePath(AssetBundleName),
                        AssetBundleManager.Instance.IsAssetBundleEncrypted,
                        cancellationToken);

                    if (assetBundle != null)
                    {
                        Retain();
                        MarkReady();
                        return;
                    }

                    retryCount++;
                    if (retryCount < maxRetryCount)
                    {
                        AssetBundleRuntimeContext.LogWarning($"Failed to load AssetBundle {AssetBundleName}, retry {retryCount}/{maxRetryCount}");
                        await UniTask.Delay(1000 * retryCount, cancellationToken: cancellationToken); // 递增延迟
                    }
                }
                catch (OperationCanceledException)
                {
                    MarkLoadFailed(new OperationCanceledException(
                        $"AssetBundle loading cancelled for {AssetBundleName}",
                        null,
                        cancellationToken));
                    AssetBundleRuntimeContext.LogWarning($"AssetBundle loading cancelled for {AssetBundleName}");
                    throw;
                }
                catch (Exception e)
                {
                    retryCount++;
                    AssetBundleRuntimeContext.LogError($"Error loading AssetBundle {AssetBundleName} (attempt {retryCount}): {e.Message}");

                    if (retryCount >= maxRetryCount)
                    {
                        MarkLoadFailed(e);
                        throw;
                    }

                    await UniTask.Delay(1000 * retryCount, cancellationToken: cancellationToken);
                }
            }

            var exception = new InvalidOperationException(
                $"Failed to load AssetBundle {AssetBundleName} after {maxRetryCount} attempts");
            MarkLoadFailed(exception);
            AssetBundleRuntimeContext.LogError(exception.Message);
            throw exception;
        }

        //public async UniTaskVoid LoadABInfoAsync()
        //{

        //    assetBundle = await UnityWebRequestMgr.GetAssetBundle(AssetBundleManager.GetAssetBundlePath(AssetBundleName));
        //    if (assetBundle == null){
        //        return;
        //    }
        //    Retain();
        //    IsReady = true;
        //}

        public bool LoadABInfo()
        {
            try
            {
                string path = AssetBundleManager.Instance.GetAssetBundlePath(AssetBundleName);
#if USE_WECHAT
                assetBundle = WXAssetBundle.LoadFromFile(path);
#else
                assetBundle = AssetBundle.LoadFromFile(path);
#endif
                if (assetBundle == null)
                {
                    var exception = new InvalidOperationException(
                        $"Failed to load AssetBundle {AssetBundleName} from {path}");
                    MarkLoadFailed(exception);
                    AssetBundleRuntimeContext.LogError(exception.Message);
                    return false;
                }

                Retain();
                MarkReady();
                return true;
            }
            catch (Exception e)
            {
                MarkLoadFailed(e);
                AssetBundleRuntimeContext.LogError(
                    $"Error loading AssetBundle {AssetBundleName}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 这个资源是否不用了
        /// </summary>
        /// <returns></returns>
        public bool isUnused
        {
            get
            {
                if (IsReady == false)
                    return false;
                return refCount <= 0;
            }
        }

        public T LoadAsset<T>(string assetName) where T : UnityEngine.Object
        {
            if (assetBundle != null)
            {
                return assetBundle.LoadAsset<T>(assetName);
            }

            return null;
        }

        // public T[] LoadAssetWithSubAssets<T>(string asset) where T : UnityEngine.Object
        // {
        //     if (assetBundle != null)
        //     {
        //         return assetBundle.LoadAssetWithSubAssets<T>(asset);
        //     }

        //     return null;
        // }

        //public Object LoadAsset(string assetName, Type type)
        //{
        //    if (assetBundle != null && type != null)
        //    {
        //        return assetBundle.LoadAsset(assetName, type);
        //    }
        //    return null;
        //}

        //public Object[] LoadAssetAll()
        //{
        //    if (assetBundle != null)
        //    {
        //        return assetBundle.LoadAllAssets();
        //    }
        //    return null;
        //}

        //public AssetBundleRequest LoadAssetAsync<T>(string assetName) where T : UnityEngine.Object
        //{
        //    AssetBundleRequest request = null;
        //    if (assetBundle != null)
        //    {
        //        request = assetBundle.LoadAssetAsync<T>(assetName);
        //    }

        //    return request;
        //}

        //public AssetBundleRequest LoadAssetWithSubAssetsAsync(string asset)
        //{
        //    AssetBundleRequest request = null;
        //    if (assetBundle != null)
        //    {
        //        request = assetBundle.LoadAssetWithSubAssetsAsync(asset);
        //    }

        //    return request;
        //}

        /// <summary>
        /// 引用计数增一
        /// </summary>
        public void Retain()
        {
            refCount++;
        }

        /// <summary>
        /// 引用计数减一
        /// </summary>
        public void Release()
        {
            if (refCount <= 0)
            {
                AssetBundleRuntimeContext.LogWarning($"AssetBundle {AssetBundleName} released with non-positive refCount: {refCount}");
                refCount = 0;
                return;
            }

            refCount--;
        }

        public void Dispose(bool unloadAllLoadedObjects = false)
        {
            List<string> dependencies = AssetBundleManager.Instance.GetDependencies(AssetBundleName);
            if (dependencies != null)
            {
                foreach (var v in dependencies)
                {
                    ABInfoMgr.Instance.ReleaseInfo(v);
                }
            }

            UnloadBundle(unloadAllLoadedObjects);
            IsReady = false;
            IsLoadFailed = false;
            LoadException = null;
        }

        private void UnloadBundle(bool unloadAllLoadedObjects)
        {
            if (assetBundle != null)
            {
#if USE_WECHAT
                assetBundle.WXUnload(unloadAllLoadedObjects);
#elif USE_TTSDK
                assetBundle.Unload(unloadAllLoadedObjects);
#else
                assetBundle.Unload(unloadAllLoadedObjects);
#endif
            }
            assetBundle = null;
        }
    }
}
