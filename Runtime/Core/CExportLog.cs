using System;
using System.Collections.Generic;
using System.Text;

namespace CoffeeBean
{
    /// <summary>
    /// 导出会话日志：收集步骤改动清单，Run 结束可写盘（CoffeeBeanExport.log）。
    /// 内部收集不即时打 Unity 日志，避免测试噪音；汇总一次性经 CLog 输出。
    /// </summary>
    public sealed class CExportLog
    {
        readonly List<string> _entries = new List<string>();

        /// <summary>会话是否发生错误（有 Error 条目）。</summary>
        public bool HasErrors { get; private set; }

        /// <summary>全部条目（只读视图）。</summary>
        public IReadOnlyList<string> Entries => _entries;

        public void Info(string message) => _entries.Add("[Info] " + message);

        public void Warn(string message) => _entries.Add("[Warn] " + message);

        public void Error(string message)
        {
            HasErrors = true;
            _entries.Add("[Error] " + message);
        }

        public void Step(string stepId, string message) => _entries.Add($"[{stepId}] {message}");

        /// <summary>输出为多行文本（写盘/展示用）。</summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            foreach (var e in _entries) sb.AppendLine(e);
            return sb.ToString();
        }

        /// <summary>把日志经框架 CLog 汇总输出（Editor 控制台看一次）。</summary>
        public void FlushToConsole(string tag)
        {
            string body = ToText();
            if (HasErrors) CLog.Error(tag, body);
            else if (_entries.Count > 0) CLog.Info(tag, body);
        }
    }
}
