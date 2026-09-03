using System.IO;
using NUnit.Framework;

namespace CoffeeBean.Build.Tests
{
    /// <summary>CGradleFile 锚点注入测试。</summary>
    public class CGradleFileTests : FakeExportTestBase
    {
        [Test]
        public void Inject_AddsLinesUnderAnchor_AndSkipsExisting()
        {
            string path = WriteFile("unityLibrary/build.gradle",
                "apply plugin: 'com.android.library'\n\ndependencies {\n    implementation fileTree(dir: 'libs', include: ['*.jar'])\n}\n");

            int n1 = CGradleFile.Inject(path, "dependencies {", new[] { "implementation(name: 'demo-sdk', ext: 'aar')" });
            Assert.AreEqual(1, n1);

            // 再执行：同批跳过
            int n2 = CGradleFile.Inject(path, "dependencies {", new[] { "implementation(name: 'demo-sdk', ext: 'aar')" });
            Assert.AreEqual(0, n2);

            string text = File.ReadAllText(path);
            int count = 0, i = 0;
            while ((i = text.IndexOf("demo-sdk", i)) >= 0) { count++; i++; }
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Inject_MissingAnchor_ThrowsExportException()
        {
            string path = WriteFile("x.gradle", "apply plugin: 'x'\n");
            Assert.Throws<CExportException>(() => CGradleFile.Inject(path, "dependencies {", new[] { "a" }));
        }

        [Test]
        public void Inject_PreservesExistingContent()
        {
            string path = WriteFile("b.gradle", "apply plugin: 'com.android.library'\n\ndependencies {\n    implementation fileTree(dir: 'libs', include: ['*.jar'])\n}\n");
            CGradleFile.Inject(path, "dependencies {", new[] { "implementation 'com.demo:x:1.0'" });
            string text = File.ReadAllText(path);
            StringAssert.Contains("implementation fileTree(dir: 'libs', include: ['*.jar'])", text);
            StringAssert.Contains("implementation 'com.demo:x:1.0'", text);
        }
    }
}
