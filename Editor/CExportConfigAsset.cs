using UnityEditor;
using UnityEngine;

namespace CoffeeBean.EditorTools
{
    /// <summary>
    /// 导出配置资产（ScriptableObject 包壳 <see cref="CExportConfig"/>）。
    /// 放置约定：Assets/CoffeeBean/ExportConfig.asset（构建回调按此路径加载）。
    /// </summary>
    [CreateAssetMenu(fileName = "ExportConfig", menuName = "CoffeeBean/Build Export Config")]
    public sealed class CExportConfigAsset : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/CoffeeBean/ExportConfig.asset";

        [Tooltip("导出定制配置（Android / iOS 两段）")]
        public CExportConfig config = new CExportConfig();

        /// <summary>加载工程配置资产；不存在返回 null（构建回调据此跳过）。</summary>
        public static CExportConfig LoadConfig()
        {
            var asset = AssetDatabase.LoadAssetAtPath<CExportConfigAsset>(DefaultAssetPath);
            return asset != null ? asset.config : null;
        }

        /// <summary>创建默认配置资产（已存在则选中返回）。入口：Assets/Create/CoffeeBean 与 Hub 工具窗口。</summary>
        public static void CreateOrFocusAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<CExportConfigAsset>(DefaultAssetPath);
            if (asset == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/CoffeeBean"))
                    AssetDatabase.CreateFolder("Assets", "CoffeeBean");
                asset = CreateInstance<CExportConfigAsset>();
                AssetDatabase.CreateAsset(asset, DefaultAssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[CoffeeBean.Build] 已创建导出配置资产：" + DefaultAssetPath);
            }
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
