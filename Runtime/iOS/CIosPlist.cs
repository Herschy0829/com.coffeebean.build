using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace CoffeeBean
{
    /// <summary>
    /// Info.plist 注入器（自研 XML 读写，Windows 可测；mac 上亦可用——输出为标准 plist XML）。
    /// 幂等合并：string 键按 policy 覆盖/跳过；SKAdNetworkItems 按 SKAdNetworkIdentifier 去重；
    /// CFBundleURLTypes 按 scheme 去重。
    /// </summary>
    public sealed class CIosPlist
    {
        const string PlistNs = "";

        readonly XmlDocument _doc;
        XmlElement _rootDict;

        /// <summary>处理的 plist 绝对路径。</summary>
        public string FilePath { get; }

        CIosPlist(string path, XmlDocument doc)
        {
            FilePath = path;
            _doc = doc;
            _rootDict = FindRootDict(doc);
            if (_rootDict == null)
                throw new CExportException("plist", path, "Info.plist 缺少根 <dict>");
        }

        public static CIosPlist Load(string path)
        {
            if (!File.Exists(path)) return null;
            var doc = new XmlDocument();
            // plist 带 DOCTYPE；.NET Core/Unity 默认 Prohibit DTD，须显式允许解析且不联网
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Parse,
                XmlResolver = null
            };
            using (var reader = XmlReader.Create(path, settings))
            {
                doc.Load(reader);
            }
            return new CIosPlist(path, doc);
        }

        static XmlElement FindRootDict(XmlDocument doc)
        {
            if (doc.DocumentElement == null) return null;
            foreach (XmlNode n in doc.DocumentElement.ChildNodes)
                if (n is XmlElement e && e.LocalName == "dict") return e;
            return null;
        }

        // ---------- 注入 API ----------

        /// <summary>设置字符串键。policy=skip：已存在不覆盖；overwrite：覆盖。</summary>
        public bool SetString(string key, string value, string policy = "overwrite")
        {
            if (string.IsNullOrEmpty(key)) return false;
            XmlElement valueNode = FindKeyValue(_rootDict, key);
            if (valueNode != null)
            {
                if (policy == "skip") return false;
                ReplaceWithString(valueNode, value);
                return true;
            }
            AppendString(_rootDict, key, value);
            return true;
        }

        /// <summary>设置布尔键（key → &lt;true/&gt; 或 &lt;false/&gt;）。policy 同上。</summary>
        public bool SetBool(string key, bool value, string policy = "overwrite")
        {
            if (string.IsNullOrEmpty(key)) return false;
            XmlElement valueNode = FindKeyValue(_rootDict, key);
            if (valueNode != null)
            {
                if (policy == "skip") return false;
                ReplaceWithBool(valueNode, value);
                return true;
            }
            AppendKey(_rootDict, key);
            var b = _doc.CreateElement(value ? "true" : "false");
            _rootDict.AppendChild(b);
            return true;
        }

        /// <summary>确保 SKAdNetworkItems 数组包含给定 identifier（按 dict 的 SKAdNetworkIdentifier 去重）。</summary>
        public int EnsureSkAdNetworkIds(IEnumerable<string> ids)
        {
            int added = 0;
            XmlElement array = EnsureArray(_rootDict, "SKAdNetworkItems");
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (ArrayContainsDictWithKey(array, "SKAdNetworkIdentifier", id)) continue;
                var dict = _doc.CreateElement("dict");
                AppendString(dict, "SKAdNetworkIdentifier", id);
                array.AppendChild(dict);
                added++;
            }
            return added;
        }

        /// <summary>确保 CFBundleURLTypes 数组含给定 scheme 的条目（按 scheme 去重，已存在同 scheme 跳过）。</summary>
        public int EnsureUrlSchemes(IEnumerable<string> schemes)
        {
            int added = 0;
            XmlElement types = EnsureArray(_rootDict, "CFBundleURLTypes");
            foreach (var scheme in schemes)
            {
                if (string.IsNullOrEmpty(scheme)) continue;
                if (ArrayContainsDictWithKey(types, "CFBundleURLSchemes", scheme, true)) continue;
                var dict = _doc.CreateElement("dict");
                AppendKey(dict, "CFBundleURLSchemes");
                var arr = _doc.CreateElement("array");
                var s = _doc.CreateElement("string");
                s.InnerText = scheme;
                arr.AppendChild(s);
                dict.AppendChild(arr);
                types.AppendChild(dict);
                added++;
            }
            return added;
        }

        /// <summary>落盘（plist XML，UTF-8，Apple DOCTYPE 保留）。</summary>
        public void Save()
        {
            using (var writer = XmlWriter.Create(FilePath, new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "\t", // plist 惯例 Tab 缩进
                Encoding = new UTF8Encoding(false),
                NewLineChars = "\n",
                NewLineHandling = NewLineHandling.Replace
            }))
            {
                _doc.Save(writer);
            }
        }

        // ---------- 内部工具 ----------

        static XmlElement FindKeyValue(XmlElement dict, string key)
        {
            for (int i = 0; i < dict.ChildNodes.Count - 1; i++)
            {
                if (dict.ChildNodes[i] is XmlElement e && e.LocalName == "key" && e.InnerText == key)
                    return dict.ChildNodes[i + 1] as XmlElement; // key 后紧跟值节点
            }
            return null;
        }

        static void AppendKey(XmlElement dict, string key)
        {
            var k = dict.OwnerDocument.CreateElement("key");
            k.InnerText = key;
            dict.AppendChild(k);
        }

        void AppendString(XmlElement dict, string key, string value)
        {
            if (key != null) AppendKey(dict, key);
            var s = _doc.CreateElement("string");
            s.InnerText = value ?? "";
            dict.AppendChild(s);
        }

        static void ReplaceWithString(XmlElement oldValueNode, string value)
        {
            var parent = oldValueNode.ParentNode as XmlElement;
            var doc = oldValueNode.OwnerDocument;
            var s = doc.CreateElement("string");
            s.InnerText = value ?? "";
            parent.ReplaceChild(s, oldValueNode);
        }

        static void ReplaceWithBool(XmlElement oldValueNode, bool value)
        {
            var parent = oldValueNode.ParentNode as XmlElement;
            var doc = oldValueNode.OwnerDocument;
            var b = doc.CreateElement(value ? "true" : "false");
            parent.ReplaceChild(b, oldValueNode);
        }

        XmlElement EnsureArray(XmlElement dict, string key)
        {
            XmlElement existing = FindKeyValue(dict, key);
            if (existing != null && existing.LocalName == "array") return existing;
            if (existing != null) dict.RemoveChild(existing); // 类型不符 → 替换
            AppendKey(dict, key);
            var arr = _doc.CreateElement("array");
            dict.AppendChild(arr);
            return arr;
        }

        static bool ArrayContainsDictWithKey(XmlElement array, string dictKey, string value, bool valueIsArrayOfStrings = false)
        {
            foreach (XmlNode n in array.ChildNodes)
            {
                if (!(n is XmlElement e) || e.LocalName != "dict") continue;
                XmlElement v = FindKeyValue(e, dictKey);
                if (v == null) continue;
                if (valueIsArrayOfStrings)
                {
                    // CFBundleURLSchemes 值是 array<string>：检查是否含该 scheme
                    if (v.LocalName != "array") continue;
                    foreach (XmlNode s in v.ChildNodes)
                        if (s is XmlElement se && se.LocalName == "string" && se.InnerText == value) return true;
                    continue;
                }
                if (v.LocalName == "string" && v.InnerText == value) return true;
            }
            return false;
        }
    }
}
