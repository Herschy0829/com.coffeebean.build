using System;
using System.Collections.Generic;
using System.IO;

namespace CoffeeBean
{
    /// <summary>
    /// 框架级「必需」Android Gradle 依赖落盘 —— build 模块是首选执行者。
    ///
    /// 需求来源是各模块通过 <see cref="CAndroidGradleRequirements"/> 登记的条目
    /// （例如 tools 的应用内评价需要 <c>com.google.android.play:review</c>）。
    ///
    /// **为什么要独立于导出配置管线**：现有管线受 <c>CExportConfig</c> 门控
    /// （没配置资产、或没启用 Android 段就整体跳过）。而「框架必需依赖」是框架自身的硬需求，
    /// 不该取决于用户有没有配过导出定制 —— 否则没配置的工程会静默漏依赖，
    /// 表现成"应用内评价在真机上永远返回 Unavailable"。
    ///
    /// 因此本类由 <c>ExportBuildCallbacks</c> 在每次 Android 导出/构建时无条件调用，
    /// 内部按行幂等（已存在则零改动），重复执行安全。
    /// </summary>
    public static class CAndroidRequiredDeps
    {
        /// <summary>步骤 id（写进导出日志）。</summary>
        public const string StepId = "android.requiredDeps";

        /// <summary>依赖所在的 Gradle 文件（相对 Gradle 工程根）。</summary>
        public const string GradleRelativePath = "unityLibrary/build.gradle";

        /// <summary>注入锚点块。</summary>
        public const string Anchor = "dependencies {";

        /// <summary>
        /// 把框架必需依赖注入导出的 Gradle 工程。返回实际插入行数。
        /// **失败只告警不抛出**：依赖缺失应当由运行时接口报 Unavailable 暴露，
        /// 而不是让整个打包失败。
        /// </summary>
        public static int Inject(string exportRoot, IReadOnlyList<CAndroidGradleRequirement> requirements,
            CExportLog log = null)
        {
            if (string.IsNullOrEmpty(exportRoot) || requirements == null || requirements.Count == 0) return 0;

            string gradlePath = Path.Combine(exportRoot, GradleRelativePath);
            if (!File.Exists(gradlePath))
            {
                log?.Warn($"{StepId}: {GradleRelativePath} 不存在，跳过框架必需依赖注入");
                return 0;
            }

            var lines = new List<string>(requirements.Count);
            var reasons = new List<string>(requirements.Count);
            foreach (CAndroidGradleRequirement requirement in requirements)
            {
                // 空白串要挡掉：否则会写出 implementation '' 这种坏行
                if (requirement == null || string.IsNullOrWhiteSpace(requirement.Artifact)) continue;
                lines.Add(requirement.ToGradleLine());
                reasons.Add(requirement.ToString());
            }
            if (lines.Count == 0) return 0;

            try
            {
                int inserted = CGradleFile.Inject(gradlePath, Anchor, lines);
                if (inserted > 0)
                {
                    log?.Step(StepId, $"{GradleRelativePath} +{inserted} 行：{string.Join("；", reasons)}");
                }
                else
                {
                    log?.Step(StepId, $"框架必需依赖已存在，无需改动（{string.Join("；", reasons)}）");
                }
                return inserted;
            }
            catch (CExportException e)
            {
                log?.Warn($"{StepId}: 注入失败（不阻断构建）：{e.Message}");
                return 0;
            }
        }
    }
}
