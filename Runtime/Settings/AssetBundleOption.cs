using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameAssetBundle
{
    public class AssetBundleOption
    {
        static int m_SimulateAssetBundleInEditor = -1;
        static string kSimulateAssetBundles = "SimulateAssetBundles";
        /// <summary>
        /// 编辑器模式下是否模拟资源包加载方式？（不需要真正打出资源包，避免每改动一个资源都打一次包的过程）
        /// </summary>
        public static bool SimulateAssetBundleInEditor
        {
            get
            {
#if UNITY_EDITOR
                if (m_SimulateAssetBundleInEditor == -1)
                {
                    m_SimulateAssetBundleInEditor = UnityEditor.EditorPrefs.GetBool(kSimulateAssetBundles, true) ? 1 : 0;
                }

                return m_SimulateAssetBundleInEditor != 0;
#else
            return false;
#endif
            }
            set
            {
#if UNITY_EDITOR
                int newValue = value ? 1 : 0;
                if (newValue != m_SimulateAssetBundleInEditor)
                {
                    m_SimulateAssetBundleInEditor = newValue;
                    UnityEditor.EditorPrefs.SetBool(kSimulateAssetBundles, value);
                }
#else
            m_SimulateAssetBundleInEditor = 0;
#endif
            }
        }
    }

}
