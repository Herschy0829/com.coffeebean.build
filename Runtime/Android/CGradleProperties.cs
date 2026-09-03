using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CoffeeBean
{
    /// <summary>
    /// properties 文件（gradle.properties / local.properties）key=value 幂等读写：
    /// 已存在同 key 更新值（保留注释与顺序），缺 key 追加到末尾；无 BOM UTF-8 保存。
    /// </summary>
    public static class CGradleProperties
    {
        /// <summary>确保 key=value 存在；返回是否发生写入。文件不存在则创建。</summary>
        public static bool EnsureKeyValue(string path, string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                throw new CExportException("properties", path, "key 为空");

            var lines = File.Exists(path)
                ? new List<string>(File.ReadAllLines(path, Encoding.UTF8))
                : new List<string>();

            for (int i = 0; i < lines.Count; i++)
            {
                string t = lines[i].Trim();
                if (t.StartsWith("#") || t.StartsWith("!")) continue;
                int eq = t.IndexOf('=');
                if (eq <= 0) continue;
                if (t.Substring(0, eq).Trim() == key)
                {
                    if (t.Substring(eq + 1).Trim() == value) return false; // 已一致
                    lines[i] = key + "=" + value;
                    WriteAll(path, lines);
                    return true;
                }
            }

            lines.Add(key + "=" + value);
            WriteAll(path, lines);
            return true;
        }

        static void WriteAll(string path, List<string> lines)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllLines(path, lines, new UTF8Encoding(false));
        }
    }
}
