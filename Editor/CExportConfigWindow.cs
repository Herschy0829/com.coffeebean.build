using UnityEditor;
using UnityEngine;

namespace CoffeeBean.EditorTools
{
    /// <summary>
    /// CoffeeBean Build 导出定制工具窗口（经 Hub 注册入口）：
    /// 创建/定位配置资产、查看配置 JSON、运行 Sample dry-run 演练。
    /// 注意：不注册 Window/CoffeeBean 子菜单（Hub 规则），仅 Tools 顶层菜单兜底。
    /// </summary>
    [CoffeeBeanTool("Build Export Config", "原生工程导出定制：Android Studio / Xcode 工程注入配置与 dry-run", "build")]
    public sealed class CExportConfigWindow : EditorWindow
    {
        [MenuItem("Tools/CoffeeBean/Build Export Config", priority = 1001)]
        static void OpenFromMenu() => Open();

        public static void Open()
        {
            var w = GetWindow<CExportConfigWindow>();
            w.titleContent = new GUIContent("CoffeeBean Build Export");
            w.minSize = new Vector2(520, 400);
        }

        Vector2 _scroll;
        string _status = "";
        bool _hasAsset;

        void OnGUI()
        {
            _hasAsset = AssetDatabase.LoadAssetAtPath<CExportConfigAsset>(CExportConfigAsset.DefaultAssetPath) != null;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("CoffeeBean Build —— 原生工程导出定制", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Android：导出 Gradle / Android Studio 工程后注入 AndroidManifest / gradle / properties / libs / res。\n" +
                "iOS：导出 Xcode 工程后注入 Info.plist / frameworks / Build Settings / capabilities（PBX 落盘需 mac）。",
                MessageType.Info);

            EditorGUILayout.Space(6);
            GUILayout.Label("配置资产", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (_hasAsset)
                {
                    if (GUILayout.Button("定位资产", GUILayout.Width(100))) CExportConfigAsset.CreateOrFocusAsset();
                    if (GUILayout.Button("编辑（Inspector）", GUILayout.Width(130))) CExportConfigAsset.CreateOrFocusAsset();
                }
                else
                {
                    if (GUILayout.Button("创建默认配置资产", GUILayout.Width(150))) CExportConfigAsset.CreateOrFocusAsset();
                }
                EditorGUILayout.LabelField(_hasAsset ? "✓ 已存在：" + CExportConfigAsset.DefaultAssetPath : "未创建（构建回调会跳过注入）", EditorStyles.wordWrappedLabel);
            }

            EditorGUILayout.Space(8);
            GUILayout.Label("dry-run 演练（fake 导出目录，不触发真构建）", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("运行 Android dry-run", GUILayout.Width(160)))
                    _status = NativeExportDemoRunner.RunDemo(CExportPlatform.Android);
                if (GUILayout.Button("运行 iOS dry-run", GUILayout.Width(160)))
                    _status = NativeExportDemoRunner.RunDemo(CExportPlatform.IOS);
            }

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space(8);
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(180));
                EditorGUILayout.TextArea(_status, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }
    }
}
