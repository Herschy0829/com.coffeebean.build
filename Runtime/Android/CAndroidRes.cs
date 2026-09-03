using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace CoffeeBean
{
    /// <summary>
    /// Android res / proguard 注入器：
    /// - res/values/strings.xml 追加 &lt;string name&gt;（幂等，同 name 覆盖文本）
    /// - proguard 文件按行去重追加 keep 规则
    /// </summary>
    public static class CAndroidRes
    {
        /// <summary>strings.xml 追加字符串项（resources 根下）。文件不存在则创建最小骨架。</summary>
        public static int AddStrings(string path, IEnumerable<CExportKv> strings)
        {
            int changed = 0;
            bool existed = File.Exists(path);
            var doc = new XmlDocument();
            if (existed) doc.Load(path);
            else
            {
                doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));
                doc.AppendChild(doc.CreateElement("resources"));
            }
            XmlElement resources = doc.DocumentElement;
            foreach (var kv in strings)
            {
                if (kv == null || string.IsNullOrEmpty(kv.key)) continue;
                XmlElement found = null;
                foreach (XmlNode n in resources.ChildNodes)
                {
                    if (n is XmlElement e && e.LocalName == "string" && e.GetAttribute("name") == kv.key)
                    {
                        found = e;
                        break;
                    }
                }
                if (found != null)
                {
                    if (found.InnerText != kv.value)
                    {
                        found.InnerText = kv.value ?? "";
                        changed++;
                    }
                    continue;
                }
                XmlElement node = doc.CreateElement("string");
                node.SetAttribute("name", kv.key);
                node.InnerText = kv.value ?? "";
                resources.AppendChild(node);
                changed++;
            }
            if (changed == 0) return 0;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            doc.Save(path);
            return changed;
        }

        /// <summary>proguard 规则行去重追加；返回新增行数。</summary>
        public static int AddProguardLines(string path, IEnumerable<string> rules)
        {
            var lines = File.Exists(path)
                ? new List<string>(File.ReadAllLines(path, Encoding.UTF8))
                : new List<string>();
            int added = 0;
            foreach (var raw in rules)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                string line = raw.Trim();
                bool exists = false;
                foreach (var l in lines)
                    if (l.Trim() == line) { exists = true; break; }
                if (exists) continue;
                lines.Add(line);
                added++;
            }
            if (added == 0) return 0;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllLines(path, lines, new UTF8Encoding(false));
            return added;
        }
    }
}
