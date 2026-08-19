#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameAssetBundle.Edit
{
    [CustomEditor(typeof(AssetBundleBuildSetting))]
    public sealed class AssetBundleBuildSettingEditor : UnityEditor.Editor
    {
        private SerializedProperty m_ActiveApplicationBuildName;
        private SerializedProperty m_ApplicationProfiles;

        private void OnEnable()
        {
            m_ActiveApplicationBuildName = serializedObject.FindProperty("m_activeApplicationBuildName");
            m_ApplicationProfiles = serializedObject.FindProperty("m_applicationProfiles");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawActiveProfilePopup();
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(m_ApplicationProfiles, new GUIContent("Application Build Profiles"), true);
            DrawValidationMessages();

            EditorGUILayout.Space();
            if (GUILayout.Button("Restore Defaults"))
            {
                Undo.RecordObject(target, "Restore AssetBundle Build Setting");
                ((AssetBundleBuildSetting)target).ResetToDefaults();
                EditorUtility.SetDirty(target);
                serializedObject.Update();
            }

            if (serializedObject.ApplyModifiedProperties())
                EditorUtility.SetDirty(target);
        }

        private void DrawActiveProfilePopup()
        {
            string[] names = GetSerializedBuildNames();
            if (names.Length == 0)
            {
                EditorGUILayout.HelpBox("Add at least one application build profile.", MessageType.Warning);
                return;
            }

            int currentIndex = System.Array.IndexOf(names, m_ActiveApplicationBuildName.stringValue);
            int selectedIndex = EditorGUILayout.Popup("Active Application Build", Mathf.Max(0, currentIndex), names);
            m_ActiveApplicationBuildName.stringValue = names[selectedIndex];
        }

        private string[] GetSerializedBuildNames()
        {
            List<string> names = new List<string>();
            for (int i = 0; i < m_ApplicationProfiles.arraySize; i++)
            {
                SerializedProperty profile = m_ApplicationProfiles.GetArrayElementAtIndex(i);
                names.Add(profile.FindPropertyRelative("m_buildName").stringValue);
            }

            return names.ToArray();
        }

        private void DrawValidationMessages()
        {
            string[] names = GetSerializedBuildNames();
            if (names.Any(string.IsNullOrWhiteSpace))
                EditorGUILayout.HelpBox("Application build name cannot be empty.", MessageType.Error);

            string duplicate = names
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .GroupBy(name => name)
                .FirstOrDefault(group => group.Count() > 1)
                ?.Key;
            if (!string.IsNullOrEmpty(duplicate))
                EditorGUILayout.HelpBox($"Duplicate application build name: {duplicate}", MessageType.Error);
        }

    }

    internal static class AssetBundleBuildSettingProvider
    {
        [SettingsProvider]
        private static SettingsProvider CreateSettingsProvider()
        {
            AssetSettingsProvider provider = new AssetSettingsProvider(
                "Project/GameAssetBundle/Build",
                GetOrCreateSetting);
            provider.keywords = new HashSet<string>
            {
                "GameAssetBundle",
                "AssetBundle",
                "Build",
                "Clean",
                "Version",
                "Application",
                "Profile",
                "Artifact",
                "Encrypt",
                "Encryption"
            };
            return provider;
        }

        [MenuItem("HaoFangTools/GameAssetBundle/Build Settings", false, 2)]
        private static void OpenSettings()
        {
            GetOrCreateSetting();
            SettingsService.OpenProjectSettings("Project/GameAssetBundle/Build");
        }

        internal static Object GetOrCreateSetting()
        {
            AssetBundleBuildSetting setting = AssetBundleBuildSetting.Load();
            if (setting != null)
                return setting;

            if (!GameAssetBundleProjectSettingsPaths.EnsureEditorFolder())
                return null;

            setting = ScriptableObject.CreateInstance<AssetBundleBuildSetting>();
            setting.ResetToDefaults();
            AssetDatabase.CreateAsset(setting, AssetBundleBuildSetting.AssetPath);
            AssetDatabase.SaveAssets();
            return setting;
        }
    }
}
#endif
