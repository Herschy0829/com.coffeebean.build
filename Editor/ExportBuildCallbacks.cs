using System;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEngine;

namespace CoffeeBean.EditorTools
{
    /// <summary>
    /// Unity 构建回调适配层：
    /// - Android：实现 IPostGenerateGradleAndroidProject（接口在 UnityEditor.CoreModule，任何编辑器可用；
    ///   导出 Gradle 工程/构建时工程生成后触发，path=gradle 工程根）
    /// - iOS：[PostProcessBuild]（#if UNITY_IOS 内的 PBX 落盘适配在 CiOSXcodeAdapter）
    /// 统一入口 <see cref="RunForPlatform"/>。
    /// </summary>
    public sealed class ExportBuildCallbacks : IPostGenerateGradleAndroidProject
    {
        /// <summary>排序：靠后执行（让其它后处理先落盘，我们最后注入）。</summary>
        public int callbackOrder => 2000;

        public void OnPostGenerateGradleAndroidProject(string exportPath)
        {
            RunForPlatform(CExportPlatform.Android, exportPath);
        }

        /// <summary>iOS：导出 Xcode 工程后触发（mac；Windows 上 iOS 构建不可用不触发）。</summary>
        [PostProcessBuild(2000)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            RunForPlatform(CExportPlatform.IOS, path);
        }

        /// <summary>平台通用执行：加载配置 → 建会话 → 跑管线 → iOS PBX 落盘 → 写日志文件。</summary>
        public static void RunForPlatform(CExportPlatform platform, string exportRoot)
        {
            try
            {
                var config = CExportConfigAsset.LoadConfig();
                if (config == null)
                {
                    Debug.Log($"[CoffeeBean.Build] 未找到导出配置资产（Assets/CoffeeBean/ExportConfig.asset），跳过 {platform} 定制。");
                    return;
                }
                var session = new CExportSession(platform, exportRoot, config)
                {
                    ProjectRoot = Path.GetDirectoryName(Application.dataPath)
                };
                bool ran = CExportRunner.Run(session);
                if (ran && !session.DryRun)
                {
                    // iOS：PBX 计划落盘（仅 mac+iOS 平台编译）
                    if (platform == CExportPlatform.IOS)
                    {
#if UNITY_IOS
                        int n = CiOSXcodeAdapter.Apply(session.IosPlan, exportRoot, session.ProjectRoot, session.Log);
                        session.Log.Step("ios.xcode", $"PBX 落盘完成（{n} 项）");
#else
                        session.Log.Warn("ios.xcode: 非 iOS 平台编辑器，跳过 PBX 落盘（plist 注入已完成）。");
#endif
                    }
                }
                CExportRunner.WriteLogFile(session);
                session.Log.FlushToConsole("CoffeeBean.Build");
            }
            catch (CExportException e)
            {
                Debug.LogError($"[CoffeeBean.Build] {e.Message}");
                throw new BuildFailedException(e.Message);
            }
        }
    }
}
