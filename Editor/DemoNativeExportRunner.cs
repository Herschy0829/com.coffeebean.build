using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CoffeeBean.EditorTools
{
    /// <summary>
    /// dry-run 演练（Editor 窗口与 Sample 共用）：在临时目录构造最小 fake 导出工程
    /// （launcher/unityLibrary manifest、build.gradle、gradle.properties、Info.plist），
    /// 用内置示例配置跑一次 CExportRunner（DryRun=true，不落盘），返回日志文本。
    /// 不打真构建即可验证注入逻辑与产物。
    /// </summary>
    public static class NativeExportDemoRunner
    {
        /// <summary>运行演示；返回日志文本。</summary>
        public static string RunDemo(CExportPlatform platform)
        {
            string dir = Path.Combine(Path.GetTempPath(), "CoffeeBeanExportDemo_" + Guid.NewGuid().ToString("N"));
            try
            {
                BuildFakeExport(dir, platform);
                var config = CreateDemoConfig(platform);
                var session = new CExportSession(platform, dir, config)
                {
                    ProjectRoot = Application.dataPath != null ? Path.GetDirectoryName(Application.dataPath) : null,
                    DryRun = true
                };
                CExportRunner.Run(session);
                string log = session.Log.ToText();
                Debug.Log("[CoffeeBean.Build demo] " + platform + " dry-run 完成。\n" + log);
                return log;
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { /* 忽略清理失败 */ }
            }
        }

        /// <summary>构造最小 fake 导出工程目录。</summary>
        public static void BuildFakeExport(string root, CExportPlatform platform)
        {
            if (platform == CExportPlatform.Android)
            {
                Directory.CreateDirectory(Path.Combine(root, "launcher", "src", "main"));
                Directory.CreateDirectory(Path.Combine(root, "unityLibrary", "src", "main", "res", "values"));
                File.WriteAllText(Path.Combine(root, "launcher", "src", "main", "AndroidManifest.xml"),
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" package=\"com.demo.game\">\n    <uses-permission android:name=\"android.permission.INTERNET\" />\n    <application android:label=\"Demo\">\n    </application>\n</manifest>\n");
                File.WriteAllText(Path.Combine(root, "unityLibrary", "src", "main", "AndroidManifest.xml"),
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\">\n    <application>\n    </application>\n</manifest>\n");
                File.WriteAllText(Path.Combine(root, "unityLibrary", "build.gradle"),
                    "apply plugin: 'com.android.library'\n\ndependencies {\n    implementation fileTree(dir: 'libs', include: ['*.jar'])\n}\n");
                File.WriteAllText(Path.Combine(root, "gradle.properties"), "org.gradle.jvmargs=-Xmx1024m\n");
                File.WriteAllText(Path.Combine(root, "unityLibrary", "src", "main", "res", "values", "strings.xml"),
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<resources>\n</resources>\n");
            }
            else
            {
                File.WriteAllText(Path.Combine(root, "Info.plist"),
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n<plist version=\"1.0\">\n<dict>\n    <key>CFBundleIdentifier</key>\n    <string>com.demo.game</string>\n</dict>\n</plist>\n");
            }
        }

        /// <summary>演示用配置（android 权限/meta-data/gradle 依赖 + ios plist 权限/skadnetwork/framework）。</summary>
        public static CExportConfig CreateDemoConfig(CExportPlatform platform)
        {
            var cfg = new CExportConfig { enable = true };
            if (platform == CExportPlatform.Android)
            {
                cfg.android.permissions.Add("android.permission.ACCESS_NETWORK_STATE");
                cfg.android.applicationMetaData.Add(new CExportKv("coffeebean_channel", "googleplay"));
                cfg.android.gradleFiles.Add("unityLibrary/build.gradle");
                cfg.android.gradleLines.Add("implementation(name: 'demo-sdk', ext: 'aar')");
                cfg.android.gradleProperties.Add(new CExportKv("android.useAndroidX", "true"));
                cfg.android.resStrings.Add(new CExportKv("demo_string", "Hello Demo"));
                cfg.android.proguardLines.Add("-keep class com.demo.** { *; }");
                cfg.android.envPolicy = "warn";
            }
            else
            {
                cfg.ios.plistStrings.Add(new CExportKv("NSUserTrackingUsageDescription", "用于广告归因与个性化推荐"));
                cfg.ios.skAdNetworkIds.Add("cstr6suwn9.skadnetwork");
                cfg.ios.urlSchemes.Add("coffeebean");
                cfg.ios.systemFrameworks.Add("AdSupport");
                cfg.ios.systemFrameworks.Add("StoreKit");
                cfg.ios.linkerFlags.Add("-ObjC");
                cfg.ios.macros.Add("COFFEEBEAN_DEMO=1");
                cfg.ios.buildSettings.Add(new CExportKv("ENABLE_BITCODE", "NO"));
            }
            return cfg;
        }
    }
}
