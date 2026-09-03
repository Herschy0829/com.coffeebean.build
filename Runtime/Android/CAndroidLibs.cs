using System;
using System.Collections.Generic;
using System.IO;

namespace CoffeeBean
{
    /// <summary>
    /// Android libs 追加器：把 .aar/.jar 拷贝进导出工程的 libs 目录。
    /// 幂等：目标已存在同名文件且长度一致即跳过（视为已注入）。
    /// </summary>
    public static class CAndroidLibs
    {
        /// <summary>
        /// 拷贝一批库文件。
        /// </summary>
        /// <param name="sources">源文件路径（绝对，或相对 ProjectRoot 的 Assets/...）。</param>
        /// <param name="projectRoot">Unity 工程根（解析 Assets/... 用；可 null）。</param>
        /// <param name="targetDir">目标目录（绝对路径，已含 libs）。</param>
        /// <param name="report">输出动作清单（added / skipped）。</param>
        /// <returns>实际拷贝数。</returns>
        public static int CopyInto(System.Collections.Generic.IList<string> sources, string projectRoot, string targetDir, System.Collections.Generic.IList<string> report)
        {
            int copied = 0;
            if (sources == null) return 0;
            Directory.CreateDirectory(targetDir);

            foreach (var src in sources)
            {
                if (string.IsNullOrEmpty(src)) continue;
                string full = ResolveSource(src, projectRoot);
                if (!File.Exists(full))
                    throw new CExportException("libs", full, "源库文件不存在");
                string name = Path.GetFileName(full);
                string dst = Path.Combine(targetDir, name);
                if (File.Exists(dst))
                {
                    if (new FileInfo(dst).Length == new FileInfo(full).Length)
                    {
                        report?.Add($"skip (exists) {name}");
                        continue;
                    }
                    File.Delete(dst); // 同名不同内容 → 覆盖
                }
                File.Copy(full, dst);
                report?.Add($"add {name} -> {Path.GetFileName(targetDir)}/");
                copied++;
            }
            return copied;
        }

        static string ResolveSource(string source, string projectRoot)
        {
            if (Path.IsPathRooted(source)) return source;
            // Assets/... 相对 Unity 工程根
            if (source.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(projectRoot))
                return Path.Combine(projectRoot, source.Replace('/', Path.DirectorySeparatorChar));
            // 相对当前目录（测试用 fake 路径）
            return Path.GetFullPath(source);
        }
    }
}
