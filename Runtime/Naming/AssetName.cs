using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameAssetBundle
{
    public static class AssetName
    {
        private static readonly Dictionary<Type, string[]> ExtensionDict = new Dictionary<Type, string[]>();
        private static readonly Dictionary<Type, Dictionary<string, string>> AssetNameDict = new Dictionary<Type, Dictionary<string, string>>();

        public static void AddExtensionDict(Type type, params string[] extensions)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (extensions == null || extensions.Length == 0)
                throw new ArgumentException("At least one asset extension is required.", nameof(extensions));

            string[] extensionCopy = new string[extensions.Length];
            Array.Copy(extensions, extensionCopy, extensions.Length);
            ExtensionDict[type] = extensionCopy;
            AssetNameDict.Remove(type);
        }

        static string[] GetExtensionSet(Type type)
        {
            if (ExtensionDict.TryGetValue(type, out string[] extensionSet))
            {
                return extensionSet;
            }
            else
            {
                Debug.LogError("ExtensionDict not found type:" + type.Name);
            }
            return null;
        }



        const string resNameFormat = "{0}.{1}";
        public static string AssetNameFormat(string assetName, string ext)
        {
            return string.Format(resNameFormat, assetName, ext);
        }
        public static bool CheckAssetName(string name, Type type, out string formatResult)
        {
            Dictionary<string, string> dict = null;
            if (AssetNameDict.TryGetValue(type, out dict))
            {
                if (dict.TryGetValue(name, out formatResult))
                {
                    //UnityEngine.Debug.LogError("======================AssetNameDict生效======================");
                    return true;
                }
            }
            else
            {
                dict = new Dictionary<string, string>();
                AssetNameDict.Add(type, dict);
            }
            formatResult = string.Empty;
            string[] extension = GetExtensionSet(type);
            if (extension == null)
                return false;
            for (int i = 0; i < extension.Length; i++)
            {
                formatResult = AssetNameFormat(name, extension[i]);
                if (AssetBundleManager.Instance.CheckAsset(formatResult))
                {
                    dict.Add(name, formatResult);
                    return true;
                }
            }

            formatResult = string.Empty;
            return false;
        }

        public static string GetAssetName(string name, Type type)
        {
            var formatResult = string.Empty;
            Dictionary<string, string> dict = null;
            if (AssetNameDict.TryGetValue(type, out dict))
            {
                if (dict.TryGetValue(name, out formatResult))
                {
                    //UnityEngine.Debug.LogError("======================AssetNameDict生效======================");
                    return formatResult;
                }
            }
            else
            {
                dict = new Dictionary<string, string>();
                AssetNameDict.Add(type, dict);
            }
            string[] extension = GetExtensionSet(type);
            if (extension == null)
                return string.Empty;
            for (int i = 0; i < extension.Length; i++)
            {
                formatResult = AssetNameFormat(name, extension[i]);
                if (AssetBundleManager.Instance.CheckAsset(formatResult))
                {
                    dict.Add(name, formatResult);
                    return formatResult;
                }
            }

            return string.Empty;
        }
    }
}
