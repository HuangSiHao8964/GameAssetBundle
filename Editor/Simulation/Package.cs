using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using System;
using GameAssetBundle;

namespace GameAssetBundle.Edit
{
    public static class Package
    {
        #region Menu Items

        const string kSimulateAssetBundlesMenu = "HaoFangTools/GameAssetBundle/Simulation/模拟AssetBundles";

        [MenuItem(kSimulateAssetBundlesMenu, false, 1)]
        public static void ToggleSimulateAssetBundle()
        {
            AssetBundleOption.SimulateAssetBundleInEditor = !AssetBundleOption.SimulateAssetBundleInEditor;
            Debug.LogErrorFormat("AssetBundleOption.SimulateAssetBundleInEditor:{0}", AssetBundleOption.SimulateAssetBundleInEditor);
        }

        [MenuItem(kSimulateAssetBundlesMenu, true, 1)]
        public static bool ToggleSimulateAssetBundleValidate()
        {
            Menu.SetChecked(kSimulateAssetBundlesMenu, AssetBundleOption.SimulateAssetBundleInEditor);
            return true;
        }

        #endregion



        [MenuItem("HaoFangTools/GameAssetBundle/Validation/Check Loop By Manifest")]
        public static void CheckLoopByManifest()
        {
            Caching.ClearCache();
            AssetBundle assetBundle = AssetBundle.LoadFromFile(FileUtility.GetAssetFilePath(PathUtility.MainfestPath, PathType.InitData));
            AssetBundleManifest manifest = assetBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            //dependenceMap保存bundle的直接依赖关系
            Dictionary<string, HashSet<string>> dependenceMap = new Dictionary<string, HashSet<string>>();
            string[] abs = manifest.GetAllAssetBundles();
            for (int i = 0; i < abs.Length; i++)
            {
                string abName = abs[i];
                string[] directDependencies = manifest.GetDirectDependencies(abName);
                dependenceMap.Add(abName, new HashSet<string>(directDependencies));
            }

            //q保存需要检查环的bundle名字
            LinkedList<string> q = new LinkedList<string>();
            foreach (var entry in dependenceMap)
            {
                q.AddLast(entry.Key);
            }

            //searchedNodeSet保存遍历过的bundle，避免重复遍历
            HashSet<string> searchedNodeSet = new HashSet<string>();

            //loopSet记录检查到的环
            HashSet<string[]> loopSet = new HashSet<string[]>();
            while (q.Count > 0)
            {
                string bundleName = q.First.Value;
                q.RemoveFirst();

                //stack记录深度遍历时遍历到的bundle
                List<string> stack = new List<string>();

                //开始通过遍历检查
                SearchLoopByManifest(bundleName, stack, searchedNodeSet, dependenceMap, loopSet);

                //把遍历过的bundle从q删除
                foreach (var node in searchedNodeSet)
                {
                    q.Remove(node);
                }
            }

            assetBundle.Unload(true);

            //以抛出异常的方式，打印所有环信息
            int maxPrintLoopNum = 100;
            if (loopSet.Count > 0)
            {
                int i = 0;
                string log = "bundle loops:";
                foreach (string[] bundles in loopSet)
                {
                    if (i >= maxPrintLoopNum)
                    {
                        break;
                    }

                    log += i + ":";
                    for (int j = 0; j < bundles.Length + 1; j++)
                    {
                        string bundleName = bundles[j % bundles.Length];
                        log += bundleName;
                        if (j != bundles.Length)
                        {
                            log += " -> ";
                        }
                    }

                    log += "\n";
                    i++;
                }

                throw new System.Exception(log);
            }
            else
            {
                throw new System.Exception("检查完毕，无循环依赖");
            }
        }

        static void SearchLoopByManifest(string bundleName, List<string> stack, HashSet<string> searchedNodeSet, Dictionary<string, HashSet<string>> dependenceMap, HashSet<string[]> loopSet)
        {
            if (string.IsNullOrEmpty(bundleName))
            {
                return;
            }

            int index = stack.IndexOf(bundleName);
            if (index < 0) //bundleName不在stack里，没形成环
            {
                if (!searchedNodeSet.Contains(bundleName)) //必须之前没遍历过这个结点
                {
                    searchedNodeSet.Add(bundleName);
                    stack.Add(bundleName);
                    HashSet<string> dependencies = null;
                    dependenceMap.TryGetValue(bundleName, out dependencies);

                    if (dependencies == null)
                    {
                        throw new System.Exception("dependencies is null: " + bundleName);
                    }

                    //遍历更深的结点
                    foreach (var d in dependencies)
                    {
                        SearchLoopByManifest(d, stack, searchedNodeSet, dependenceMap, loopSet);
                    }

                    //这里一定要移除，stack记录当前遍历到的结点，当前的bundleName已经遍历过了，所以要移除
                    stack.Remove(bundleName);
                }
            }
            else  //存在环，记录到loopSet里
            {
                string[] loop = new string[stack.Count - index];
                for (int i = index; i < stack.Count; i++)
                {
                    loop[i - index] = stack[i];
                }
                loopSet.Add(loop);
            }
        }

        [MenuItem("HaoFangTools/GameAssetBundle/Validation/检查资源文件名是否合法")]
        static public void CheckFileName()
        {
            string targetPath = Application.dataPath + "/" + PathUtility.LOCAL_ABR_PATH;
            if (Directory.Exists(targetPath) == false)
            {
                Debug.LogError("LOCAL_ABR_PATH文件夹不存在");
                return;
            }

            string[] files = Directory.GetFiles(targetPath, "*.*", SearchOption.AllDirectories);
            int i = 0;
            int count = files.Length;
            string info = string.Empty;
            foreach (var v in files)
            {
                i++;
                if (v.Contains(" ") == true)
                {
                    Debug.LogError(StringExtensions.Format("{0}包含空格", v));
                    info = StringExtensions.Format("处理进度{0} / {1}", i, count);
                    EditorUtility.DisplayProgressBar("检查文件名", info, (float)i / (float)count);
                    continue;
                }
                if (IsChineseString(v) == true)
                {
                    Debug.LogError(StringExtensions.Format("{0}包含中文", v));
                    info = StringExtensions.Format("处理进度{0} / {1}", i, count);
                    EditorUtility.DisplayProgressBar("检查文件名", info, (float)i / (float)count);
                    continue;
                }
                info = StringExtensions.Format("处理进度{0} / {1}", i, count);
                EditorUtility.DisplayProgressBar("检查文件名", info, (float)i / (float)count);
            }

            EditorUtility.ClearProgressBar();

            EditorUtility.DisplayDialog("完成", "检查完成", "确定");
        }

        static bool IsChineseString(string CString)
        {
            bool BoolValue = false;
            for (int i = 0; i < CString.Length; i++)
            {
                if (Convert.ToInt32(Convert.ToChar(CString.Substring(i, 1))) < Convert.ToInt32(Convert.ToChar(128)))
                {
                    BoolValue = false;
                }
                else
                {
                    return BoolValue = true;
                }
            }
            return BoolValue;
        }
    }
}
