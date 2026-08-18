using System.Collections.Generic;
using UnityEngine;

namespace GameAssetBundle
{
    internal sealed class AssetLeaseOwner : MonoBehaviour
    {
        private readonly Dictionary<string, IAssetLease> _leases = new();
        private readonly Dictionary<string, int> _versions = new();

        internal int Reserve(string slot)
        {
            _versions.TryGetValue(slot, out int version);
            version++;
            _versions[slot] = version;
            return version;
        }

        internal bool TryTrack(string slot, int version, IAssetLease lease)
        {
            if (lease == null)
                return false;

            if (!_versions.TryGetValue(slot, out int currentVersion) || currentVersion != version)
            {
                lease.Release();
                return false;
            }

            if (_leases.TryGetValue(slot, out IAssetLease previousLease))
                previousLease?.Release();
            _leases[slot] = lease;
            return true;
        }

        internal void Release(string slot)
        {
            if (string.IsNullOrEmpty(slot))
                return;

            Reserve(slot);
            if (!_leases.TryGetValue(slot, out IAssetLease lease))
                return;

            _leases.Remove(slot);
            lease?.Release();
        }

        private void OnDestroy()
        {
            foreach (IAssetLease lease in _leases.Values)
                lease?.Release();
            _leases.Clear();
            _versions.Clear();
        }
    }
}
