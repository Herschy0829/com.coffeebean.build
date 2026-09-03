using System;
using System.IO;
using NUnit.Framework;

namespace CoffeeBean.Build.Tests
{
    /// <summary>CGradleProperties / CAndroidRes / CAndroidEnv 测试。</summary>
    public class CAndroidPropertiesResEnvTests : FakeExportTestBase
    {
        [Test]
        public void Properties_EnsureKeyValue_AddsThenUpdates()
        {
            string path = WriteFile("gradle.properties", "org.gradle.jvmargs=-Xmx1024m\n");
            Assert.True(CGradleProperties.EnsureKeyValue(path, "android.useAndroidX", "true"));
            Assert.False(CGradleProperties.EnsureKeyValue(path, "android.useAndroidX", "true")); // 幂等
            Assert.True(CGradleProperties.EnsureKeyValue(path, "android.useAndroidX", "false")); // 更新
            Assert.True(CGradleProperties.EnsureKeyValue(path, "new.key", "v"));
            string text = File.ReadAllText(path);
            StringAssert.Contains("android.useAndroidX=false", text);
            StringAssert.Contains("org.gradle.jvmargs=-Xmx1024m", text); // 注释/原行保留
            StringAssert.Contains("new.key=v", text);
        }

        [Test]
        public void Properties_UpdatesInPlace_NoDuplicateKeys()
        {
            string path = WriteFile("gradle.properties", "android.useAndroidX=true\n");
            CGradleProperties.EnsureKeyValue(path, "android.useAndroidX", "true");
            string text = File.ReadAllText(path);
            int count = 0, i = 0;
            while ((i = text.IndexOf("android.useAndroidX", i)) >= 0) { count++; i++; }
            Assert.AreEqual(1, count);
        }

        [Test]
        public void ResStrings_AddsAndOverwrites()
        {
            string path = WriteFile("unityLibrary/src/main/res/values/strings.xml",
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<resources>\n</resources>\n");
            Assert.AreEqual(1, CAndroidRes.AddStrings(path, new[] { new CExportKv("demo_string", "Hello") }));
            Assert.AreEqual(1, CAndroidRes.AddStrings(path, new[] { new CExportKv("demo_string", "World") })); // 覆盖
            Assert.AreEqual(0, CAndroidRes.AddStrings(path, new[] { new CExportKv("demo_string", "World") })); // 幂等
            string text = File.ReadAllText(path);
            StringAssert.Contains("<string name=\"demo_string\">World</string>", text);
            int count = 0, i = 0;
            while ((i = text.IndexOf("demo_string", i)) >= 0) { count++; i++; }
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Proguard_AddsLines_Deduplicated()
        {
            string path = WriteFile("unityLibrary/proguard-unity.txt", "-keep class a.** { *; }\n");
            Assert.AreEqual(1, CAndroidRes.AddProguardLines(path, new[] { "-keep class b.** { *; }" }));
            Assert.AreEqual(0, CAndroidRes.AddProguardLines(path, new[] { "-keep class b.** { *; }" }));
            string text = File.ReadAllText(path);
            StringAssert.Contains("-keep class a.** { *; }", text);
            StringAssert.Contains("-keep class b.** { *; }", text);
        }

        [Test]
        public void Libs_CopyOnce_DuplicateSkipped()
        {
            string srcDir = Path.Combine(Path.GetTempPath(), "CBExportLibSrc_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(srcDir);
            string srcAar = Path.Combine(srcDir, "demo.aar");
            File.WriteAllBytes(srcAar, new byte[] { 1, 2, 3, 4 });
            try
            {
                string target = Path.Combine(Root, "unityLibrary", "libs");
                var report = new System.Collections.Generic.List<string>();
                Assert.AreEqual(1, CAndroidLibs.CopyInto(new[] { srcAar }, null, target, report));
                Assert.AreEqual(0, CAndroidLibs.CopyInto(new[] { srcAar }, null, target, report)); // 同名同长跳过
                Assert.AreEqual(1, Directory.GetFiles(target).Length);
            }
            finally
            {
                Directory.Delete(srcDir, true);
            }
        }

        [Test]
        public void Env_CheckWarn_DoesNotThrow()
        {
            // warn 策略：无论环境如何都不抛（Collect 也不应抛）
            Assert.DoesNotThrow(() => CAndroidEnv.Check("warn"));
        }
    }
}
