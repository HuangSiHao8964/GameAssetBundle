using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameAssetBundle
{
    internal interface IAssetLease : IDisposable
    {
        long Id { get; }
        string AssetName { get; }
        bool IsReleased { get; }
        void Release();
    }

    internal sealed class AssetLease<T> : IAssetLease where T : Object
    {
        private AssetHandle _handle;
        private T _value;
        private readonly bool _isInstance;
        private int _released;

        internal AssetLease(long id, AssetHandle handle, T value, bool isInstance)
        {
            Id = id;
            _handle = handle;
            _value = value;
            _isInstance = isInstance;
        }

        public long Id { get; }
        public string AssetName => _handle?.asset ?? string.Empty;
        public bool IsReleased => Volatile.Read(ref _released) != 0;
        public T Value => IsReleased ? null : _value;

        public void Release()
        {
            ReleaseInternal(true);
        }

        public void Dispose()
        {
            Release();
        }

        internal void ReleaseFromDestroyedInstance()
        {
            ReleaseInternal(false);
        }

        private void ReleaseInternal(bool destroyInstance)
        {
            if (Interlocked.Exchange(ref _released, 1) != 0)
                return;

            AssetManager.Unregister(this);

            AssetHandle handle = _handle;
            T value = _value;
            _handle = null;
            _value = null;

            if (_isInstance && value is GameObject gameObject)
            {
                var tracker = gameObject.GetComponent<AssetInstanceLeaseTracker>();
                tracker?.Unbind((AssetLease<GameObject>)(object)this);
                handle?.Release(gameObject, destroyInstance);
                return;
            }

            handle?.ReleaseAll();
        }
    }

    public static class AssetManager
    {
        private static readonly Dictionary<long, IAssetLease> LiveLeases = new();
        private static readonly HashSet<AssetHandle> PendingHandles = new();
        private static readonly Dictionary<Object, List<IAssetLease>> ManualLeases =
            new(ObjectReferenceComparer.Instance);
        private static long _nextLeaseId;

        public static int LiveLeaseCount => LiveLeases.Count;
        public static int PendingLoadCount => PendingHandles.Count;

        public static bool IsRetained(Object asset)
        {
            return !ReferenceEquals(asset, null) &&
                   ManualLeases.TryGetValue(asset, out List<IAssetLease> leases) &&
                   leases.Count > 0;
        }

        public static void Init()
        {
            Clean();
            _nextLeaseId = 0;
        }

        public static async UniTask<T> LoadAsync<T>(
            string assetName,
            CancellationToken cancellationToken = default)
            where T : Object
        {
            AssetLease<T> lease = await LoadLeaseAsync<T>(assetName, cancellationToken);
            T value = lease?.Value;
            if (value == null)
            {
                lease?.Release();
                return null;
            }

            TrackManualLease(value, lease);
            return value;
        }

        public static async UniTask<T> LoadAsync<T>(
            Component owner,
            string assetName,
            string slot = null,
            CancellationToken cancellationToken = default)
            where T : Object
        {
            if (owner == null)
                return null;

            string leaseSlot = slot ?? typeof(T).FullName;
            var leaseOwner = owner.GetComponent<AssetLeaseOwner>();
            if (leaseOwner == null)
                leaseOwner = owner.gameObject.AddComponent<AssetLeaseOwner>();
            int leaseVersion = leaseOwner.Reserve(leaseSlot);

            AssetLease<T> lease = await LoadLeaseAsync<T>(assetName, cancellationToken);
            if (lease == null)
                return null;

            if (owner == null)
            {
                lease.Release();
                return null;
            }

            return leaseOwner.TryTrack(leaseSlot, leaseVersion, lease)
                ? lease.Value
                : null;
        }

        private static async UniTask<AssetLease<T>> LoadLeaseAsync<T>(
            string assetName,
            CancellationToken cancellationToken)
            where T : Object
        {
            AssetHandle handle = CreateHandle<T>(assetName);
            if (handle == null)
                return null;

            UniTask<T> loadTask = handle.AssetObject<T>().Preserve();
            try
            {
                T asset = cancellationToken.CanBeCanceled
                    ? await loadTask.AttachExternalCancellation(cancellationToken)
                    : await loadTask;

                CompletePending(handle);
                if (asset == null)
                {
                    handle.ReleaseAll();
                    return null;
                }

                return CreateLease(handle, asset, false);
            }
            catch (OperationCanceledException)
            {
                ReleaseWhenCompleted(loadTask, handle).Forget();
                throw;
            }
            catch
            {
                CompletePending(handle);
                handle.ReleaseAll();
                throw;
            }
        }

        public static async UniTask<GameObject> InstantiateAsync(
            string assetName,
            Transform parent = null,
            CancellationToken cancellationToken = default)
        {
            AssetHandle handle = CreateHandle<GameObject>(assetName);
            if (handle == null)
                return null;

            UniTask<GameObject> instantiateTask = handle.Instantiate().Preserve();
            try
            {
                GameObject instance = cancellationToken.CanBeCanceled
                    ? await instantiateTask.AttachExternalCancellation(cancellationToken)
                    : await instantiateTask;

                CompletePending(handle);
                if (instance == null)
                {
                    handle.ReleaseAll();
                    return null;
                }

                if (parent != null)
                    instance.transform.SetParent(parent, false);

                AssetLease<GameObject> lease = CreateLease(handle, instance, true);
                var tracker = instance.GetComponent<AssetInstanceLeaseTracker>();
                if (tracker == null)
                    tracker = instance.AddComponent<AssetInstanceLeaseTracker>();
                tracker.Bind(lease);
                return instance;
            }
            catch (OperationCanceledException)
            {
                ReleaseWhenCompleted(instantiateTask, handle).Forget();
                throw;
            }
            catch
            {
                CompletePending(handle);
                handle.ReleaseAll();
                throw;
            }
        }

        public static string GetDiagnostics()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Asset leases: {LiveLeases.Count}, pending loads: {PendingHandles.Count}");
            foreach (IAssetLease lease in LiveLeases.Values)
                builder.AppendLine($"  [{lease.Id}] {lease.AssetName} ({lease.GetType().Name})");
            ABInfoMgr.Instance.AppendDiagnostics(builder);
            return builder.ToString();
        }

        public static void Release(Object assetOrInstance)
        {
            if (ReferenceEquals(assetOrInstance, null))
                return;

            if (TryReleaseManualLease(assetOrInstance))
                return;

            if (assetOrInstance == null)
                return;

            if (assetOrInstance is not GameObject instance)
                return;

            var tracker = instance.GetComponent<AssetInstanceLeaseTracker>();
            if (tracker == null)
                tracker = instance.GetComponentInParent<AssetInstanceLeaseTracker>();
            if (tracker != null)
                tracker.Release();
            else
                GameObject.Destroy(instance);
        }

        public static void Release(Component owner, string slot)
        {
            if (owner == null || string.IsNullOrEmpty(slot))
                return;

            owner.GetComponent<AssetLeaseOwner>()?.Release(slot);
        }

        public static void Clean()
        {
            var leases = new List<IAssetLease>(LiveLeases.Values);
            foreach (IAssetLease lease in leases)
                lease.Release();
            LiveLeases.Clear();
            ManualLeases.Clear();

            var pendingHandles = new List<AssetHandle>(PendingHandles);
            PendingHandles.Clear();
            foreach (AssetHandle handle in pendingHandles)
                handle.ReleaseAll();
        }

        internal static void Unregister(IAssetLease lease)
        {
            if (lease != null)
                LiveLeases.Remove(lease.Id);
        }

        private static AssetHandle CreateHandle<T>(string assetName) where T : Object
        {
            string asset = AssetName.GetAssetName(assetName, typeof(T));
            if (string.IsNullOrEmpty(asset))
            {
                AssetBundleRuntimeContext.LogError(
                    $"Asset {assetName} was not found for type {typeof(T).Name}.");
                return null;
            }

            var handle = new AssetHandle
            {
                asset = asset,
                type = typeof(T),
                cancelToken = new CancellationTokenSource()
            };
            PendingHandles.Add(handle);
            return handle;
        }

        private static AssetLease<T> CreateLease<T>(AssetHandle handle, T value, bool isInstance)
            where T : Object
        {
            long id = Interlocked.Increment(ref _nextLeaseId);
            var lease = new AssetLease<T>(id, handle, value, isInstance);
            LiveLeases[id] = lease;
            return lease;
        }

        private static void TrackManualLease(Object value, IAssetLease lease)
        {
            if (!ManualLeases.TryGetValue(value, out List<IAssetLease> leases))
            {
                leases = new List<IAssetLease>();
                ManualLeases[value] = leases;
            }
            leases.Add(lease);
        }

        private static bool TryReleaseManualLease(Object value)
        {
            if (!ManualLeases.TryGetValue(value, out List<IAssetLease> leases) || leases.Count == 0)
                return false;

            int lastIndex = leases.Count - 1;
            IAssetLease lease = leases[lastIndex];
            leases.RemoveAt(lastIndex);
            if (leases.Count == 0)
                ManualLeases.Remove(value);
            lease.Release();
            return true;
        }

        private static void CompletePending(AssetHandle handle)
        {
            PendingHandles.Remove(handle);
        }

        private static async UniTask ReleaseWhenCompleted<T>(UniTask<T> task, AssetHandle handle)
        {
            try
            {
                await task;
            }
            catch
            {
                // The caller already observed cancellation. This observer only owns cleanup.
            }
            finally
            {
                CompletePending(handle);
                handle.ReleaseAll();
            }
        }

        private sealed class ObjectReferenceComparer : IEqualityComparer<Object>
        {
            internal static readonly ObjectReferenceComparer Instance = new();

            public bool Equals(Object x, Object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(Object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
