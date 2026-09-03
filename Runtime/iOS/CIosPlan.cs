using System.Collections.Generic;

namespace CoffeeBean
{
    /// <summary>
    /// iOS 落盘计划（纯数据）：PBX 相关步骤（framework/库/Build Settings/Capability/文件入 target）
    /// 在这里产出计划，Editor 的 `#if UNITY_IOS` 适配层消费并落盘到 Xcode 工程。
    /// 纯 C# 可测：Windows 上也能验证计划内容。
    /// </summary>
    public sealed class CIosPlan
    {
        /// <summary>要追加的系统 framework 名（如 AdSupport）。</summary>
        public readonly List<string> SystemFrameworks = new List<string>();

        /// <summary>弱链接系统 framework 名。</summary>
        public readonly List<string> WeakSystemFrameworks = new List<string>();

        /// <summary>本地 framework/静态库/源文件条目（拷贝进工程并注册 target）。</summary>
        public readonly List<CIosLocalLibConfig> LocalLibs = new List<CIosLocalLibConfig>();

        /// <summary>Other Linker Flags 追加（-ObjC 等）。</summary>
        public readonly List<string> LinkerFlags = new List<string>();

        /// <summary>GCC_PREPROCESSOR_DEFINITIONS 追加宏。</summary>
        public readonly List<string> Macros = new List<string>();

        /// <summary>Build Settings 覆盖键值。</summary>
        public readonly List<CExportKv> BuildSettings = new List<CExportKv>();

        /// <summary>Capability 类型名列表。</summary>
        public readonly List<string> Capabilities = new List<string>();

        /// <summary>是否有任何 PBX 侧工作要做。</summary>
        public bool HasPbxWork =>
            SystemFrameworks.Count > 0 || WeakSystemFrameworks.Count > 0 || LocalLibs.Count > 0 ||
            LinkerFlags.Count > 0 || Macros.Count > 0 || BuildSettings.Count > 0 || Capabilities.Count > 0;
    }
}
