using System;

namespace CoffeeBean
{
    /// <summary>导出定制异常：携带步骤、目标文件与原因，中止构建并让 Unity 显示明确失败。</summary>
    public sealed class CExportException : Exception
    {
        public string StepId { get; }
        public string TargetFile { get; }

        public CExportException(string stepId, string targetFile, string reason)
            : base(BuildMessage(stepId, targetFile, reason))
        {
            StepId = stepId;
            TargetFile = targetFile;
        }

        public CExportException(string stepId, string targetFile, string reason, Exception inner)
            : base(BuildMessage(stepId, targetFile, reason), inner)
        {
            StepId = stepId;
            TargetFile = targetFile;
        }

        static string BuildMessage(string stepId, string targetFile, string reason)
            => $"CoffeeBean.Build 步骤 [{stepId}] 失败" +
               (string.IsNullOrEmpty(targetFile) ? "" : $"（文件：{targetFile}）") +
               $"：{reason}";
    }
}
