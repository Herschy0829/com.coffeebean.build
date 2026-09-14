using System;
using System.IO;
using System.Text;
using System.Xml;

namespace CoffeeBean
{
    /// <summary>
    /// AndroidManifest.xml 注入器（纯 C# / XmlDocument，幂等：先查后插）。
    /// 处理 launcher / unityLibrary 两份源 manifest 的权限、uses-feature、application meta-data 与属性。
    /// 落盘统一用 4 空格缩进 XmlWriter（UTF-8 无 BOM）。
    /// </summary>
    public sealed class CAndroidManifest
    {
        public const string AndroidNs = "http://schemas.android.com/apk/res/android";

        readonly XmlDocument _doc;
        readonly XmlElement _manifest;
        readonly XmlElement _application;

        /// <summary>android 命名空间在本文档绑定的前缀（根元素推断，默认 "android"）。</summary>
        readonly string _prefix;

        /// <summary>当前处理的 manifest 绝对路径（日志/异常定位）。</summary>
        public string FilePath { get; }

        CAndroidManifest(string path, XmlDocument doc)
        {
            FilePath = path;
            _doc = doc;
            _manifest = doc.DocumentElement; // AndroidManifest 根即 <manifest>
            _application = FindChild(_manifest, "application");
            _prefix = DetectPrefix(_manifest);
        }

        /// <summary>从根元素已有 android 属性推断前缀（无则 "android"）。</summary>
        static string DetectPrefix(XmlElement manifest)
        {
            if (manifest != null)
            {
                foreach (XmlAttribute a in manifest.Attributes)
                    if (a.NamespaceURI == AndroidNs && !string.IsNullOrEmpty(a.Prefix))
                        return a.Prefix;
            }
            return "android";
        }

        /// <summary>
        /// 创建 manifest 子元素（uses-permission/meta-data 等属于**无命名空间**，
        /// 只有属性在 android 命名空间——AndroidManifest 的 XML 惯例）。
        /// </summary>
        XmlElement CreateAndroidElement(string localName)
            => _doc.CreateElement(localName);

        /// <summary>设置 android 命名空间属性（显式前缀创建）。</summary>
        void SetAndroidAttr(XmlElement el, string localName, string value)
        {
            el.RemoveAttribute(localName, AndroidNs); // 幂等：先移除再挂新属性
            var attr = _doc.CreateAttribute(_prefix, localName, AndroidNs);
            attr.Value = value ?? "";
            el.SetAttributeNode(attr);
        }

        /// <summary>加载 manifest 文件（不存在返回 null）。</summary>
        public static CAndroidManifest Load(string path)
        {
            if (!File.Exists(path)) return null;
            var doc = new XmlDocument();
            doc.Load(path);
            if (doc.DocumentElement == null) return null;
            return new CAndroidManifest(path, doc);
        }

        /// <summary>按 Unity 导出工程结构定位 manifest 文件；target=launcher|unityLibrary|both（both 返回两份）。</summary>
        public static string[] FindManifests(string exportRoot, string target)
        {
            switch (target)
            {
                case "launcher":
                    return new[] { Path.Combine(exportRoot, "launcher", "src", "main", "AndroidManifest.xml") };
                case "unityLibrary":
                    return new[] { Path.Combine(exportRoot, "unityLibrary", "src", "main", "AndroidManifest.xml") };
                default:
                    return new[]
                    {
                        Path.Combine(exportRoot, "launcher", "src", "main", "AndroidManifest.xml"),
                        Path.Combine(exportRoot, "unityLibrary", "src", "main", "AndroidManifest.xml")
                    };
            }
        }

        // ---------- 注入 API（全部幂等） ----------

        /// <summary>确保存在 &lt;uses-permission android:name&gt;（已存在跳过）。</summary>
        public bool EnsurePermission(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (XmlElement e in _manifest.ChildNodes)
                if (e.LocalName == "uses-permission" && GetAndroidAttr(e, "name") == name)
                    return false; // 已存在
            var node = CreateAndroidElement("uses-permission");
            SetAndroidAttr(node, "name", name);
            AppendIndented(_manifest, node);
            return true;
        }

        /// <summary>确保存在 &lt;uses-feature android:name&gt;（已存在跳过）。</summary>
        public bool EnsureUsesFeature(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (XmlElement e in _manifest.ChildNodes)
                if (e.LocalName == "uses-feature" && GetAndroidAttr(e, "name") == name)
                    return false;
            var node = CreateAndroidElement("uses-feature");
            SetAndroidAttr(node, "name", name);
            AppendIndented(_manifest, node);
            return true;
        }

        /// <summary>确保 application 下存在 &lt;meta-data android:name&gt;；同 key 覆盖值（值相同则视为无变化）。</summary>
        public bool EnsureApplicationMetaData(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (_application == null)
                throw new CExportException("manifest", FilePath, "manifest 缺少 <application> 节点，无法注入 meta-data");
            foreach (XmlElement e in _application.ChildNodes)
            {
                if (e.LocalName != "meta-data") continue;
                if (GetAndroidAttr(e, "name") == key)
                {
                    // 必须比较后再返回：早期实现无条件 return true，导致"同 key 同值"的重复导出
                    // 也被判定为有改动 → manifest 每次导出都被重写（无谓的格式重排 diff），
                    // 直接破坏了模块承诺的幂等性。语义与 SetApplicationAttribute 对齐。
                    if (GetAndroidAttr(e, "value") == value) return false; // 值未变：无改动
                    SetAndroidAttr(e, "value", value);
                    return true; // 覆盖
                }
            }
            var node = CreateAndroidElement("meta-data");
            SetAndroidAttr(node, "name", key);
            SetAndroidAttr(node, "value", value);
            AppendIndented(_application, node);
            return true;
        }

        /// <summary>设置 &lt;application&gt; 属性（如 android:name / android:label / android:allowBackup）。attr 可带 android: 前缀。</summary>
        public bool SetApplicationAttribute(string attr, string value)
        {
            if (_application == null)
                throw new CExportException("manifest", FilePath, "manifest 缺少 <application> 节点，无法设置属性");
            string localName = StripPrefix(attr);
            string existing = GetAndroidAttr(_application, localName);
            SetAndroidAttr(_application, localName, value);
            return existing != value; // 有变化才 true
        }

        /// <summary>把 "android:name" 规范化为 localName "name"（SetAttribute 不接受带冒号名）。</summary>
        static string StripPrefix(string attr)
        {
            if (string.IsNullOrEmpty(attr)) return attr;
            int idx = attr.IndexOf(':');
            return idx >= 0 ? attr.Substring(idx + 1) : attr;
        }

        /// <summary>写入磁盘（4 空格缩进，UTF-8 无 BOM）。</summary>
        public void Save()
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "    ",
                Encoding = new UTF8Encoding(false),
                OmitXmlDeclaration = false,
                NewLineChars = "\n",
                NewLineHandling = NewLineHandling.Replace
            };
            using (var writer = XmlWriter.Create(FilePath, settings))
            {
                _doc.Save(writer);
            }
        }

        // ---------- 内部工具 ----------

        static XmlElement FindChild(XmlElement parent, string localName)
        {
            if (parent == null) return null;
            foreach (XmlNode n in parent.ChildNodes)
                if (n is XmlElement e && e.LocalName == localName) return e;
            return null;
        }

        static string GetAndroidAttr(XmlElement e, string localName)
            => e.HasAttribute(localName, AndroidNs) ? e.GetAttribute(localName, AndroidNs) : null;

        /// <summary>带缩进换行追加子节点（保持可读格式）。</summary>
        void AppendIndented(XmlElement parent, XmlElement child)
        {
            // 不手动插入空白文本：XmlWriter 缩进会对纯元素树自动格式化；
            // 手动文本节点反而会干扰 Indent（混排）。PreserveWhitespace 保持 false（Load 时丢弃空白）。
            parent.AppendChild(child);
        }
    }
}
