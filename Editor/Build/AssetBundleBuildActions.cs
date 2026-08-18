using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

#if USE_TTSDK
using TTSDK.UNBridgeLib.LitJson;
#else
using LitJson;
#endif

namespace GameAssetBundle.Edit
{
    [Serializable]
    public sealed class AssetBundleDifferencePackage
    {
        public string applicationBuildName;
        public string baseArtifactId;
        public string currentArtifactId;
        public string outputPath;
        public int changedBundleCount;
        public long totalBytes;

        public string DisplayName =>
            $"{applicationBuildName}: {baseArtifactId} -> {currentArtifactId}";
    }

    public sealed class AssetBundleBuildResult
    {
        public string applicationBuildName;
        public string artifactId;
        public string artifactPath;
        public string version;

        public string DisplayName =>
            $"{applicationBuildName}: {artifactId}";
    }

    public static class AssetBundleBuildActions
    {
        private static Action<AssetBundleApplicationProfile, bool> s_BeforeBuild;
        private static Action<AssetBundleApplicationProfile, string> s_AfterBuild;
        private static Func<byte[], byte[]> s_Encrypt;

        public static bool HasEncryptionCallback => s_Encrypt != null;

        public static void RegisterBuildHooks(
            Action<AssetBundleApplicationProfile> beforeBuild,
            Action<AssetBundleApplicationProfile, string> afterBuild)
        {
            RegisterBuildHooks(
                (profile, _) => beforeBuild?.Invoke(profile),
                afterBuild);
        }

        public static void RegisterBuildHooks(
            Action<AssetBundleApplicationProfile, bool> beforeBuild,
            Action<AssetBundleApplicationProfile, string> afterBuild)
        {
            s_BeforeBuild = beforeBuild;
            s_AfterBuild = afterBuild;
        }

        public static void RegisterEncryptionCallback(Func<byte[], byte[]> encrypt)
        {
            s_Encrypt = encrypt;
        }

        [MenuItem("HaoFangTools/GameAssetBundle/Build/仅导出资源", false, 10)]
        public static void BuildResourcesFromActiveProfile()
        {
            AssetBundleBuildSetting setting = LoadSetting();
            AssetBundleBuildResult result = BuildResources(setting.ActiveApplicationBuildName);
            Debug.Log($"AssetBundle resource export completed: {result.artifactPath}");
        }

        [MenuItem("HaoFangTools/GameAssetBundle/Build/导出资源并生成差异包", false, 11)]
        public static void BuildResourcesAndDifferenceFromActiveProfile()
        {
            AssetBundleBuildSetting setting = LoadSetting();
            AssetBundleDifferencePackage result = BuildResourcesAndDifference(setting.ActiveApplicationBuildName);
            Debug.Log($"AssetBundle resource and difference package completed: {result.outputPath}");
        }

        [MenuItem("HaoFangTools/GameAssetBundle/Build/仅生成差异包", false, 12)]
        public static void BuildDifferenceFromActiveProfile()
        {
            AssetBundleBuildSetting setting = LoadSetting();
            AssetBundleDifferencePackage result = BuildDifference(setting.ActiveApplicationBuildName);
            Debug.Log($"AssetBundle difference package completed: {result.outputPath}");
        }

        public static AssetBundleDifferencePackage BuildResourcesAndDifference(string applicationBuildName)
        {
            return BuildResourcesAndDifference(applicationBuildName, true);
        }

        public static AssetBundleDifferencePackage BuildResourcesAndDifference(
            string applicationBuildName,
            bool generateHybridWrap)
        {
            AssetBundleBuildSetting setting = LoadSetting();
            setting.ValidateProfiles();
            AssetBundleApplicationProfile profile = setting.GetRequiredProfile(applicationBuildName);
            EnsureEncryptionCallback(profile);
            AssetBundleBuildArtifact baseArtifact = LoadBaseArtifact(profile);
            AssetBundleBuildArtifact artifact = BuildResourcesInternal(
                setting,
                profile,
                baseArtifact,
                generateHybridWrap);

            return CreateDifference(
                profile,
                AssetBundleArtifactRepository.GetArtifactId(profile, profile.BaseVersion),
                artifact.artifactId);
        }

        public static AssetBundleBuildResult BuildResources(string applicationBuildName)
        {
            return BuildResources(applicationBuildName, true);
        }

        public static AssetBundleBuildResult BuildResources(
            string applicationBuildName,
            bool generateHybridWrap)
        {
            AssetBundleBuildSetting setting = LoadSetting();
            setting.ValidateProfiles();
            AssetBundleApplicationProfile profile = setting.GetRequiredProfile(applicationBuildName);
            EnsureEncryptionCallback(profile);
            AssetBundleBuildArtifact artifact = BuildResourcesInternal(
                setting,
                profile,
                null,
                generateHybridWrap);

            return new AssetBundleBuildResult
            {
                applicationBuildName = profile.BuildName,
                artifactId = artifact.artifactId,
                artifactPath = AssetBundleArtifactRepository.GetArtifactPath(artifact.artifactId),
                version = artifact.version,
            };
        }

        private static AssetBundleBuildArtifact BuildResourcesInternal(
            AssetBundleBuildSetting setting,
            AssetBundleApplicationProfile profile,
            AssetBundleBuildArtifact baseArtifact,
            bool generateHybridWrap)
        {

            bool versionUpdated = false;
            bool artifactSaved = false;
            try
            {
                if (profile.IncrementResourceVersion)
                {
                    profile.UpdateVersion(1);
                    versionUpdated = true;
                }

                AssetBundleBuildArtifact plannedArtifact = AssetBundleArtifactRepository.CreateManifest(profile);
                if (baseArtifact != null)
                    ValidateDifferencePair(baseArtifact, plannedArtifact);
                ApplyBuildEnvironment(profile);
                EditorUtility.SetDirty(setting);
                AssetDatabase.SaveAssetIfDirty(setting);
                s_BeforeBuild?.Invoke(profile, generateHybridWrap);

                string artifactPath = AssetBundleBuilder.Build(profile, profile.Release);
                s_AfterBuild?.Invoke(profile, artifactPath);
                AssetBundleBuilder.WriteVersionFile(artifactPath, profile.CurrentVersion);
                if (profile.EncryptAssetBundle)
                {
                    AssetBundleBuilder.TransformBuiltBundles(
                        artifactPath,
                        s_Encrypt);
                }

                AssetBundleBuildArtifact artifact = plannedArtifact;
                AssetBundleArtifactRepository.Save(artifact);
                artifactSaved = true;
                AssetBundleArtifactRepository.Stage(artifact.artifactId);
                AssetDatabase.Refresh();

                return artifact;
            }
            catch
            {
                if (versionUpdated && !artifactSaved)
                {
                    profile.UpdateVersion(-1);
                    EditorUtility.SetDirty(setting);
                    AssetDatabase.SaveAssetIfDirty(setting);
                }

                throw;
            }
        }

        private static AssetBundleBuildArtifact LoadBaseArtifact(AssetBundleApplicationProfile profile)
        {
            string baseArtifactId = AssetBundleArtifactRepository.GetArtifactId(profile, profile.BaseVersion);
            AssetBundleBuildArtifact baseArtifact = AssetBundleArtifactRepository.Load(baseArtifactId);
            AssetBundleArtifactRepository.ValidateForApplication(baseArtifact, profile.BuildName);
            return baseArtifact;
        }

        private static void ValidateDifferencePair(
            AssetBundleBuildArtifact baseArtifact,
            AssetBundleBuildArtifact currentArtifact)
        {
            if (!AssetBundleArtifactRepository.AreDifferenceCompatible(baseArtifact, currentArtifact))
                throw new InvalidOperationException("AssetBundle artifacts are not compatible for difference generation.");
            if (string.Equals(baseArtifact.artifactId, currentArtifact.artifactId, StringComparison.Ordinal))
                throw new InvalidOperationException("AssetBundle base and current artifacts are identical.");
        }

        public static AssetBundleDifferencePackage BuildDifference(string applicationBuildName)
        {
            return BuildDifference(applicationBuildName, null);
        }

        public static AssetBundleDifferencePackage BuildDifference(
            string applicationBuildName,
            string currentArtifactId)
        {
            AssetBundleBuildSetting setting = LoadSetting();
            setting.ValidateProfiles();
            AssetBundleApplicationProfile profile = setting.GetRequiredProfile(applicationBuildName);
            string targetArtifactId = string.IsNullOrWhiteSpace(currentArtifactId)
                ? AssetBundleArtifactRepository.GetArtifactId(profile)
                : currentArtifactId;
            AssetBundleBuildArtifact currentArtifact = AssetBundleArtifactRepository.Load(targetArtifactId);
            AssetBundleArtifactRepository.ValidateForApplication(currentArtifact, profile.BuildName);

            return CreateDifference(
                profile,
                AssetBundleArtifactRepository.GetArtifactId(profile, profile.BaseVersion),
                currentArtifact.artifactId);
        }

        private static AssetBundleDifferencePackage CreateDifference(
            AssetBundleApplicationProfile profile,
            string baseArtifactId,
            string currentArtifactId)
        {
            AssetBundleBuildArtifact baseArtifact = AssetBundleArtifactRepository.Load(baseArtifactId);
            AssetBundleBuildArtifact currentArtifact = AssetBundleArtifactRepository.Load(currentArtifactId);
            AssetBundleArtifactRepository.ValidateForApplication(baseArtifact, profile.BuildName);
            AssetBundleArtifactRepository.ValidateForApplication(currentArtifact, profile.BuildName);
            ValidateDifferencePair(baseArtifact, currentArtifact);

            Dictionary<string, string> currentBundles = ReadBundleMap(currentArtifact);
            Dictionary<string, string> baseBundles = ReadBundleMap(baseArtifact);
            Dictionary<string, string> difference = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> pair in currentBundles)
            {
                if (!baseBundles.TryGetValue(pair.Key, out string oldPath) || !string.Equals(oldPath, pair.Value, StringComparison.Ordinal))
                    difference.Add(pair.Key, pair.Value);
            }

            if (difference.Count == 0)
                throw new InvalidOperationException("The two AssetBundle versions have no difference.");

            string outputPath = GetDifferencePath(profile, baseArtifact.version, currentArtifact.version);
            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);
            Directory.CreateDirectory(outputPath);

            Dictionary<string, long> sizes = new Dictionary<string, long>();
            string currentPath = AssetBundleArtifactRepository.GetArtifactPath(currentArtifact.artifactId);
            foreach (KeyValuePair<string, string> pair in difference)
            {
                string sourcePath = ResolveChildPath(currentPath, pair.Value);
                string targetPath = ResolveChildPath(outputPath, pair.Value);
                CopyFile(sourcePath, targetPath, pair.Value, sizes);
            }

            string manifestName = AssetBundleArtifactRepository.GetManifestName(currentArtifact.buildTarget);
            CopyFileFromRoot(currentPath, outputPath, manifestName, sizes);
            CopyFileFromRoot(currentPath, outputPath, PathUtility.AssetRecordsFileName, sizes);
            CopyFileFromRoot(currentPath, outputPath, PathUtility.VersionFilename, sizes);
            CopyOptionalFileFromRoot(currentPath, outputPath, "AOTAssemblies.json", sizes);
            CopyOptionalDirectory(currentPath, outputPath, "HotUpdate", sizes);

            string differencePath = Path.Combine(outputPath, PathUtility.AssetBundleDifference);
            File.WriteAllText(differencePath, JsonMapper.ToJson(difference));
            sizes[PathUtility.AssetBundleDifference] = new FileInfo(differencePath).Length;

            string sizePath = Path.Combine(outputPath, "size.json");
            File.WriteAllText(sizePath, JsonMapper.ToJson(sizes));

            long totalBytes = 0;
            foreach (long size in sizes.Values)
                totalBytes += size;

            return new AssetBundleDifferencePackage
            {
                applicationBuildName = profile.BuildName,
                baseArtifactId = baseArtifact.artifactId,
                currentArtifactId = currentArtifact.artifactId,
                outputPath = outputPath,
                changedBundleCount = difference.Count,
                totalBytes = totalBytes,
            };
        }

        private static AssetBundleBuildSetting LoadSetting()
        {
            AssetBundleBuildSetting setting = AssetBundleBuildSetting.Load();
            if (setting == null)
                throw new InvalidOperationException(
                    $"AssetBundle build setting not found: {AssetBundleBuildSetting.AssetPath}");
            return setting;
        }

        private static void EnsureEncryptionCallback(AssetBundleApplicationProfile profile)
        {
            if (profile.EncryptAssetBundle && s_Encrypt == null)
            {
                throw new InvalidOperationException(
                    $"AssetBundle encryption is enabled for '{profile.BuildName}', but no encryption callback is registered.");
            }
        }

        private static void ApplyBuildEnvironment(AssetBundleApplicationProfile profile)
        {
            BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(profile.BuildTarget);
            if (EditorUserBuildSettings.activeBuildTarget != profile.BuildTarget
                && !EditorUserBuildSettings.SwitchActiveBuildTarget(targetGroup, profile.BuildTarget))
            {
                throw new InvalidOperationException($"Failed to switch build target to {profile.BuildTarget}.");
            }

            PlayerSettings.SetScriptingBackend(targetGroup, profile.ScriptingImplementation);
            if (!string.IsNullOrWhiteSpace(profile.ProductName))
                PlayerSettings.productName = profile.ProductName;
        }

        private static Dictionary<string, string> ReadBundleMap(AssetBundleBuildArtifact artifact)
        {
            string recordPath = Path.Combine(
                AssetBundleArtifactRepository.GetArtifactPath(artifact.artifactId),
                PathUtility.AssetRecordsFileName);
            AssetBundleRecord record = new AssetBundleRecord();
            record.LoadRecord(File.ReadAllBytes(recordPath));
            if (record.bundleName == null || record.md5 == null || record.bundleName.Length != record.md5.Length)
                throw new InvalidDataException($"Invalid AssetBundle record: {recordPath}");

            Dictionary<string, string> map = new Dictionary<string, string>();
            for (int i = 0; i < record.bundleName.Length; i++)
                map.Add(record.bundleName[i], record.md5[i]);
            return map;
        }

        private static string GetDifferencePath(
            AssetBundleApplicationProfile profile,
            string baseVersion,
            string currentVersion)
        {
            return Path.Combine(
                AssetBundleArtifactRepository.DifferenceRoot,
                AssetBundleArtifactRepository.SanitizeSegment(profile.BuildName),
                AssetBundleArtifactRepository.GetPlatformName(profile.BuildTarget),
                profile.ScriptingImplementation.ToString(),
                profile.Release ? "Release" : "Debug",
                profile.EncryptAssetBundle ? "Encrypted" : "Plain",
                AssetBundleArtifactRepository.SanitizeSegment(baseVersion + "-" + currentVersion));
        }

        private static void CopyFileFromRoot(
            string sourceRoot,
            string targetRoot,
            string relativePath,
            Dictionary<string, long> sizes)
        {
            CopyFile(
                ResolveChildPath(sourceRoot, relativePath),
                ResolveChildPath(targetRoot, relativePath),
                relativePath,
                sizes);
        }

        private static void CopyOptionalFileFromRoot(
            string sourceRoot,
            string targetRoot,
            string relativePath,
            Dictionary<string, long> sizes)
        {
            string sourcePath = ResolveChildPath(sourceRoot, relativePath);
            if (File.Exists(sourcePath))
                CopyFile(sourcePath, ResolveChildPath(targetRoot, relativePath), relativePath, sizes);
        }

        private static void CopyOptionalDirectory(
            string sourceRoot,
            string targetRoot,
            string relativeDirectory,
            Dictionary<string, long> sizes)
        {
            string sourcePath = ResolveChildPath(sourceRoot, relativeDirectory);
            if (!Directory.Exists(sourcePath))
                return;

            foreach (string file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
            {
                string relativePath = file.Substring(sourceRoot.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                CopyFile(file, ResolveChildPath(targetRoot, relativePath), relativePath, sizes);
            }
        }

        private static void CopyFile(
            string sourcePath,
            string targetPath,
            string relativePath,
            Dictionary<string, long> sizes)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("AssetBundle difference source file not found.", sourcePath);

            string targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory))
                Directory.CreateDirectory(targetDirectory);
            File.Copy(sourcePath, targetPath, true);
            sizes[relativePath.Replace("\\", "/")] = new FileInfo(targetPath).Length;
        }

        private static string ResolveChildPath(string rootPath, string relativePath)
        {
            string root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(root, normalized));
            if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"AssetBundle difference path escapes its root: {relativePath}");
            return fullPath;
        }
    }
}
