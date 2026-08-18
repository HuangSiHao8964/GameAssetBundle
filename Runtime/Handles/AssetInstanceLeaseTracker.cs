using System;
using UnityEngine;

namespace GameAssetBundle
{
    internal sealed class AssetInstanceLeaseTracker : MonoBehaviour
    {
        private AssetLease<GameObject> _lease;

        internal void Bind(AssetLease<GameObject> lease)
        {
            _lease = lease;
            hideFlags = HideFlags.HideInInspector;
        }

        internal void Unbind(AssetLease<GameObject> lease)
        {
            if (ReferenceEquals(_lease, lease))
                _lease = null;
        }

        internal void Release()
        {
            var lease = _lease;
            _lease = null;
            lease?.Release();
        }

        private void OnDestroy()
        {
            var lease = _lease;
            _lease = null;
            lease?.ReleaseFromDestroyedInstance();
        }
    }
}
