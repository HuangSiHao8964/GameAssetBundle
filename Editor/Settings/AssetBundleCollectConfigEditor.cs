#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace GameAssetBundle.Edit
{
    [CustomEditor(typeof(AssetBundleCollectConfig))]
    public sealed class AssetBundleCollectConfigEditor : UnityEditor.Editor
    {
        private SerializedProperty m_Collect;

        private void OnEnable()
        {
            m_Collect = serializedObject.FindProperty("collect");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "这是编辑器专用的资源采集配置，请使用独立配置窗口管理采集规则。",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField("采集规则", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(m_Collect.arraySize.ToString(), GUILayout.Width(32));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("打开资源采集配置窗口", GUILayout.Height(28)))
            {
                AssetBundleCollectConfigWindow.OpenWindow();
            }

            if (GUILayout.Button("定位包内配置资产"))
            {
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }

    public sealed class AssetBundleCollectConfigWindow : EditorWindow
    {
        private const float HeaderHeight = 76f;
        private const float RowHeight = 92f;

        private AssetBundleCollectConfig m_Config;
        private SerializedObject m_SerializedObject;
        private SerializedProperty m_Collect;
        private ReorderableList m_RuleList;
        private Vector2 m_ScrollPosition;
        private GUIStyle m_HeaderTitle;
        private GUIStyle m_HeaderSubtitle;
        private GUIStyle m_SectionTitle;
        private GUIStyle m_StatusValid;
        private GUIStyle m_StatusInvalid;
        private GUIStyle m_ToolbarButton;

        [MenuItem("HaoFangTools/GameAssetBundle/资源采集/配置窗口", false, 20)]
        public static void OpenWindow()
        {
            AssetBundleCollectConfigWindow window = GetWindow<AssetBundleCollectConfigWindow>("资源采集配置", false);
            window.minSize = new Vector2(760f, 500f);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("资源采集配置");
            EditorApplication.projectChanged += ReloadConfig;
            Undo.undoRedoPerformed += Repaint;
            ReloadConfig();
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= ReloadConfig;
            Undo.undoRedoPerformed -= Repaint;
        }

        private void ReloadConfig()
        {
            AssetBundleCollectData.ReloadConfig();
            m_Config = AssetBundleCollectData.Config;
            m_SerializedObject = m_Config == null ? null : new SerializedObject(m_Config);
            m_Collect = m_SerializedObject == null ? null : m_SerializedObject.FindProperty("collect");
            BuildRuleList();
            Repaint();
        }

        private void BuildRuleList()
        {
            if (m_Collect == null)
            {
                m_RuleList = null;
                return;
            }

            m_RuleList = new ReorderableList(m_SerializedObject, m_Collect, true, true, false, false)
            {
                elementHeight = RowHeight,
                drawHeaderCallback = DrawRuleHeader,
                drawElementCallback = DrawRule,
                onAddCallback = AddRule,
                onRemoveCallback = RemoveRule,
                onReorderCallback = list => MarkDirty()
            };
        }

        private void OnGUI()
        {
            InitializeStyles();
            if (m_Config == null || m_SerializedObject == null || m_RuleList == null)
            {
                EditorGUILayout.HelpBox("未找到包内资源采集配置资产。", MessageType.Error);
                if (GUILayout.Button("创建配置资产"))
                    ReloadConfig();
                return;
            }

            m_SerializedObject.Update();
            DrawHeader();
            DrawSummary();
            DrawToolbar();

            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);
            m_RuleList.DoLayoutList();
            EditorGUILayout.EndScrollView();

            DrawFooter();
            if (m_SerializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(m_Config);
                Repaint();
            }
        }

        private void InitializeStyles()
        {
            if (m_HeaderTitle != null)
                return;

            Color primary = EditorGUIUtility.isProSkin ? new Color(0.92f, 0.94f, 0.98f) : new Color(0.12f, 0.14f, 0.18f);
            Color secondary = EditorGUIUtility.isProSkin ? new Color(0.67f, 0.72f, 0.80f) : new Color(0.32f, 0.36f, 0.42f);

            m_HeaderTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 19,
                normal = { textColor = primary }
            };
            m_HeaderSubtitle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                normal = { textColor = secondary }
            };
            m_SectionTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = primary }
            };
            m_StatusValid = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.28f, 0.72f, 0.42f) }
            };
            m_StatusInvalid = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.85f, 0.38f, 0.32f) }
            };
            m_ToolbarButton = new GUIStyle(EditorStyles.toolbarButton)
            {
                fixedHeight = 22f
            };
        }

        private void DrawHeader()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, HeaderHeight, GUILayout.ExpandWidth(true));
            Color background = EditorGUIUtility.isProSkin ? new Color(0.12f, 0.15f, 0.20f) : new Color(0.88f, 0.91f, 0.96f);
            EditorGUI.DrawRect(rect, background);

            Rect content = new Rect(rect.x + 18f, rect.y + 13f, rect.width - 36f, rect.height - 20f);
            GUI.Label(new Rect(content.x, content.y, content.width, 28f), "AssetBundle 资源采集", m_HeaderTitle);
            GUI.Label(new Rect(content.x, content.y + 31f, content.width, 20f),
                "仅编辑器使用的 GameAssetBundle 资源采集规则", m_HeaderSubtitle);
        }

        private void DrawSummary()
        {
            int valid = 0;
            for (int i = 0; i < m_Collect.arraySize; i++)
            {
                if (IsValidFolder(m_Collect.GetArrayElementAtIndex(i).FindPropertyRelative("folder").stringValue))
                    valid++;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            DrawSummaryCard("规则总数", m_Collect.arraySize.ToString(), new Color(0.30f, 0.52f, 0.86f));
            DrawSummaryCard("路径有效", valid.ToString(), new Color(0.28f, 0.72f, 0.42f));
            DrawSummaryCard("需要检查", (m_Collect.arraySize - valid).ToString(), new Color(0.86f, 0.54f, 0.24f));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummaryCard(string label, string value, Color accent)
        {
            Rect rect = GUILayoutUtility.GetRect(120f, 48f, GUILayout.MinWidth(120f), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.17f, 0.19f, 0.24f) : new Color(0.95f, 0.96f, 0.98f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), accent);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 7f, rect.width - 20f, 16f), label, EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 22f, rect.width - 20f, 22f), value, m_SectionTitle);
            GUILayout.Space(6f);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("采集规则", m_SectionTitle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("添加规则", m_ToolbarButton))
                AddRule(m_RuleList);
            EditorGUI.BeginDisabledGroup(m_RuleList.index < 0 || m_RuleList.index >= m_Collect.arraySize);
            if (GUILayout.Button("删除规则", m_ToolbarButton))
                RemoveRule(m_RuleList);
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("保存", m_ToolbarButton))
                SaveConfig();
            if (GUILayout.Button("清空", m_ToolbarButton))
                ResetConfig();
            if (GUILayout.Button("定位资产", m_ToolbarButton))
            {
                Selection.activeObject = m_Config;
                EditorGUIUtility.PingObject(m_Config);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRuleHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "规则将按照列表顺序执行", EditorStyles.miniLabel);
        }

        private void DrawRule(Rect rect, int index, bool active, bool focused)
        {
            SerializedProperty element = m_Collect.GetArrayElementAtIndex(index);
            SerializedProperty folder = element.FindPropertyRelative("folder");
            SerializedProperty type = element.FindPropertyRelative("type");
            SerializedProperty ext = element.FindPropertyRelative("ext");
            bool valid = IsValidFolder(folder.stringValue);

            Rect card = new Rect(rect.x + 2f, rect.y + 3f, rect.width - 4f, RowHeight - 8f);
            Color cardColor = EditorGUIUtility.isProSkin ? new Color(0.16f, 0.18f, 0.22f) : new Color(0.97f, 0.98f, 1f);
            EditorGUI.DrawRect(card, cardColor);
            EditorGUI.DrawRect(new Rect(card.x, card.y, 3f, card.height), valid ? new Color(0.28f, 0.72f, 0.42f) : new Color(0.85f, 0.38f, 0.32f));

            float left = card.x + 12f;
            float width = card.width - 24f;
            EditorGUI.LabelField(new Rect(left, card.y + 8f, 80f, 18f), $"规则 {index + 1}", EditorStyles.boldLabel);
            GUI.Label(new Rect(card.x + card.width - 155f, card.y + 8f, 140f, 18f), valid ? "目录有效" : "目录不存在", valid ? m_StatusValid : m_StatusInvalid);

            EditorGUI.PropertyField(new Rect(left, card.y + 29f, width, 18f), folder, new GUIContent("资源目录"));
            float controlWidth = Mathf.Max(180f, (width - 8f) * 0.5f);
            EditorGUI.PropertyField(new Rect(left, card.y + 54f, controlWidth, 18f), type, new GUIContent("打包方式"));
            EditorGUI.PropertyField(new Rect(left + controlWidth + 8f, card.y + 54f, controlWidth, 18f), ext, new GUIContent("文件类型"));
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("可编辑资源目录、打包方式和文件类型，也可以拖动左侧手柄调整规则顺序。", EditorStyles.miniLabel);
        }

        private void AddRule(ReorderableList list)
        {
            int index = m_Collect.arraySize;
            m_Collect.InsertArrayElementAtIndex(index);
            SerializedProperty element = m_Collect.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("folder").stringValue = string.Empty;
            element.FindPropertyRelative("type").enumValueIndex = (int)PackageType.PackAllFileWithExt;
            element.FindPropertyRelative("ext").enumValueIndex = (int)PackageExt.None;
            list.index = index;
            MarkDirty();
        }

        private void RemoveRule(ReorderableList list)
        {
            if (list.index < 0 || list.index >= m_Collect.arraySize)
                return;

            if (!EditorUtility.DisplayDialog("删除采集规则", "确定删除当前选中的采集规则吗？", "删除", "取消"))
                return;

            m_Collect.DeleteArrayElementAtIndex(list.index);
            list.index = Mathf.Clamp(list.index - 1, -1, m_Collect.arraySize - 1);
            MarkDirty();
        }

        private void SaveConfig()
        {
            if (m_SerializedObject.ApplyModifiedProperties())
                EditorUtility.SetDirty(m_Config);
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent("配置已保存"));
        }

        private void ResetConfig()
        {
            if (!EditorUtility.DisplayDialog("清空采集规则", "确定清空全部采集规则吗？", "清空", "取消"))
                return;

            Undo.RecordObject(m_Config, "清空 AssetBundle 采集规则");
            m_Collect.ClearArray();
            MarkDirty();
        }

        private void MarkDirty()
        {
            if (m_Config != null)
                EditorUtility.SetDirty(m_Config);
            Repaint();
        }

        private static bool IsValidFolder(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && AssetDatabase.IsValidFolder(path.Replace('\\', '/'));
        }
    }
}
#endif
