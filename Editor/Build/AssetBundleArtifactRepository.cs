using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameAssetBundle.Edit
{
    [Serializable]
    public sealed class AssetBundleBuildArtifact
    {
        public string artifactId;
        public string applicationBuildName;
        public string productName;
        public string baseVersion;
        public string version;
        public int versionCode;
        public bool firstPackage;
        public BuildTarget buildTarget;
        public ScriptingImplementation scriptingImplementation;
        public bool release;
        public bool encrypted;
        public string createdAtUtc;

        public string DisplayName =>
            $"{version} | {buildTarget} | {scriptingImplementation} | {(release ? "Release" : "Debug")} | {(encrypted ? "Encrypted" : "Plain")}";
    }

    public static class AssetBundleArtifactRepository
    {
        public const string ManifestFileName = "Artifact.json";

        public static string ArtifactRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../AssetBundles/Artifacts"));
        public static string DifferenceRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../AssetBundles/Differences"));

        public static string GetArtifactId(AssetBundleApplicationProfile profile)
        {
            return GetArtifactId(profile, profile != null ? profile.CurrentVersion : null);
        }

        public static string GetArtifactId(AssetBundleApplicationProfile profile, string version)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrWhiteSpace(version))
                throw new InvalidOperationException("AssetBundle artifact version is empty.");

            return CombineId(
                SanitizeSegment(profile.BuildName),
                GetPlatformName(profile.BuildTarget),
                profile.ScriptingImplementation.ToString(),
                profile.Release ? "Release" : "Debug",
                profile.EncryptAssetBundle ? "Encrypted" : "Plain",
                SanitizeSegment(version));
        }

        public static string GetBuildCachePath(AssetBundleApplicationProfile profile)
        {
            string platform = GetPlatformName(profile.BuildTarget);
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../AssetBundles/BuildCache",
                SanitizeSegment(profile.BuildName),
                platform));
        }

        public static string GetArtifactPath(string artifactId)
        {
            if (string.IsNullOrWhiteSpace(artifactId))
                throw new InvalidOperationException("AssetBundle artifact id is empty.");

            string normalizedId = artifactId.Replace('/', Path.DirectorySeparatorChar);
            string root = ArtifactRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string path = Path.GetFullPath(Path.Combine(root, normalizedId));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"AssetBundle artifact path escapes the artifact root: {artifactId}");

            return path;
        }

        public static AssetBundleBuildArtifact CreateManifest(AssetBundleApplicationProfile profile)
        {
            return new AssetBundleBuildArtifact
            {
                artifactId = GetArtifactId(profile),
                applicationBuildName = profile.BuildName,
                productName = profile.ProductName,
                baseVersion = profile.BaseVersion,
                version = profile.CurrentVersion,
                versionCode = profile.VersionCode,
                firstPackage = profile.FirstPackage,
                buildTarget = profile.BuildTarget,
                scriptingImplementation = profile.ScriptingImplementation,
                release = profile.Release,
                encrypted = profile.EncryptAssetBundle,
                createdAtUtc = DateTime.UtcNow.ToString("O"),
            };
        }

        public static void Save(AssetBundleBuildArtifact artifact)
        {
            if (artifact == null)
                throw new ArgumentNullException(nameof(artifact));

            string artifactPath = GetArtifactPath(artifact.artifactId);
            Directory.CreateDirectory(artifactPath);
            File.WriteAllText(
                Path.Combine(artifactPath, ManifestFileName),
                JsonUtility.ToJson(artifact, true));
        }

        public static AssetBundleBuildArtifact Load(string artifactId)
        {
            string artifactPath = GetArtifactPath(artifactId);
            string manifestPath = Path.Combine(artifactPath, ManifestFileName);
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("AssetBundle artifact manifest not found.", manifestPath);

            AssetBundleBuildArtifact artifact = JsonUtility.FromJson<AssetBundleBuildArtifact>(File.ReadAllText(manifestPath));
            if (artifact == null || string.IsNullOrWhiteSpace(artifact.artifactId))
                throw new InvalidDataException($"Invalid AssetBundle artifact manifest: {manifestPath}");
            if (!string.Equals(artifact.artifactId, artifactId, StringComparison.Ordinal))
                throw new InvalidDataException($"AssetBundle artifact id does not match its directory: {manifestPath}");

            ValidateRuntimeFiles(artifact);
            return artifact;
        }

        public static List<AssetBundleBuildArtifact> GetArtifacts(string applicationBuildName)
        {
            List<AssetBundleBuildArtifact> artifacts = new List<AssetBundleBuildArtifact>();
            if (string.IsNullOrWhiteSpace(applicationBuildName) || !Directory.Exists(ArtifactRoot))
                return artifacts;

            foreach (string manifestPath in Directory.GetFiles(ArtifactRoot, ManifestFileName, SearchOption.AllDirectories))
            {
                try
                {
                    AssetBundleBuildArtifact artifact = JsonUtility.FromJson<AssetBundleBuildArtifact>(File.ReadAllText(manifestPath));
                    if (artifact != null && string.Equals(artifact.applicationBuildName, applicationBuildName, StringComparison.Ordinal))
                        artifacts.Add(artifact);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Skip invalid AssetBundle artifact manifest: {manifestPath}\n{exception.Message}");
                }
            }

            return artifacts
                .OrderByDescending(artifact => artifact.createdAtUtc, StringComparer.Ordinal)
                .ToList();
        }

        public static void ValidateForApplication(AssetBundleBuildArtifact artifact, string applicationBuildName)
        {
            if (!string.Equals(artifact.applicationBuildName, applicationBuildName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"AssetBundle artifact '{artifact.artifactId}' belongs to '{artifact.applicationBuildName}', not '{applicationBuildName}'.");
            }

            ValidateRuntimeFiles(artifact);
        }

        public static bool AreDifferenceCompatible(AssetBundleBuildArtifact left, AssetBundleBuildArtifact right)
        {
            return left != null && right != null
                && string.Equals(left.applicationBuildName, right.applicationBuildName, StringComparison.Ordinal)
                && left.buildTarget == right.buildTarget
                && left.scriptingImplementation == right.scriptingImplementation
                && left.release == right.release
                && left.encrypted == right.encrypted;
        }

        public static void Stage(string artifactId)
        {
            AssetBundleBuildArtifact artifact = Load(artifactId);
            string artifactPath = GetArtifactPath(artifact.artifactId);

            AssetBundleBuilder.ClearAssetPack();
            foreach (string directory in Directory.GetDirectories(artifactPath))
            {
                string targetDirectory = Path.Combine(Application.streamingAssetsPath, Path.GetFileName(directory));
                AssetBundleBuilder.DirectoryCopy(directory, targetDirectory);
            }

            foreach (string file in Directory.GetFiles(artifactPath, "*", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(Path.GetFileName(file), ManifestFileName, StringComparison.OrdinalIgnoreCase))
                    continue;
                File.Copy(file, Path.Combine(Application.streamingAssetsPath, Path.GetFileName(file)), true);
            }

            AssetDatabase.Refresh();
        }

        private static void ValidateRuntimeFiles(AssetBundleBuildArtifact artifact)
        {
            string artifactPath = GetArtifactPath(artifact.artifactId);
            string[] requiredPaths =
            {
                Path.Combine(artifactPath, "assets"),
                Path.Combine(artifactPath, PathUtility.AssetRecordsFileName),
                Path.Combine(artifactPath, GetManifestName(artifact.buildTarget)),
                Path.Combine(artifactPath, PathUtility.VersionFilename),
            };

            foreach (string requiredPath in requiredPaths)
            {
                if (!File.Exists(requiredPath) && !Directory.Exists(requiredPath))
                    throw new FileNotFoundException("AssetBundle artifact is incomplete.", requiredPath);
            }
        }

        public static string GetManifestName(BuildTarget buildTarget)
        {
            return GetPlatformName(buildTarget) + ".json";
        }

        public static string GetPlatformName(BuildTarget buildTarget)
        {
            switch (buildTarget)
            {
                case BuildTarget.iOS:
                    return "IOS";
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                case BuildTarget.StandaloneOSX:
                case BuildTarget.StandaloneLinux64:
                    return "Windows";
                default:
                    return buildTarget.ToString();
            }
        }

        private static string CombineId(params string[] parts)
        {
            return string.Join("/", parts);
        }

        public static string SanitizeSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("AssetBundle artifact path segment cannot be empty.");

            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = new string(value.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray());
            return sanitized.Replace('/', '_').Replace('\\', '_');
        }
    }
}
