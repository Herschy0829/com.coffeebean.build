using System;
using System.IO;
using NUnit.Framework;

namespace CoffeeBean.Build.Tests
{
    /// <summary>
    /// fake 导出工程构造器：每个测试在临时目录建最小工程，TearDown 自动清理。
    /// </summary>
    public abstract class FakeExportTestBase
    {
        protected string Root;

        [SetUp]
        public void SetUp()
        {
            Root = Path.Combine(Path.GetTempPath(), "CBExportTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Root != null && Directory.Exists(Root)) Directory.Delete(Root, true); }
            catch { /* 清理失败忽略（只读文件等） */ }
        }

        protected string WriteFile(string relative, string content)
        {
            string full = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, content);
            return full;
        }

        protected string ReadFile(string relative)
            => File.ReadAllText(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)));

        /// <summary>标准 launcher manifest（含权限与 application）。</summary>
        protected void WriteLauncherManifest()
        {
            WriteFile("launcher/src/main/AndroidManifest.xml",
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" package=\"com.demo.game\">\n" +
                "    <uses-permission android:name=\"android.permission.INTERNET\" />\n" +
                "    <application android:label=\"Demo\">\n" +
                "    </application>\n" +
                "</manifest>\n");
        }

        /// <summary>标准 unityLibrary manifest。</summary>
        protected void WriteUnityManifest()
        {
            WriteFile("unityLibrary/src/main/AndroidManifest.xml",
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\">\n" +
                "    <application>\n" +
                "    </application>\n" +
                "</manifest>\n");
        }

        /// <summary>标准 plist（带 DOCTYPE，模拟 Xcode 导出）。</summary>
        protected void WritePlist()
        {
            WriteFile("Info.plist",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
                "<plist version=\"1.0\">\n" +
                "<dict>\n" +
                "    <key>CFBundleIdentifier</key>\n" +
                "    <string>com.demo.game</string>\n" +
                "</dict>\n" +
                "</plist>\n");
        }
    }
}
