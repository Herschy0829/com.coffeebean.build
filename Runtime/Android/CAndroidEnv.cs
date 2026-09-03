using System;
using System.Collections.Generic;
using System.Text;

namespace CoffeeBean
{
    /// <summary>环境变量检查结果单项。</summary>
    public sealed class CExportEnvItem
    {
        public string Name;
        public string Value;
        public bool Found => !string.IsNullOrEmpty(Value);

        public CExportEnvItem(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }

    /// <summary>
    /// Android 构建环境检查器（对应"设置环境变量/CI 环境"诉求）：
    /// 读取 ANDROID_HOME / ANDROID_NDK_HOME / JAVA_HOME 与 Unity 侧 SDK 偏好路径，
    /// 产出报告；policy=abort 且缺失时抛异常中止。
    /// </summary>
    public static class CAndroidEnv
    {
        /// <summary>常见 Android 构建环境变量名。</summary>
        public static readonly string[] VarNames =
        {
            "ANDROID_HOME", "ANDROID_SDK_ROOT", "ANDROID_NDK_HOME", "JAVA_HOME"
        };

        /// <summary>收集环境变量值（不存在/为空记为空）。</summary>
        public static List<CExportEnvItem> Collect()
        {
            var list = new List<CExportEnvItem>();
            foreach (var name in VarNames)
            {
                string v = Environment.GetEnvironmentVariable(name);
                list.Add(new CExportEnvItem(name, string.IsNullOrWhiteSpace(v) ? null : v));
            }
            return list;
        }

        /// <summary>
        /// 检查环境并返回报告文本；policy=abort 且存在缺失项时抛 CExportException。
        /// </summary>
        public static string Check(string policy)
        {
            var items = Collect();
            var sb = new StringBuilder();
            bool missing = false;
            foreach (var it in items)
            {
                sb.AppendLine($"{it.Name}: {(it.Found ? it.Value : "(missing)")}");
                if (!it.Found) missing = true;
            }
            if (missing && policy == "abort")
                throw new CExportException("env", null, "构建环境变量缺失（policy=abort）：\n" + sb);
            return sb.ToString().TrimEnd();
        }
    }
}
