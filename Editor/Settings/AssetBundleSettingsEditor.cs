#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameAssetBundle.Edit
{
    [CustomEditor(typeof(AssetBundleSettings))]
    public sealed class AssetBundleSettingsEditor : UnityEditor.Editor
    {
        private const string SelectedBuildNameSessionKey = "GameAssetBundle.Settings.SelectedBuildName";

        private SerializedProperty m_AssetRecordsFileName;
        private SerializedProperty m_MainHybridName;
        private SerializedProperty m_HybridFileExtension;
        private SerializedProperty m_HybridDllExtension;
        private SerializedProperty m_AssetBundleDifference;
        private SerializedProperty m_AssetBundleNameMap;
        private SerializedProperty m_AssetBundleExt;
        private SerializedProperty m_AssetPackFileName;
        private SerializedProperty m_VersionFilename;
        private SerializedProperty m_PreloadFile;
        private SerializedProperty m_Md5Filename;
        private SerializedProperty m_FileListName;
        private SerializedProperty m_LocalAbrPath;
        private bool m_ShowBuildActions = true;
        private bool m_ShowPaths = true;
        private bool m_ShowFileNames = true;
        private bool m_ShowHybrid;
        private string m_SelectedBuildName;
        private string m_LastResult;
        private MessageType m_LastResultType = MessageType.Info;
#if USE_HYBRID
        private bool m_GenerateHybridWrap = true;
#endif
        private GUIStyle m_HeaderStyle;
        private GUIStyle m_SectionStyle;

        private void OnEnable()
        {
            m_AssetRecordsFileName = serializedObject.FindProperty("m_assetRecordsFileName");
            m_MainHybridName = serializedObject.FindProperty("m_mainHybridName");
            m_HybridFileExtension = serializedObject.FindProperty("m_hybridFileExtension");
            m_HybridDllExtension = serializedObject.FindProperty("m_hybridDllExtension");
            m_AssetBundleDifference = serializedObject.FindProperty("m_assetBundleDifference");
            m_AssetBundleNameMap = serializedObject.FindProperty("m_assetBundleNameMap");
            m_AssetBundleExt = serializedObject.FindProperty("m_assetBundleExt");
            m_AssetPackFileName = serializedObject.FindProperty("m_assetPackFileName");
            m_VersionFilename = serializedObject.FindProperty("m_versionFilename");
            m_PreloadFile = serializedObject.FindProperty("m_preloadFile");
            m_Md5Filename = serializedObject.FindProperty("m_md5Filename");
            m_FileListName = serializedObject.FindProperty("m_fileListName");
            m_LocalAbrPath = serializedObject.FindProperty("m_localAbrPath");
            m_SelectedBuildName = SessionState.GetString(SelectedBuildNameSessionKey, string.Empty);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EnsureStyles();

            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 180f;

            DrawHeader();
            EditorGUILayout.Space(6f);

            DrawSection(ref m_ShowBuildActions, "应用构建操作", DrawBuildActions);
            DrawSection(ref m_ShowPaths, "路径与扩展名", () =>
            {
                DrawProperty(m_LocalAbrPath, "本地资源目录", "运行时读取本地 AssetBundle 资源的目录。");
                DrawProperty(m_AssetBundleExt, "AssetBundle 扩展名", "生成 AssetBundle 文件时使用的扩展名。");
            });
            DrawSection(ref m_ShowFileNames, "生成文件名称", () =>
            {
                DrawProperty(m_AssetRecordsFileName, "资源记录", "AssetBundle 名称、哈希和资源查找表文件。");
                DrawProperty(m_AssetBundleDifference, "差异记录", "差异包中变化 AssetBundle 的查找表文件。");
                DrawProperty(m_AssetBundleNameMap, "资源包名称映射", "生成资源包的可读名称映射文件。");
                DrawProperty(m_AssetPackFileName, "资源整包", "合并后的 AssetBundle 整包文件名。");
                DrawProperty(m_VersionFilename, "版本记录", "资源版本记录文件名。");
                DrawProperty(m_PreloadFile, "预加载记录", "预加载资源列表文件名。");
                DrawProperty(m_Md5Filename, "MD5 记录", "资源整包校验文件名。");
                DrawProperty(m_FileListName, "文件列表", "生成资源文件列表的文件名。");
            });
            DrawSection(ref m_ShowHybrid, "HybridCLR 集成", () =>
            {
                DrawProperty(m_MainHybridName, "主程序集名称", "主要热更新程序集名称。");
                DrawProperty(m_HybridFileExtension, "运行时文件扩展名", "热更新 DLL 准备为运行时资源后使用的扩展名。");
                DrawProperty(m_HybridDllExtension, "DLL 扩展名", "HybridCLR 程序集源文件的扩展名。");
            });

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUIContent resetContent = new GUIContent("恢复默认值", "恢复全部 GameAssetBundle 文件名和路径设置。");
            if (GUILayout.Button(resetContent, GUILayout.Width(140f), GUILayout.Height(24f)))
            {
                Undo.RecordObject(target, "Restore GameAssetBundle Settings");
                ((AssetBundleSettings)target).ResetToDefaults();
                EditorUtility.SetDirty(target);
                serializedObject.Update();
            }
            EditorGUILayout.EndHorizontal();

            if (serializedObject.ApplyModifiedProperties())
                EditorUtility.SetDirty(target);

            EditorGUIUtility.labelWidth = previousLabelWidth;
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            GUIContent icon = EditorGUIUtility.IconContent("d_Settings");
            GUILayout.Label(icon, GUILayout.Width(28f), GUILayout.Height(28f));
            EditorGUILayout.LabelField("GameAssetBundle 设置", m_HeaderStyle, GUILayout.Height(28f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Editor", EditorStyles.miniLabel, GUILayout.Width(90f));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBuildActions()
        {
            AssetBundleBuildSetting buildSetting = AssetBundleBuildSetting.Load();
            if (buildSetting == null)
            {
                EditorGUILayout.HelpBox(
                    $"AssetBundle build setting not found: {AssetBundleBuildSetting.AssetPath}",
                    MessageType.Error);
                return;
            }

            string[] buildNames = buildSetting.GetBuildNames();
            if (buildNames.Length == 0)
            {
                EditorGUILayout.HelpBox("No application build profiles are configured.", MessageType.Warning);
                return;
            }

            int currentIndex = Array.IndexOf(buildNames, m_SelectedBuildName);
            if (currentIndex < 0)
                currentIndex = Mathf.Max(0, Array.IndexOf(buildNames, buildSetting.ActiveApplicationBuildName));

            int selectedIndex = EditorGUILayout.Popup("应用构建名称", currentIndex, buildNames);
            m_SelectedBuildName = buildNames[selectedIndex];
            SessionState.SetString(SelectedBuildNameSessionKey, m_SelectedBuildName);
            AssetBundleApplicationProfile profile = buildSetting.GetRequiredProfile(m_SelectedBuildName);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("构建目标", $"{profile.BuildTarget} / {profile.ScriptingImplementation}");
            EditorGUILayout.TextField("构建模式", profile.Release ? "Release" : "Debug");
            EditorGUILayout.TextField("资源版本", $"{profile.BaseVersion} -> {profile.CurrentVersion}");
            EditorGUILayout.TextField("资源加密", profile.EncryptAssetBundle ? "已加密" : "未加密");
            EditorGUI.EndDisabledGroup();

            bool encryptionReady = !profile.EncryptAssetBundle || AssetBundleBuildActions.HasEncryptionCallback;
            if (!encryptionReady)
            {
                EditorGUILayout.HelpBox(
                    "当前档案启用了资源加密，但尚未注册外部加密回调。",
                    MessageType.Error);
            }

#if USE_HYBRID
            m_GenerateHybridWrap = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "生成 HybridCLR Wrap",
                    "资源构建前先重新生成 HybridCLR Wrap；取消后沿用现有 Wrap 文件。"),
                m_GenerateHybridWrap);
#endif

#if USE_HYBRID
            bool generateHybridWrap = m_GenerateHybridWrap;
#else
            const bool generateHybridWrap = true;
#endif

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            bool previousEnabled = GUI.enabled;
            Color previousColor = GUI.backgroundColor;
            GUI.enabled = previousEnabled && encryptionReady;
            GUI.backgroundColor = new Color(0.64f, 0.76f, 0.87f);
            GUIContent exportContent = new GUIContent(
                "仅导出资源",
                EditorGUIUtility.IconContent("BuildSettings.Editor.Small").image,
                "构建并导出当前应用构建档案，不生成差异包。");
            bool exportClicked = GUILayout.Button(exportContent, GUILayout.Height(30f));
            GUI.enabled = previousEnabled;
            GUI.backgroundColor = previousColor;
            GUI.enabled = previousEnabled && encryptionReady;
            GUI.backgroundColor = new Color(0.58f, 0.78f, 0.63f);
            GUIContent buildContent = new GUIContent(
                "导出资源并生成差异包",
                EditorGUIUtility.IconContent("BuildSettings.Editor.Small").image,
                "构建下一个资源版本，并同时生成相对基础版本的差异包。");
            bool buildClicked = GUILayout.Button(buildContent, GUILayout.Height(30f));
            GUI.enabled = previousEnabled;
            GUI.backgroundColor = previousColor;
            GUIContent differenceContent = new GUIContent(
                "仅生成差异包",
                EditorGUIUtility.IconContent("d_Profiler.NetworkMessages").image,
                "使用已经存在的基础版本和当前版本成品生成差异包。");
            bool differenceClicked = GUILayout.Button(differenceContent, GUILayout.Height(30f));
            EditorGUILayout.EndHorizontal();

            DrawOutputFolder();

            if (exportClicked)
                ExecuteResourceBuildAction(() => AssetBundleBuildActions.BuildResources(
                    profile.BuildName,
                    generateHybridWrap));
            if (buildClicked)
                ExecuteBuildAction(() => AssetBundleBuildActions.BuildResourcesAndDifference(
                    profile.BuildName,
                    generateHybridWrap));
            if (differenceClicked)
                ExecuteBuildAction(() => AssetBundleBuildActions.BuildDifference(profile.BuildName));

            if (!string.IsNullOrEmpty(m_LastResult))
                EditorGUILayout.HelpBox(m_LastResult, m_LastResultType);
        }

        private void ExecuteResourceBuildAction(Func<AssetBundleBuildResult> buildAction)
        {
            try
            {
                AssetBundleBuildResult result = buildAction();
                m_LastResult = $"资源导出完成：{result.version}\n{result.artifactPath}";
                m_LastResultType = MessageType.Info;
                Debug.Log(m_LastResult);
            }
            catch (Exception exception)
            {
                m_LastResult = exception.Message;
                m_LastResultType = MessageType.Error;
                Debug.LogException(exception);
            }
        }

        private void DrawOutputFolder()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("差异包输出", AssetBundleArtifactRepository.DifferenceRoot);
            EditorGUI.EndDisabledGroup();
            GUIContent folderContent = EditorGUIUtility.IconContent("d_FolderOpened Icon");
            folderContent.tooltip = "打开差异包输出目录。";
            if (GUILayout.Button(folderContent, GUILayout.Width(30f), GUILayout.Height(19f)))
            {
                Directory.CreateDirectory(AssetBundleArtifactRepository.DifferenceRoot);
                EditorUtility.RevealInFinder(AssetBundleArtifactRepository.DifferenceRoot);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ExecuteBuildAction(Func<AssetBundleDifferencePackage> buildAction)
        {
            try
            {
                AssetBundleDifferencePackage result = buildAction();
                m_LastResult = $"完成：{result.changedBundleCount} 个变化资源包，共 {result.totalBytes} 字节\n{result.outputPath}";
                m_LastResultType = MessageType.Info;
                Debug.Log(m_LastResult);
            }
            catch (Exception exception)
            {
                m_LastResult = exception.Message;
                m_LastResultType = MessageType.Error;
                Debug.LogException(exception);
            }
        }

        private void DrawSection(ref bool expanded, string title, Action drawFields)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            expanded = EditorGUILayout.Foldout(expanded, title, true, m_SectionStyle);
            if (expanded)
            {
                EditorGUILayout.Space(3f);
                EditorGUI.indentLevel++;
                drawFields();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2f);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3f);
        }

        private static void DrawProperty(SerializedProperty property, string label, string tooltip)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip));
        }

        private void EnsureStyles()
        {
            if (m_HeaderStyle == null)
            {
                m_HeaderStyle = new GUIStyle(EditorStyles.largeLabel)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                };
            }

            if (m_SectionStyle == null)
            {
                m_SectionStyle = new GUIStyle(EditorStyles.foldoutHeader)
                {
                    fontStyle = FontStyle.Bold,
                };
            }
        }
    }

    internal static class AssetBundleSettingsProvider
    {
        internal const string SettingsAssetPath = GameAssetBundleProjectSettingsPaths.SettingsAssetPath;

        [SettingsProvider]
        private static SettingsProvider CreateSettingsProvider()
        {
            AssetSettingsProvider provider = new AssetSettingsProvider(
                "Project/GameAssetBundle",
                GetOrCreateSettings);
            provider.keywords = new HashSet<string>
            {
                "GameAssetBundle",
                "AssetBundle",
                "HybridCLR",
                "Path",
                "File Name",
                "Extension",
                "Build",
                "Difference",
                "Encryption",
                "应用构建",
                "差异包"
            };
            return provider;
        }

        [MenuItem("HaoFangTools/GameAssetBundle/Settings", false, 1)]
        private static void OpenSettings()
        {
            GetOrCreateSettings();
            SettingsService.OpenProjectSettings("Project/GameAssetBundle");
        }

        internal static UnityEngine.Object GetOrCreateSettings()
        {
            AssetBundleSettings settings = AssetDatabase.LoadAssetAtPath<AssetBundleSettings>(SettingsAssetPath);
            if (settings != null)
                return settings;

            if (!GameAssetBundleProjectSettingsPaths.EnsureEditorFolder())
            {
                Debug.LogError($"GameAssetBundle settings folder is missing: {SettingsAssetPath}");
                return null;
            }

            settings = ScriptableObject.CreateInstance<AssetBundleSettings>();
            settings.ResetToDefaults();
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

    }
}
#endif
