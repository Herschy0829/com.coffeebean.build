using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CoffeeBean
{
    /// <summary>
    /// Google Play Asset Delivery（PAD）asset pack 落地器 + 内置步骤。
    /// 在导出的 Gradle 工程内为每个配置的 pack 生成：
    ///   &lt;exportRoot&gt;/&lt;pack&gt;/build.gradle          （com.android.asset-pack 插件）
    ///   &lt;exportRoot&gt;/&lt;pack&gt;/src/main/AndroidManifest.xml
    ///   &lt;exportRoot&gt;/&lt;pack&gt;/src/main/assets/...       （源目录内容幂等拷贝）
    ///   settings.gradle 追加 include ':&lt;pack&gt;'
    /// 存在非 install-time pack 时，向 unityLibrary/build.gradle dependencies 注入
    ///   com.google.android.play:core（运行时下载 fast-follow/on-demand 包）。
    /// </summary>
    public sealed class CAndroidAssetPacksStep : IExportStep
    {
        public const string PlayCoreArtifact = "com.google.android.play:core:1.10.3";

        public string Id => "android.assetpacks";
        public int Order => 10010;

        public bool IsActive(CExportSession s)
            => s.Platform == CExportPlatform.Android && s.Config.android.enabled &&
               s.Config.android.assetPacks.Count > 0;

        public void Execute(CExportSession s)
        {
            var cfg = s.Config.android;
            bool anyNonInstallTime = false;

            foreach (var pack in cfg.assetPacks)
            {
                if (pack == null || string.IsNullOrEmpty(pack.name)) continue;
                ValidatePackName(pack.name);
                if (pack.NeedsPlayCore) anyNonInstallTime = true;

                string packRoot = s.Resolve(pack.name);
                if (s.DryRun)
                {
                    s.Log.Step(Id, $"dry-run：将生成 asset pack '{pack.name}'（{pack.deliveryType}），资源 {pack.sourceFolders.Count} 个源目录");
                    continue;
                }

                // 1) pack/build.gradle（幂等：已含插件与 packName 则跳过）
                string gradlePath = Path.Combine(packRoot, "build.gradle");
                string gradleContent = BuildPackGradle(pack.name, pack.deliveryType);
                if (File.Exists(gradlePath) && File.ReadAllText(gradlePath).Contains("com.android.asset-pack"))
                {
                    s.Log.Step(Id, $"pack {pack.name}: build.gradle 已存在，跳过");
                }
                else
                {
                    Directory.CreateDirectory(packRoot);
                    File.WriteAllText(gradlePath, gradleContent, new UTF8Encoding(false));
                    s.Log.Step(Id, $"pack {pack.name}: 生成 build.gradle（{pack.deliveryType}）");
                }
                s.MarkApplied($"assetpack:{pack.name}:gradle");

                // 2) pack/src/main/AndroidManifest.xml（幂等：已含本 pack package 则跳过）
                string manifestDir = Path.Combine(packRoot, "src", "main");
                string manifestPath = Path.Combine(manifestDir, "AndroidManifest.xml");
                string packPackage = BuildPackPackage(s, pack.name);
                if (File.Exists(manifestPath) && File.ReadAllText(manifestPath).Contains(packPackage))
                {
                    s.Log.Step(Id, $"pack {pack.name}: AndroidManifest.xml 已存在，跳过");
                }
                else
                {
                    Directory.CreateDirectory(manifestDir);
                    File.WriteAllText(manifestPath, BuildPackManifest(packPackage), new UTF8Encoding(false));
                    s.Log.Step(Id, $"pack {pack.name}: 生成 AndroidManifest.xml（package={packPackage}）");
                }
                s.MarkApplied($"assetpack:{pack.name}:manifest");

                // 3) 源目录内容 → src/main/assets/（文件级幂等：同名同大小跳过）
                string assetsDir = Path.Combine(manifestDir, "assets");
                foreach (var src in pack.sourceFolders)
                {
                    if (string.IsNullOrEmpty(src)) continue;
                    string srcFull = ResolveSource(src, s.ProjectRoot);
                    if (!Directory.Exists(srcFull))
                    {
                        s.Log.Warn($"{Id}: 源目录不存在（跳过）：{srcFull}");
                        continue;
                    }
                    int copied = CopyDirectoryContent(srcFull, assetsDir, s.Log, Id);
                    if (copied > 0) s.Log.Step(Id, $"pack {pack.name}: 资源拷贝 +{copied}（来自 {src}）");
                }
                s.MarkApplied($"assetpack:{pack.name}:assets");

                // 4) settings.gradle 追加 include ':<pack>'（行级幂等）
                EnsureSettingsInclude(s, pack.name);
            }

            if (anyNonInstallTime)
            {
                // 4) Play Core 依赖（unityLibrary/build.gradle dependencies 内）
                if (s.DryRun)
                {
                    s.Log.Step(Id, "dry-run：将注入 Play Core 依赖（存在 fast-follow/on-demand pack）");
                    return;
                }
                string gradleFile = s.Resolve("unityLibrary/build.gradle");
                if (!File.Exists(gradleFile))
                {
                    s.Log.Warn($"{Id}: unityLibrary/build.gradle 不存在，跳过 Play Core 注入");
                }
                else
                {
                    int n = CGradleFile.Inject(gradleFile, "dependencies {", new[] { "implementation '" + PlayCoreArtifact + "'" });
                    if (n > 0)
                    {
                        s.Log.Step(Id, $"Play Core 依赖 +1（{PlayCoreArtifact}）");
                        s.MarkApplied("assetpack:playcore");
                    }
                }
            }
        }

        /// <summary>settings.gradle 追加 include ':<packName>'（已存在任意引号形式则跳过）。</summary>
        static void EnsureSettingsInclude(CExportSession s, string packName)
        {
            string settingsPath = s.Resolve("settings.gradle");
            if (!File.Exists(settingsPath))
            {
                s.Log.Warn("android.assetpacks: settings.gradle 不存在（跳过 include " + packName + "）");
                return;
            }
            var lines = new List<string>(File.ReadAllLines(settingsPath, Encoding.UTF8));
            string needleSingle = $"include ':{packName}'";
            string needleDouble = $"include \":{packName}\"";
            foreach (var l in lines)
            {
                string t = l.Trim();
                if (t == needleSingle || t == needleDouble || t.StartsWith("include ") && t.Contains(":" + packName + "'") || t.Contains("\":" + packName + "\""))
                {
                    s.Log.Step("android.assetpacks", $"settings.gradle 已包含 {packName}，跳过");
                    return;
                }
            }
            if (s.DryRun)
            {
                s.Log.Step("android.assetpacks", $"dry-run：settings.gradle 将追加 include ':{packName}'");
                return;
            }
            lines.Add(needleSingle);
            File.WriteAllLines(settingsPath, lines, new UTF8Encoding(false));
            s.Log.Step("android.assetpacks", $"settings.gradle + include ':{packName}'");
        }

        // ---------- 生成物模板 ----------

        static void ValidatePackName(string name)
        {
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (!(char.IsLower(c) || char.IsDigit(c) || c == '_'))
                    throw new CExportException("android.assetpacks", null,
                        $"asset pack 名 '{name}' 非法：仅允许小写字母/数字/下划线");
            }
        }

        static string BuildPackGradle(string packName, string deliveryType)
            => "apply plugin: 'com.android.asset-pack'\n" +
               "\n" +
               "assetPack {\n" +
               $"    packName = '{packName}'\n" +
               "    dynamicDelivery {\n" +
               $"        deliveryType = '{deliveryType}'\n" +
               "    }\n" +
               "}\n";

        static string BuildPackManifest(string package)
            => "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
               "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"\n" +
               $"    package=\"{package}\">\n" +
               "</manifest>\n";

        /// <summary>pack manifest package：&lt;basePackage&gt;.assetpacks.&lt;pack&gt;；读不到 base 用 coffeebean.assetpacks.&lt;pack&gt;。</summary>
        static string BuildPackPackage(CExportSession s, string packName)
        {
            string basePkg = ReadBasePackage(s);
            return (string.IsNullOrEmpty(basePkg) ? "coffeebean" : basePkg) + ".assetpacks." + packName;
        }

        /// <summary>从 launcher（或 unityLibrary）manifest 根节点 package 属性读应用包名。</summary>
        static string ReadBasePackage(CExportSession s)
        {
            foreach (var rel in new[] { "launcher/src/main/AndroidManifest.xml", "unityLibrary/src/main/AndroidManifest.xml" })
            {
                string path = s.Resolve(rel);
                if (!File.Exists(path)) continue;
                string text = File.ReadAllText(path);
                int idx = text.IndexOf("package=\"", StringComparison.Ordinal);
                if (idx < 0) continue;
                idx += "package=\"".Length;
                int end = text.IndexOf('"', idx);
                if (end > idx) return text.Substring(idx, end - idx);
            }
            return null;
        }

        static string ResolveSource(string source, string projectRoot)
        {
            if (Path.IsPathRooted(source)) return source;
            if (source.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(projectRoot))
                return Path.Combine(projectRoot, source.Replace('/', Path.DirectorySeparatorChar));
            return Path.GetFullPath(source);
        }

        /// <summary>递归拷贝 src 目录**内容**到 dst 目录；文件级幂等（同名同长度跳过）。返回拷贝文件数。</summary>
        static int CopyDirectoryContent(string srcDir, string dstDir, CExportLog log, string stepId)
        {
            Directory.CreateDirectory(dstDir);
            int copied = 0;
            foreach (var file in Directory.GetFiles(srcDir))
            {
                string name = Path.GetFileName(file);
                string dst = Path.Combine(dstDir, name);
                if (File.Exists(dst) && new FileInfo(dst).Length == new FileInfo(file).Length) continue;
                File.Copy(file, dst, true);
                copied++;
            }
            foreach (var sub in Directory.GetDirectories(srcDir))
            {
                string subName = Path.GetFileName(sub);
                copied += CopyDirectoryContent(sub, Path.Combine(dstDir, subName), log, stepId);
            }
            return copied;
        }
    }
}
