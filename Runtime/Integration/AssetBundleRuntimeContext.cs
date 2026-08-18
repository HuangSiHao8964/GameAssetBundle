using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameAssetBundle
{
    public enum AssetBundlePathType
    {
        InitData,
        Local,
        Remote,
    }

    public sealed class AssetBundleRuntimeConfig
    {
        public string AssetRecordsFileName { get; set; }
        public string AssetBundleDifferenceFileName { get; set; }
        public string MainManifestPath { get; set; }
        public Func<string, AssetBundlePathType, string> GetAssetFilePath { get; set; }
        public Func<string, AssetBundlePathType, byte[]> ReadFileBytes { get; set; }
        public Func<string, UniTask<byte[]>> GetBytesAsync { get; set; }
        public Func<string, UniTask<string>> GetTextAsync { get; set; }
        public Func<string, bool, CancellationToken, UniTask<AssetBundle>> GetAssetBundleAsync { get; set; }
        public Func<bool> HasLocalManifest { get; set; }
        public Action<string> Log { get; set; }
        public Action<string> LogWarning { get; set; }
        public Action<string> LogError { get; set; }
    }

    public static class AssetBundleRuntimeContext
    {
        private static AssetBundleRuntimeConfig config;

        public static bool IsConfigured => config != null;

        public static void Configure(AssetBundleRuntimeConfig runtimeConfig)
        {
            if (runtimeConfig == null)
                throw new ArgumentNullException(nameof(runtimeConfig));
            if (string.IsNullOrEmpty(runtimeConfig.AssetRecordsFileName))
                throw new ArgumentException("AssetRecordsFileName is required.", nameof(runtimeConfig));
            if (string.IsNullOrEmpty(runtimeConfig.AssetBundleDifferenceFileName))
                throw new ArgumentException("AssetBundleDifferenceFileName is required.", nameof(runtimeConfig));
            if (string.IsNullOrEmpty(runtimeConfig.MainManifestPath))
                throw new ArgumentException("MainManifestPath is required.", nameof(runtimeConfig));
            if (runtimeConfig.GetAssetFilePath == null || runtimeConfig.ReadFileBytes == null ||
                runtimeConfig.GetBytesAsync == null || runtimeConfig.GetTextAsync == null ||
                runtimeConfig.GetAssetBundleAsync == null || runtimeConfig.HasLocalManifest == null)
            {
                throw new ArgumentException("All AssetBundle runtime delegates are required.", nameof(runtimeConfig));
            }

            runtimeConfig.Log ??= Debug.Log;
            runtimeConfig.LogWarning ??= Debug.LogWarning;
            runtimeConfig.LogError ??= Debug.LogError;
            config = runtimeConfig;
        }

        private static AssetBundleRuntimeConfig Config
        {
            get
            {
                if (config == null)
                {
                    throw new InvalidOperationException(
                        "AssetBundleRuntimeContext is not configured. Configure it before using GameAssetBundle.");
                }

                return config;
            }
        }

        public static string AssetRecordsFileName => Config.AssetRecordsFileName;
        public static string AssetBundleDifferenceFileName => Config.AssetBundleDifferenceFileName;
        public static string MainManifestPath => Config.MainManifestPath;
        public static bool HasLocalManifest => Config.HasLocalManifest();

        public static string GetAssetFilePath(string fileName, AssetBundlePathType pathType)
        {
            return Config.GetAssetFilePath(fileName, pathType);
        }

        internal static byte[] ReadFileBytes(string fileName, AssetBundlePathType pathType)
        {
            return Config.ReadFileBytes(fileName, pathType);
        }

        internal static UniTask<byte[]> GetBytesAsync(string path)
        {
            return Config.GetBytesAsync(path);
        }

        internal static UniTask<string> GetTextAsync(string path)
        {
            return Config.GetTextAsync(path);
        }

        internal static UniTask<AssetBundle> GetAssetBundleAsync(
            string path,
            bool isEncrypted,
            CancellationToken cancellationToken = default)
        {
            return Config.GetAssetBundleAsync(path, isEncrypted, cancellationToken);
        }

        internal static void Log(string message)
        {
            Config.Log(message);
        }

        internal static void LogWarning(string message)
        {
            Config.LogWarning(message);
        }

        internal static void LogError(string message)
        {
            Config.LogError(message);
        }

        internal static void LogFormat(string format, params object[] args)
        {
            Config.Log(string.Format(format, args));
        }

        internal static void LogErrorFormat(string format, params object[] args)
        {
            Config.LogError(string.Format(format, args));
        }
    }
}
