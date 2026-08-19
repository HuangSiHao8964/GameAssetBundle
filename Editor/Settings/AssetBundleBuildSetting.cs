using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameAssetBundle.Edit
{
    public enum AssetBundleBuildSource
    {
        None,
        BuildFromProfile,
        ExistingArtifact,
    }

    [Serializable]
    public sealed class AssetBundleApplicationProfile
    {
        [SerializeField] private string m_buildName = "Default";
        [SerializeField] private string m_productName = string.Empty;
        [SerializeField] private BuildTarget m_buildTarget = BuildTarget.Android;
        [SerializeField] private ScriptingImplementation m_scriptingImplementation = ScriptingImplementation.IL2CPP;
        [SerializeField] private bool m_release;
        [SerializeField] private bool m_encryptAssetBundle = true;
        [SerializeField] private bool m_cleanPack = true;
        [SerializeField] private bool m_incrementResourceVersion = true;
        [SerializeField] private bool m_firstPackage;
        [SerializeField] private string m_baseVersion = string.Empty;
        [SerializeField] private string m_currentVersion = string.Empty;
        [SerializeField] private int m_versionCode = 1;

        public string BuildName => m_buildName;
        public string ProductName => m_productName;
        public BuildTarget BuildTarget => m_buildTarget;
        public ScriptingImplementation ScriptingImplementation => m_scriptingImplementation;
        public bool Release => m_release;
        public bool EncryptAssetBundle => m_encryptAssetBundle;
        public bool CleanPack => m_cleanPack;
        public bool IncrementResourceVersion => m_incrementResourceVersion;
        public bool FirstPackage => m_firstPackage;
        public string BaseVersion => m_baseVersion;
        public string CurrentVersion => string.IsNullOrEmpty(m_currentVersion) ? m_baseVersion : m_currentVersion;
        public int VersionCode => m_versionCode;

        public void UpdateVersion(int offset)
        {
            if (string.IsNullOrEmpty(m_currentVersion))
                return;
            if (m_currentVersion.Equals(m_baseVersion, StringComparison.Ordinal) && offset < 0)
                return;

            string[] parts = m_currentVersion.Split('.');
            if (parts.Length == 0 || !int.TryParse(parts[parts.Length - 1], out int revision))
                throw new InvalidOperationException($"Invalid AssetBundle version: {m_currentVersion}");

            parts[parts.Length - 1] = (revision + offset).ToString();
            m_currentVersion = string.Join(".", parts);
        }

        public void UpdateVersionCode(int offset)
        {
            m_versionCode += offset;
        }

        public void ResetForFirstPackage()
        {
            m_firstPackage = true;
            m_currentVersion = string.Empty;
            m_versionCode = 0;
        }

        public void CompleteFirstPackage()
        {
            m_firstPackage = false;
        }
    }

    public sealed class AssetBundleBuildSetting : ScriptableObject
    {
        public const string AssetPath = GameAssetBundleProjectSettingsPaths.BuildSettingsAssetPath;

        [SerializeField] private string m_activeApplicationBuildName = string.Empty;
        [SerializeField] private List<AssetBundleApplicationProfile> m_applicationProfiles = new List<AssetBundleApplicationProfile>();

        public string ActiveApplicationBuildName => m_activeApplicationBuildName;
        public IReadOnlyList<AssetBundleApplicationProfile> ApplicationProfiles => m_applicationProfiles;

        public static AssetBundleBuildSetting Load()
        {
            return AssetDatabase.LoadAssetAtPath<AssetBundleBuildSetting>(AssetPath);
        }

        public bool TryGetProfile(string buildName, out AssetBundleApplicationProfile profile)
        {
            profile = null;
            if (string.IsNullOrWhiteSpace(buildName))
                return false;

            foreach (AssetBundleApplicationProfile candidate in m_applicationProfiles)
            {
                if (candidate != null && string.Equals(candidate.BuildName, buildName, StringComparison.Ordinal))
                {
                    profile = candidate;
                    return true;
                }
            }

            return false;
        }

        public AssetBundleApplicationProfile GetRequiredProfile(string buildName)
        {
            if (TryGetProfile(buildName, out AssetBundleApplicationProfile profile))
                return profile;

            throw new InvalidOperationException($"AssetBundle application build profile not found: {buildName}");
        }

        public AssetBundleApplicationProfile GetActiveProfile()
        {
            return GetRequiredProfile(m_activeApplicationBuildName);
        }

        public string[] GetBuildNames()
        {
            List<string> names = new List<string>();
            foreach (AssetBundleApplicationProfile profile in m_applicationProfiles)
            {
                if (profile != null && !string.IsNullOrWhiteSpace(profile.BuildName))
                    names.Add(profile.BuildName);
            }

            return names.ToArray();
        }

        public void ValidateProfiles()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (AssetBundleApplicationProfile profile in m_applicationProfiles)
            {
                if (profile == null || string.IsNullOrWhiteSpace(profile.BuildName))
                    throw new InvalidOperationException("AssetBundle application build name cannot be empty.");
                if (!names.Add(profile.BuildName))
                    throw new InvalidOperationException($"Duplicate AssetBundle application build name: {profile.BuildName}");
            }

            if (m_applicationProfiles.Count == 0)
                throw new InvalidOperationException("At least one AssetBundle application build profile is required.");
            if (!TryGetProfile(m_activeApplicationBuildName, out _))
                throw new InvalidOperationException($"Active AssetBundle application build profile not found: {m_activeApplicationBuildName}");
        }

        public void ResetToDefaults()
        {
            m_applicationProfiles = new List<AssetBundleApplicationProfile> { new AssetBundleApplicationProfile() };
            m_activeApplicationBuildName = "Default";
        }
    }
}
