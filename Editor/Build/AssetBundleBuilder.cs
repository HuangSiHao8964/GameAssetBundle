using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Path = System.IO.Path;

namespace GameAssetBundle.Edit
{
    public static class AssetBundleBuilder
    {
        private static readonly HashSet<string> PreservedStreamingAssetFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "LocalRoomID.txt",
            "LocalRoomID.txt.meta",
        };

        private static readonly string[] IgnoredBuildFileExtensions = { ".meta", ".manifest" };
        private static int s_PathIndex;
        private static string[] s_PathArray;

        public static string Build(AssetBundleApplicationProfile profile, bool disableWriteTypeTree)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            string atlasConfigPath = PathUtility.GetARBPath("Config/AtlasConfig.json.txt");
            if (File.Exists(atlasConfigPath))
            {
                File.Delete(atlasConfigPath);
                File.Delete(atlasConfigPath + ".meta");
            }

            if (profile.CleanPack)
                ClearAssetBundleNames();

            BuildAssetBundleNames();

            string outputPath = GetBuildOutputPath(profile);
            if (Directory.Exists(outputPath) && profile.CleanPack)
                Directory.Delete(outputPath, true);

            BuildAssetBundleOptions options = BuildAssetBundleOptions.ChunkBasedCompression;
            if (profile.CleanPack)
                options |= BuildAssetBundleOptions.ForceRebuildAssetBundle;
            if (disableWriteTypeTree)
                options |= BuildAssetBundleOptions.DisableWriteTypeTree;

            Directory.CreateDirectory(outputPath);
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(outputPath, options, EditorUserBuildSettings.activeBuildTarget);
            if (manifest == null)
                throw new InvalidOperationException($"AssetBundle build failed for {EditorUserBuildSettings.activeBuildTarget}.");

            string artifactId = AssetBundleArtifactRepository.GetArtifactId(profile);
            string artifactPath = AssetBundleArtifactRepository.GetArtifactPath(artifactId);
            GenerateAssetRecordsFile(profile.EncryptAssetBundle, outputPath, artifactPath);

            string sourceManifest = Path.Combine(artifactPath, AssetBundleArtifactRepository.GetPlatformName(profile.BuildTarget));
            string targetManifest = Path.Combine(artifactPath, AssetBundleArtifactRepository.GetManifestName(profile.BuildTarget));
            if (File.Exists(targetManifest))
                File.Delete(targetManifest);
            File.Move(sourceManifest, targetManifest);

            Debug.LogErrorFormat("AssetBundle build for {0} completed.", EditorUserBuildSettings.activeBuildTarget);
            return artifactPath;
        }

        public static void WriteVersionFile(string rootPath, string version)
        {
            FileUtility.WriteTextToFile(Path.Combine(rootPath, PathUtility.VersionFilename), version ?? string.Empty);
        }

        public static void TransformBuiltBundles(string rootPath, Func<byte[], byte[]> transform)
        {
            if (transform == null)
                throw new ArgumentNullException(nameof(transform));

            string assetBundlePath = Path.Combine(rootPath, "assets");
            string[] assetBundleFiles = Directory.GetFiles(assetBundlePath, "*", SearchOption.AllDirectories)
                .Where(file => !file.EndsWith(".meta") && !file.EndsWith(".manifest"))
                .ToArray();

            foreach (string file in assetBundleFiles)
            {
                byte[] transformedData = transform(File.ReadAllBytes(file));
                File.WriteAllBytes(file, transformedData);
                Debug.Log($"Transformed AssetBundle: {file}");
            }
        }

        public static void ClearAssetPack()
        {
            string[] files = Directory.GetFiles(Application.streamingAssetsPath, "*.*", SearchOption.TopDirectoryOnly);
            foreach (string file in files)
            {
                if (ShouldPreserveStreamingAssetFile(file))
                    continue;

                string extension = Path.GetExtension(file);
                if (extension.Equals(".txt") || extension.Equals(".bat") || extension.Equals(".exe"))
                    continue;

                File.Delete(file);
            }

            string assetPath = Application.streamingAssetsPath + "/assets";
            if (Directory.Exists(assetPath))
                Directory.Delete(assetPath, true);

            string excelPath = Application.streamingAssetsPath + "/Excels";
#if !DEBUG
            if (Directory.Exists(excelPath))
                Directory.Delete(excelPath, true);
#endif

            string pbPath = Application.streamingAssetsPath + "/PB";
            if (Directory.Exists(pbPath))
                Directory.Delete(pbPath, true);
        }

        public static void DirectoryCopy(string sourceDirectory, string targetDirectory)
        {
            string sourceRoot = Path.GetFullPath(sourceDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string targetRoot = Path.GetFullPath(targetDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(targetRoot))
                Directory.CreateDirectory(targetRoot);

            foreach (string folderPath in Directory.GetDirectories(sourceRoot, "*", SearchOption.AllDirectories))
            {
                string relativePath = folderPath.Substring(sourceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string targetFolder = Path.Combine(targetRoot, relativePath);
                if (!Directory.Exists(targetFolder))
                    Directory.CreateDirectory(targetFolder);
            }

            foreach (string filePath in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                string relativePath = filePath.Substring(sourceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string targetPath = Path.Combine(targetRoot, relativePath);
                File.Copy(filePath, targetPath, true);
            }
        }

        [MenuItem("HaoFangTools/GameAssetBundle/Build/Build AB Names", false, 0)]
        public static void BuildAssetBundleNames()
        {
            AssetDatabase.RemoveUnusedAssetBundleNames();
            AssetDatabase.Refresh();
            List<string> assetBundlePaths = new List<string>();
            AssetBundleCollectData.CollectAbPath(ref assetBundlePaths);

            foreach (string path in assetBundlePaths)
                SetAssetBundleName(path);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("HaoFangTools/GameAssetBundle/Build/Clear AB Names", false, 1)]
        public static void ClearAssetBundleNames()
        {
            string resourcePath = Application.dataPath;
            s_PathArray = Directory.GetFiles(resourcePath, "*.*", SearchOption.AllDirectories);
            ApplyToPaths((progress, path) =>
            {
                if (path.Contains(".meta") || path.Contains(".vscode") || path.Contains(".cs"))
                    return;

                ClearAssetBundleName(path);
                EditorUtility.DisplayProgressBar("ClearFile", "ClearAllFile ... ", progress);
            });
            EditorUtility.ClearProgressBar();

            s_PathArray = Directory.GetDirectories(resourcePath, "*", SearchOption.AllDirectories);
            ApplyToPaths((progress, path) =>
            {
                if (path.Contains(".meta") || path.Contains(".vscode"))
                    return;

                ClearAssetBundleName(path);
                EditorUtility.DisplayProgressBar("ClearDirectory", "ClearAllDirectory ... ", progress);
            });
            EditorUtility.ClearProgressBar();
            AssetDatabase.RemoveUnusedAssetBundleNames();
            AssetDatabase.Refresh();
        }

        private static void GenerateAssetRecordsFile(bool encryptAssetBundle, string outputPath, string artifactPath)
        {
            if (Directory.Exists(artifactPath))
                Directory.Delete(artifactPath, true);
            DirectoryCopy(outputPath, artifactPath);
            ClearAllManifestFiles(artifactPath);

            string[] files = Directory.GetFiles(
                Path.Combine(artifactPath, "assets"),
                "*.*",
                SearchOption.AllDirectories);
            Dictionary<string, string> md5Map = new Dictionary<string, string>();
            string rootToken = artifactPath.Replace("\\", "/").TrimEnd('/') + "/";

            foreach (string sourceFile in files)
            {
                string file = sourceFile.Replace("\\", "/");
                if (Path.GetExtension(file).Equals(".manifest"))
                    continue;

                string md5 = FileUtility.GetFileMD5Code(file);
                string targetPath = string.IsNullOrEmpty(PathUtility.AssetBundleExt)
                    ? file + md5
                    : file.Replace(PathUtility.AssetBundleExt, md5 + PathUtility.AssetBundleExt);
                File.Move(file, targetPath);
                md5Map.Add(file.Replace(rootToken, string.Empty), targetPath.Replace(rootToken, string.Empty));
            }

            AssetBundleCollectData.PackCollect(true);
            Dictionary<string, AssetRecord> assetRecords = AssetBundleCollectData.AssetRecordInfoDict;
            AssetBundleRecord assetBundleRecord = new AssetBundleRecord();
            assetBundleRecord.IsEncrypted = encryptAssetBundle;
            Dictionary<string, int> bundleIndices = new Dictionary<string, int>();
            List<string> bundleNames = new List<string>();
            List<string> md5Names = new List<string>();

            foreach (KeyValuePair<string, AssetRecord> pair in assetRecords)
            {
                string bundleName = pair.Value.ABN.ToLower();
                if (bundleNames.Contains(bundleName))
                    continue;

                bundleIndices.Add(bundleName, bundleNames.Count);
                bundleNames.Add(bundleName);
                md5Names.Add(md5Map[bundleName]);
            }

            assetBundleRecord.bundleName = bundleNames.ToArray();
            assetBundleRecord.md5 = md5Names.ToArray();
            foreach (KeyValuePair<string, AssetRecord> pair in assetRecords)
                assetBundleRecord.assetMap.Add(pair.Key, bundleIndices[pair.Value.ABN.ToLower()]);

            assetBundleRecord.SaveRecord(
                Path.Combine(artifactPath, PathUtility.AssetRecordsFileName));
        }

        private static string GetBuildOutputPath(AssetBundleApplicationProfile profile)
        {
            return AssetBundleArtifactRepository.GetBuildCachePath(profile);
        }

        private static void ClearAssetBundleName(string sourcePath)
        {
            string path = sourcePath.Replace(Application.dataPath, "Assets").Replace("\\", "/");
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer != null)
                importer.assetBundleName = null;
        }

        private static void ApplyToPaths(Action<float, string> action)
        {
            s_PathIndex = 0;
            int pathCount = s_PathArray.Length;
            if (pathCount <= 0)
                return;

            foreach (string path in s_PathArray)
            {
                s_PathIndex++;
                action?.Invoke((float)s_PathIndex / pathCount, path);
            }
        }

        private static void SetAssetBundleName(string sourcePath)
        {
            if (Path.GetExtension(sourcePath).Equals(".meta"))
                return;

            string path = sourcePath.Replace(Application.dataPath, "Assets").Replace("\\", "/");
            if (path.Contains("LuaScript"))
                path = path.Replace("LuaScript", "Primitives");

            AssetImporter importer = AssetImporter.GetAtPath(path);
            Debug.LogFormat("SetABName:{0}", path);
            int extensionIndex = importer.assetPath.LastIndexOf('.');
            string bundleName = extensionIndex == -1
                ? importer.assetPath.Split('.')[0].Replace("assets/", string.Empty)
                : importer.assetPath.Substring(0, extensionIndex).Replace("assets/", string.Empty);
            string targetBundleName = bundleName + PathUtility.AssetBundleExt;
            if (!string.Equals(importer.assetBundleName, targetBundleName, StringComparison.Ordinal))
                importer.assetBundleName = targetBundleName;
        }

        private static void ClearAllManifestFiles(string rootPath)
        {
            foreach (string extension in IgnoredBuildFileExtensions)
            {
                string[] files = Directory.GetFiles(
                    rootPath,
                    "*" + extension,
                    SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    if (!ShouldPreserveStreamingAssetFile(file))
                        File.Delete(file);
                }
            }

            Debug.Log("<color=#00ee00ff>Success to clear manifest files.</color>");
        }

        private static bool ShouldPreserveStreamingAssetFile(string filePath)
        {
            return !string.IsNullOrEmpty(filePath) && PreservedStreamingAssetFiles.Contains(Path.GetFileName(filePath));
        }
    }
}
