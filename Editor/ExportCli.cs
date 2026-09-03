using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CoffeeBean.EditorTools
{
    /// <summary>
    /// CI / 批处理入口：
    /// Unity.exe -batchmode -quit -projectPath &lt;proj&gt; -executeMethod CoffeeBean.ExportCli.Run
    ///   -exportRoot &lt;导出工程根&gt; -platform Android|IOS -configPath &lt;配置.json&gt; -dryRun
    /// 成功时导出根生成 CoffeeBeanExport.log；失败抛异常（Unity 退出码非 0）。
    /// </summary>
    public static class ExportCli
    {
        public static void Run()
        {
            string exportRoot = GetArg("-exportRoot");
            string platform = GetArg("-platform") ?? "Android";
            string configPath = GetArg("-configPath");
            bool dryRun = GetArg("-dryRun") != null;

            if (string.IsNullOrEmpty(exportRoot))
                throw new CExportException("cli", null, "缺少 -exportRoot 参数（导出工程根目录）");

            CExportPlatform plat = platform.Equals("IOS", StringComparison.OrdinalIgnoreCase)
                ? CExportPlatform.IOS
                : CExportPlatform.Android;

            CExportConfig config;
            if (!string.IsNullOrEmpty(configPath))
            {
                string json = File.ReadAllText(configPath);
                config = JsonUtility.FromJson<CExportConfig>(json) ?? new CExportConfig();
                Debug.Log($"[CoffeeBean.Build CLI] 已加载配置：{configPath}");
            }
            else
            {
                config = CExportConfigAsset.LoadConfig();
                if (config == null) config = new CExportConfig();
            }

            var session = new CExportSession(plat, exportRoot, config)
            {
                ProjectRoot = Path.GetDirectoryName(Application.dataPath),
                DryRun = dryRun
            };
            CExportRunner.Run(session);
            CExportRunner.WriteLogFile(session);
            session.Log.FlushToConsole("CoffeeBean.Build");
            Debug.Log("[CoffeeBean.Build CLI] 完成：" + exportRoot);
        }

        static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
