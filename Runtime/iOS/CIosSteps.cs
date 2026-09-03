using System.IO;

namespace CoffeeBean
{
    /// <summary>Info.plist 注入步骤（纯 XML 落盘，Windows/mac 通用）。</summary>
    public sealed class CIosPlistStep : IExportStep
    {
        public string Id => "ios.plist";
        public int Order => 10000;

        public bool IsActive(CExportSession s)
            => s.Platform == CExportPlatform.IOS && s.Config.ios.enabled &&
               (s.Config.ios.plistStrings.Count > 0 || s.Config.ios.skAdNetworkIds.Count > 0 ||
                s.Config.ios.urlSchemes.Count > 0);

        public void Execute(CExportSession s)
        {
            var cfg = s.Config.ios;
            string plistPath = s.Resolve("Info.plist");
            var plist = CIosPlist.Load(plistPath);
            if (plist == null)
            {
                s.Log.Warn($"{Id}: 未找到 Info.plist：{plistPath}");
                return;
            }
            if (s.DryRun)
            {
                s.Log.Step(Id, $"dry-run：将注入 {cfg.plistStrings.Count} 字符串 / {cfg.skAdNetworkIds.Count} SKAdNetwork / {cfg.urlSchemes.Count} scheme @ Info.plist");
                return;
            }
            foreach (var kv in cfg.plistStrings)
                if (plist.SetString(kv.key, kv.value)) s.Log.Step(Id, $"{kv.key} = {kv.value}");
            int sk = plist.EnsureSkAdNetworkIds(cfg.skAdNetworkIds);
            if (sk > 0) s.Log.Step(Id, $"SKAdNetworkItems +{sk}");
            int urls = plist.EnsureUrlSchemes(cfg.urlSchemes);
            if (urls > 0) s.Log.Step(Id, $"CFBundleURLTypes +{urls}");
            plist.Save();
        }
    }

    /// <summary>iOS PBX 计划生成步骤：把配置转为 <see cref="CIosPlan"/>（落盘由 Editor 适配层做）。</summary>
    public sealed class CIosPlanStep : IExportStep
    {
        public string Id => "ios.plan";
        public int Order => 10001;

        public bool IsActive(CExportSession s)
            => s.Platform == CExportPlatform.IOS && s.Config.ios.enabled;

        public void Execute(CExportSession s)
        {
            var cfg = s.Config.ios;
            var plan = s.IosPlan;
            plan.SystemFrameworks.AddRange(cfg.systemFrameworks);
            plan.WeakSystemFrameworks.AddRange(cfg.weakSystemFrameworks);
            foreach (var lib in cfg.localLibs)
                if (lib != null && !string.IsNullOrEmpty(lib.source))
                    plan.LocalLibs.Add(new CIosLocalLibConfig(lib.source, lib.embed, lib.target));
            plan.LinkerFlags.AddRange(cfg.linkerFlags);
            plan.Macros.AddRange(cfg.macros);
            plan.BuildSettings.AddRange(cfg.buildSettings);
            plan.Capabilities.AddRange(cfg.capabilities);
            s.Log.Step(Id, $"计划：system {plan.SystemFrameworks.Count} / local {plan.LocalLibs.Count} / flags {plan.LinkerFlags.Count} / macros {plan.Macros.Count} / bs {plan.BuildSettings.Count} / caps {plan.Capabilities.Count}");
        }
    }
}
