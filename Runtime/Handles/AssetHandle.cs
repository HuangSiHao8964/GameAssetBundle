using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameAssetBundle
{
    internal sealed class AssetHandle
    {
        internal CancellationTokenSource cancelToken;
        internal Object sourceObj;
        internal string asset = string.Empty;
        internal Type type;
        internal readonly List<Object> instantiates = new();

        private bool _isDisposed;
        private bool _isCancelled;
        private bool _ownsAssetBundleRef;

        internal async UniTask<T> AssetObject<T>() where T : Object
        {
            if (_isDisposed || _isCancelled || cancelToken == null)
                return null;

            if (sourceObj != null)
                return sourceObj as T;

            try
            {
                T loadedAsset = await AssetBundleManager.Instance.LoadAssetByName<T>(
                    asset,
                    cancelToken.Token,
                    false);

                if (_isDisposed)
                {
                    if (loadedAsset != null)
                        AssetBundleManager.Instance.UnloadAsset(asset);
                    return null;
                }

                sourceObj = loadedAsset;
                _ownsAssetBundleRef = loadedAsset != null;
                return loadedAsset;
            }
            catch (OperationCanceledException)
            {
                _isCancelled = true;
                return null;
            }
            catch (Exception exception)
            {
                AssetBundleRuntimeContext.LogError(
                    $"Failed to load asset {asset}: {exception.Message}");
                return null;
            }
        }

        internal async UniTask<GameObject> Instantiate()
        {
            GameObject prefab = await AssetObject<GameObject>();
            if (prefab == null || _isDisposed)
                return null;

            GameObject instance = GameObject.Instantiate(prefab);
            instance.name = asset;
            instantiates.Add(instance);
            return instance;
        }

        internal void ReleaseAll()
        {
            if (_isDisposed)
                return;

            Cancel();

            foreach (Object instance in instantiates)
            {
                if (instance != null)
                    GameObject.Destroy(instance);
            }
            instantiates.Clear();

            ReleaseBundleReference();
            sourceObj = null;
            Dispose();
        }

        internal void Release(GameObject instance, bool destroyObject)
        {
            if (_isDisposed || ReferenceEquals(instance, null))
                return;

            for (int i = instantiates.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(instantiates[i], instance))
                {
                    instantiates.RemoveAt(i);
                    break;
                }
            }
            if (destroyObject && instance != null)
                GameObject.Destroy(instance);

            if (instantiates.Count == 0)
            {
                ReleaseBundleReference();
                sourceObj = null;
                Dispose();
            }
        }

        private void Cancel()
        {
            if (_isCancelled || cancelToken == null)
                return;

            _isCancelled = true;
            cancelToken.Cancel();
        }

        private void ReleaseBundleReference()
        {
            if (!_ownsAssetBundleRef)
                return;

            _ownsAssetBundleRef = false;
            if (AssetBundleManager.Instance != null)
                AssetBundleManager.Instance.UnloadAsset(asset);
        }

        private void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            cancelToken?.Dispose();
            cancelToken = null;
        }
    }
}
