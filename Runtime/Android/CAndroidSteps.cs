using System.IO;

namespace CoffeeBean
{
    /// <summary>AndroidManifest 注入步骤（权限/uses-feature/meta-data/application 属性）。</summary>
    public sealed class CAndroidManifestStep : IExportStep
    {
        public string Id => "android.manifest";
        public int Order => 10000;

        public bool IsActive(CExportSession s)
            => s.Platform == CExportPlatform.Android && s.Config.android.enabled &&
               (s.Config.android.permissions.Count > 0 || s.Config.android.usesFeatures.Count > 0 ||
                s.Config.android.applicationMetaData.Count > 0 || s.Config.android.applicationAttributes.Count > 0);

        public void Execute(CExportSession s)
        {
            var cfg = s.Config.android;
            string[] paths = CAndroidManifest.FindManifests(s.ExportRoot, cfg.manifestTarget);
            bool any = false;
            foreach (var path in paths)
            {
                var manifest = CAndroidManifest.Load(path);
                if (manifest == null)
                {
                    s.Log.Warn($"{Id}: 未找到 manifest（跳过）：{path}");
                    continue;
                }
                if (s.DryRun) { s.Log.Step(Id, $"dry-run：将注入 {path}"); any = true; continue; }

                // 每份 manifest 独立判定是否需要保存：早期版本把 any 当作"本次是否有改动"复用，
                // 导致第一份有改动后，后续 manifest 即使零改动也会被 Save()（无谓重排格式、产生 diff）。
                bool changed = false;
                foreach (var p in cfg.permissions)
                    if (manifest.EnsurePermission(p)) { s.Log.Step(Id, $"permission +{p}"); s.MarkApplied($"manifest:perm:{p}"); changed = true; }
                foreach (var f in cfg.usesFeatures)
                    if (manifest.EnsureUsesFeature(f)) { s.Log.Step(Id, $"uses-feature +{f}"); s.MarkApplied($"manifest:feature:{f}"); changed = true; }
                foreach (var kv in cfg.applicationMetaData)
                    if (manifest.EnsureApplicationMetaData(kv.key, kv.value)) { s.Log.Step(Id, $"meta-data {kv.key}={kv.value}"); s.MarkApplied($"manifest:meta:{kv.key}"); changed = true; }
                foreach (var kv in cfg.applicationAttributes)
                    if (manifest.SetApplicationAttribute(kv.key, kv.value)) { s.Log.Step(Id, $"application[{kv.key}]={kv.value}"); s.MarkApplied($"manifest:appattr:{kv.key}"); changed = true; }

                if (changed) manifest.Save();
                any |= changed;
            }
            if (!any && !s.DryRun) s.Log.Warn(Id + ": 无 manifest 被修改（检查 manifestTarget 与路径）");
        }
    }

    /// <summary>gradle 文本锚点注入步骤。</summary>
    public sealed class CAndroidGradleStep : IExportStep
    {
        public string Id => "android.gradle";
        public int Order => 10001;

        public bool IsActive(CExportSession s)
            => s.Platform == CExportPlatform.Android && s.Config.android.enabled &&
               s.Config.android.gradleFiles.Count > 0 && s.Config.android.gradleLines.Count > 0;

        public void Execute(CExportSession s)
        {
            var cfg = s.Config.android;
            foreach (var rel in cfg.gradleFiles)
            {
                string path = s.Resolve(rel);
                if (!File.Exists(path))
                {
                    s.Log.Warn($"{Id}: 文件不存在（跳过）：{path}");
                    continue;
                }
                if (s.DryRun)
                {
                    s.Log.Step(Id, $"dry-run：向 {rel} 锚点 {cfg.gradleAnchor} 注入 {cfg.gradleLines.Count} 行");
                    continue;
                }
                int n = CGradleFile.Inject(path, cfg.gradleAnchor, cfg.gradleLines);
                if (n > 0)
                {
                    s.Log.Step(Id, $"{rel} +{n} 行 @ {cfg.gradleAnchor}");
                    s.MarkApplied($"gradle:{rel}:{cfg.gradleAnchor}");
                }
            }
        }
    }

    /// <summary>gradle.properties / local.properties 键值注入步骤。</summary>
    public sealed class CAndroidPropertiesStep : IExportStep
    {
        public string Id => "android.properties";
        public int Order => 10002;

        public bool IsActive(CExportSession s)
            => s.Platform == CExportPlatform.Android && s.Config.android.enabled &&
               (s.Config.android.gradleProperties.Count > 0 || !string.IsNullOrEmpty(s.Config.android.localSdkDir));

        public void Execute(CExportSession s)
        {
            var cfg = s.Config.android;
            if (s.DryRun)
            {
                s.Log.Step(Id, "dry-run：将写入 gradle.properties / local.properties 键值");
                return;
            }
            if (cfg.gradleProperties.Count > 0)
            {
                string path = s.Resolve("gradle.properties");
                foreach (var kv in cfg.gradleProperties)
                    if (CGradleProperties.EnsureKeyValue(path, kv.key, kv.value))
                    {
                        s.Log.Step(Id, $"gradle.properties {kv.key}={kv.value}");
                        s.MarkApplied($"props:{kv.key}");
                    }
            }
            if (!string.IsNullOrEmpty(cfg.localSdkDir))
            {
                string path = s.Resolve("local.properties");
                if (CGradleProperties.EnsureKeyValue(path, "sdk.dir", cfg.localSdkDir))
                {
                    s.Log.Step(Id, $"local.properties sdk.dir={cfg.localSdkDir}");
                    s.MarkApplied("props:sdk.dir");
                }
            }
        }
    }

    /// <summary>libs（aar/jar）追加步骤。</summary>
    public sealed class CAndroidLibsStep : IExportStep
    {
        public string Id => "android.libs";
        public int Order => 10003;

        public bool IsActive(CExportSession s)
            => s.Platform == CExportPlatform.Android && s.Config.android.enabled &&
               s.Config.android.libSources.Count > 0;

        public void Execute(CExportSession s)
        {
            var cfg = s.Config.android;
            string targetDir = s.Resolve(cfg.libsTargetFolder);
            if (s.DryRun)
            {
                s.Log.Step(Id, $"dry-run：将拷贝 {cfg.libSources.Count} 个库到 {cfg.libsTargetFolder}");
                return;
            }
            int n = CAndroidLibs.CopyInto(cfg.libSources.ToArray(), s.ProjectRoot, targetDir, null);
            s.Log.Step(Id, $"{cfg.libsTargetFolder} +{n} 库");
        }
    }

    /// <summary>res/strings + proguard 注入步骤。</summary>
    public sealed class CAndroidResStep : IExportStep
    {
        public string Id => "android.res";
        public int Order => 10004;

        public bool IsActive(CExportSession s)
            => s.Platform == CExportPlatform.Android && s.Config.android.enabled &&
               (s.Config.android.resStrings.Count > 0 || s.Config.android.proguardLines.Count > 0);

        public void Execute(CExportSession s)
        {
            var cfg = s.Config.android;
            if (s.DryRun)
            {
                s.Log.Step(Id, "dry-run：将追加 strings.xml / proguard 规则");
                return;
            }
            if (cfg.resStrings.Count > 0)
            {
                // strings.xml 注入 unityLibrary（引擎 res 随 manifest 合并进最终包）
                string stringsPath = s.Resolve("unityLibrary/src/main/res/values/strings.xml");
                int n = CAndroidRes.AddStrings(stringsPath, cfg.resStrings);
                if (n > 0) { s.Log.Step(Id, $"strings.xml +{n}"); s.MarkApplied("res:strings"); }
            }
            if (cfg.proguardLines.Count > 0)
            {
                string pgPath = s.Resolve(cfg.proguardTargetFile);
                int n = CAndroidRes.AddProguardLines(pgPath, cfg.proguardLines);
                if (n > 0) { s.Log.Step(Id, $"proguard +{n} 行"); s.MarkApplied("res:proguard"); }
            }
        }
    }

    /// <summary>构建环境检查步骤（policy=warn 仅日志；abort 缺失即中止）。</summary>
    public sealed class CAndroidEnvStep : IExportStep
    {
        public string Id => "android.env";
        public int Order => 10005;

        public bool IsActive(CExportSession s) => s.Platform == CExportPlatform.Android && s.Config.android.enabled;

        public void Execute(CExportSession s)
        {
            if (s.DryRun)
            {
                s.Log.Step(Id, "dry-run：将检查构建环境变量");
                return;
            }
            string report = CAndroidEnv.Check(s.Config.android.envPolicy);
            s.Log.Step(Id, report.Replace("\n", " | "));
        }
    }
}
