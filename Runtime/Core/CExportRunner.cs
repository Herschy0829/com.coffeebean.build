using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CoffeeBean
{
    /// <summary>
    /// 导出定制管线：按序执行"自定义步骤（改配置）→ 内置落盘步骤（写文件）"。
    /// dry-run 时内置步骤只记录不写盘；任何步骤抛 <see cref="CExportException"/> 即中止。
    /// </summary>
    public static class CExportRunner
    {
        /// <summary>运行一次导出定制。返回是否成功（false = 配置关闭或平台不匹配，未执行）。</summary>
        public static bool Run(CExportSession session, IEnumerable<IExportStep> customSteps = null)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (!session.Config.PlatformEnabled(session.Platform))
            {
                session.Log.Info($"配置未启用平台 {session.Platform}，跳过。");
                return false;
            }

            session.Log.Info($"开始 {session.Platform} 导出定制 @ {session.ExportRoot}" +
                             (session.DryRun ? "（dry-run：只计算不写盘）" : ""));

            // 1) 自定义步骤：声明注入到 Config（SDK 集成方；Order 小在前）
            var customs = (customSteps ?? Enumerable.Empty<IExportStep>())
                          .Where(s => s != null && s.IsActive(session))
                          .OrderBy(s => s.Order)
                          .ToList();
            foreach (var step in customs) ExecuteStep(session, step);

            // 2) 内置落盘步骤：读最终 Config 写文件
            var builtins = BuiltinSteps(session.Platform)
                           .Where(s => s.IsActive(session))
                           .OrderBy(s => s.Order)
                           .ToList();
            foreach (var step in builtins) ExecuteStep(session, step);

            session.Log.Info($"完成：共 {session.Log.Entries.Count - 1} 条记录。");
            return true;
        }

        static void ExecuteStep(CExportSession session, IExportStep step)
        {
            try
            {
                step.Execute(session);
                session.Log.Step(step.Id, "ok");
            }
            catch (CExportException)
            {
                session.Log.FlushToConsole("CoffeeBean.Build");
                throw;
            }
            catch (Exception e)
            {
                session.Log.FlushToConsole("CoffeeBean.Build");
                throw new CExportException(step.Id, null, e.Message, e);
            }
        }

        /// <summary>该平台的内置落盘步骤（幂等、dry-run 感知）。</summary>
        static IEnumerable<IExportStep> BuiltinSteps(CExportPlatform platform)
        {
            if (platform == CExportPlatform.Android)
            {
                yield return new CAndroidManifestStep();
                yield return new CAndroidGradleStep();
                yield return new CAndroidPropertiesStep();
                yield return new CAndroidLibsStep();
                yield return new CAndroidResStep();
                yield return new CAndroidAssetPacksStep();
                yield return new CAndroidEnvStep();
            }
            else
            {
                yield return new CIosPlistStep();
                yield return new CIosPlanStep();
            }
        }

        /// <summary>把日志写盘到导出根（供 CI 读取）。</summary>
        public static void WriteLogFile(CExportSession session)
        {
            if (session.DryRun) return;
            try
            {
                string path = Path.Combine(session.ExportRoot, "CoffeeBeanExport.log");
                File.WriteAllText(path, session.Log.ToText());
            }
            catch (Exception e)
            {
                session.Log.Warn("写日志文件失败：" + e.Message);
            }
        }
    }
}
