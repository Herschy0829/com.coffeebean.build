#if UNITY_IOS
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.iOS.Xcode;

namespace CoffeeBean.EditorTools
{
    /// <summary>
    /// iOS Xcode 工程落盘适配（仅 mac + iOS 平台编译；Windows 无 iOS 模块时整个文件被排除）：
    /// 消费 <see cref="CIosPlan"/>，用 PBXProject / ProjectCapabilityManager 写入 Xcode 工程。
    /// </summary>
    public static class CiOSXcodeAdapter
    {
        const string TargetName = "Unity-iPhone";

        /// <summary>应用计划到 Xcode 工程；返回处理项数。projectRoot 用于解析 Assets/... 本地库源。</summary>
        public static int Apply(CIosPlan plan, string exportRoot, string projectRoot, CExportLog log)
        {
            if (plan == null || !plan.HasPbxWork) return 0;

            string pbxPath = Path.Combine(exportRoot, "Unity-iPhone.xcodeproj", "project.pbxproj");
            if (!File.Exists(pbxPath))
                throw new CExportException("ios.xcode", pbxPath, "未找到 Xcode 工程 project.pbxproj（确认在 mac 上导出 iOS 工程）");

            var project = new PBXProject();
            project.ReadFromFile(pbxPath);
            string mainTarget = project.GetUnityMainTargetGuid();
            string fwTarget = project.GetUnityFrameworkTargetGuid();
            int n = 0;

            // 1) 系统 framework
            foreach (var f in plan.SystemFrameworks)
            {
                if (string.IsNullOrEmpty(f)) continue;
                project.AddFrameworkToProject(fwTarget, f, false);
                log.Step("ios.xcode", $"framework +{f}");
                n++;
            }
            foreach (var f in plan.WeakSystemFrameworks)
            {
                if (string.IsNullOrEmpty(f)) continue;
                project.AddFrameworkToProject(fwTarget, f, true);
                log.Step("ios.xcode", $"weak framework +{f}");
                n++;
            }

            // 2) 本地库/文件：拷贝到 <exportRoot>/Libraries/CoffeeBean 并注册 target
            string localDir = Path.Combine(exportRoot, "Libraries", "CoffeeBean");
            foreach (var lib in plan.LocalLibs)
            {
                string src = ResolveSource(lib.source, projectRoot);
                if (!File.Exists(src) && !Directory.Exists(src))
                {
                    log.Warn($"ios.xcode: 本地库不存在（跳过）：{src}");
                    continue;
                }
                string name = Path.GetFileName(src.TrimEnd(Path.DirectorySeparatorChar));
                string dst = Path.Combine(localDir, name);
                Directory.CreateDirectory(localDir);
                if (Directory.Exists(src))
                    CopyDirectory(src, dst);
                else
                    File.Copy(src, dst, true);

                string targetGuid = ResolveTarget(project, mainTarget, fwTarget, lib.target);
                // 相对工程根的路径（Xcode 以工程根为 sourceTree 根）
                string rel = "Libraries/CoffeeBean/" + name;
                string fileRef = project.AddFile(rel, rel, PBXSourceTree.Source);
                project.AddFileToBuild(targetGuid, fileRef);
                log.Step("ios.xcode", $"local {name} -> {lib.target}" + (lib.embed ? "（embed 需在 Xcode Embed Frameworks 阶段确认，见 README）" : ""));
                n++;
            }

            // 3) linker flags / 宏 / build settings
            foreach (var flag in plan.LinkerFlags)
            {
                if (string.IsNullOrEmpty(flag)) continue;
                project.AddBuildProperty(fwTarget, "OTHER_LDFLAGS", flag);
                n++;
            }
            foreach (var m in plan.Macros)
            {
                if (string.IsNullOrEmpty(m)) continue;
                project.AddBuildProperty(fwTarget, "GCC_PREPROCESSOR_DEFINITIONS", m);
                n++;
            }
            foreach (var kv in plan.BuildSettings)
            {
                if (kv == null || string.IsNullOrEmpty(kv.key)) continue;
                project.SetBuildProperty(fwTarget, kv.key, kv.value);
                n++;
            }

            // 4) 先落盘 PBX 改动，再交给 ProjectCapabilityManager（其构造读当前文件）
            project.WriteToFile(pbxPath);

            if (plan.Capabilities.Count > 0)
            {
                string entitlementsPath = Path.Combine(exportRoot, TargetName + ".entitlements");
                var capMgr = new ProjectCapabilityManager(pbxPath, entitlementsPath, TargetName, mainTarget);
                foreach (var cap in plan.Capabilities)
                {
                    switch (cap)
                    {
                        case "PushNotifications":
                            capMgr.AddPushNotifications();
                            break;
                        case "BackgroundModes":
                            capMgr.AddBackgroundModes(new[] { BackgroundModesOptions.RemoteNotification });
                            break;
                        case "SignInWithApple":
                            capMgr.AddSignInWithApple();
                            break;
                        case "InAppPurchase":
                            capMgr.AddInAppPurchase();
                            break;
                        case "GameCenter":
                            capMgr.AddGameCenter();
                            break;
                        default:
                            log.Warn($"ios.xcode: 未知 capability（跳过）：{cap}");
                            continue;
                    }
                    log.Step("ios.xcode", $"capability +{cap}");
                    n++;
                }
                capMgr.WriteToFile();
            }
            return n;
        }

        static string ResolveTarget(PBXProject project, string mainTarget, string fwTarget, string target)
        {
            switch (target)
            {
                case "main": return mainTarget;
                case "both": return fwTarget; // framework 与 main 共享阶段；简化：进 framework（引擎库所在 target）
                default: return fwTarget;
            }
        }

        static string ResolveSource(string source, string projectRoot)
        {
            if (Path.IsPathRooted(source)) return source;
            if (source.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(projectRoot))
                return Path.Combine(projectRoot, source.Replace('/', Path.DirectorySeparatorChar));
            return Path.GetFullPath(source);
        }

        static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var file in Directory.GetFiles(src))
                File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(src))
                CopyDirectory(dir, Path.Combine(dst, Path.GetFileName(dir)));
        }
    }
}
#endif
