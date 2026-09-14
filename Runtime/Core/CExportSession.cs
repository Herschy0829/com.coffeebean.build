using System;
using System.Collections.Generic;
using System.IO;

namespace CoffeeBean
{
    /// <summary>
    /// 一次导出定制的会话上下文：平台 / 导出根 / 合并后配置 / 日志 / 幂等表 / iOS 落盘计划。
    /// 由 Unity 构建回调或 CI 批处理创建并交给 <see cref="CExportRunner"/>。
    /// </summary>
    public sealed class CExportSession
    {
        /// <summary>目标平台。</summary>
        public CExportPlatform Platform { get; }

        /// <summary>导出工程根目录（绝对路径）。</summary>
        public string ExportRoot { get; }

        /// <summary>Unity 工程根目录（可选；用于把 Assets/... 资产路径解析为绝对路径）。</summary>
        public string ProjectRoot { get; set; }

        /// <summary>合并后的配置（自定义步骤可改，落盘步骤最后统一读）。</summary>
        public CExportConfig Config { get; }

        /// <summary>会话日志。</summary>
        public CExportLog Log { get; } = new CExportLog();

        /// <summary>dry-run：只计算与记录、不写文件。</summary>
        public bool DryRun { get; set; }

        /// <summary>iOS 落盘计划（PBX 相关步骤产出，Editor #if UNITY_IOS 适配层消费）。</summary>
        public CIosPlan IosPlan { get; } = new CIosPlan();

        /// <summary>幂等签名表：已注入内容（节点 key / 行 hash / 库名 / 文件目标）。</summary>
        internal readonly HashSet<string> AppliedKeys = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// 本次会话已应用的注入项数（诊断用）。
        /// 注意：**跨次幂等并不依赖这张表** —— 它只是单次会话的审计，
        /// 真正的"重复导出零改动"由各注入器的文件内容比对保证。0 表示本次没有任何改动。
        /// </summary>
        internal int AppliedCount => AppliedKeys.Count;

        public CExportSession(CExportPlatform platform, string exportRoot, CExportConfig config)
        {
            Platform = platform;
            ExportRoot = exportRoot ?? throw new ArgumentNullException(nameof(exportRoot));
            Config = config ?? new CExportConfig();
        }

        /// <summary>记录某注入已被应用（单次会话审计；幂等由文件内容比对保证）。</summary>
        public void MarkApplied(string key)
        {
            if (!string.IsNullOrEmpty(key)) AppliedKeys.Add(key);
        }

        /// <summary>把相对导出根的路径解析为绝对路径（规范化分隔符）。</summary>
        public string Resolve(string relativePath)
        {
            string p = relativePath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(ExportRoot, p);
        }
    }
}
