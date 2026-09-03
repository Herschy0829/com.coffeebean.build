using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CoffeeBean
{
    /// <summary>
    /// build.gradle 文本锚点注入器：在锚点块（如 "dependencies {"）内插入行。
    /// 幂等：目标行 Trim 后与已有行相同即跳过；保留文件其余内容原样。
    /// </summary>
    public static class CGradleFile
    {
        /// <summary>在某文件锚点块内注入行。返回实际插入行数。</summary>
        public static int Inject(string path, string anchor, IEnumerable<string> lines)
        {
            if (!File.Exists(path))
                throw new CExportException("gradle", path, "文件不存在：" + path);
            if (string.IsNullOrEmpty(anchor) || lines == null) return 0;

            string[] content = File.ReadAllLines(path, Encoding.UTF8);
            int anchorIndex = -1;
            for (int i = 0; i < content.Length; i++)
            {
                if (content[i].Trim().Equals(anchor, StringComparison.Ordinal))
                {
                    anchorIndex = i;
                    break;
                }
            }
            if (anchorIndex < 0)
                throw new CExportException("gradle", path, $"未找到锚点块：{anchor}");

            string indent = LeadingWhitespace(content[anchorIndex]) + "    "; // 块内容缩进 = 锚点缩进 + 4
            var toInsert = new List<string>();
            int inserted = 0;
            foreach (var rawLine in lines)
            {
                if (string.IsNullOrEmpty(rawLine)) continue;
                foreach (var sub in rawLine.Split('\n'))
                {
                    string line = sub.TrimEnd('\r');
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string trimmed = line.Trim();
                    if (LineExists(content, trimmed)) continue;      // 幂等：已有
                    if (toInsert.Exists(x => x.Trim() == trimmed)) continue; // 同批去重
                    toInsert.Add(indent + trimmed);
                    inserted++;
                }
            }
            if (inserted == 0) return 0;

            var sb = new StringBuilder();
            for (int i = 0; i <= anchorIndex; i++) sb.AppendLine(content[i]);
            foreach (var l in toInsert) sb.AppendLine(l);
            for (int i = anchorIndex + 1; i < content.Length; i++) sb.AppendLine(content[i]);

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return inserted;
        }

        static bool LineExists(string[] content, string trimmed)
        {
            foreach (var line in content)
                if (line.Trim() == trimmed) return true;
            return false;
        }

        static string LeadingWhitespace(string line)
        {
            int i = 0;
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
            return line.Substring(0, i);
        }
    }
}
