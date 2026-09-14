using System.IO;
using NUnit.Framework;

namespace CoffeeBean.Build.Tests
{
    /// <summary>CAndroidManifest 注入测试：注入 + 幂等 + 期望产物。</summary>
    public class CAndroidManifestTests : FakeExportTestBase
    {
        [Test]
        public void EnsurePermission_AddsOnce_AndIsIdempotent()
        {
            WriteLauncherManifest();
            string path = Path.Combine(Root, "launcher", "src", "main", "AndroidManifest.xml");

            var m1 = CAndroidManifest.Load(path);
            Assert.True(m1.EnsurePermission("android.permission.ACCESS_NETWORK_STATE"));
            Assert.False(m1.EnsurePermission("android.permission.INTERNET")); // 已存在
            m1.Save();

            // 再次加载执行 → 不重复
            var m2 = CAndroidManifest.Load(path);
            Assert.False(m2.EnsurePermission("android.permission.ACCESS_NETWORK_STATE"));
            m2.Save();

            string text = File.ReadAllText(path);
            int count = 0, i = 0;
            while ((i = text.IndexOf("android.permission.ACCESS_NETWORK_STATE", i)) >= 0) { count++; i++; }
            Assert.AreEqual(1, count);
        }

        [Test]
        public void EnsureMetaData_Adds_AndOverwritesSameKey()
        {
            WriteLauncherManifest();
            string path = Path.Combine(Root, "launcher", "src", "main", "AndroidManifest.xml");

            var m = CAndroidManifest.Load(path);
            Assert.True(m.EnsureApplicationMetaData("coffeebean_channel", "googleplay"));
            m.Save();

            var m2 = CAndroidManifest.Load(path);
            Assert.True(m2.EnsureApplicationMetaData("coffeebean_channel", "huawei")); // 覆盖值
            m2.Save();

            string text = File.ReadAllText(path);
            StringAssert.Contains("android:name=\"coffeebean_channel\"", text);
            StringAssert.Contains("android:value=\"huawei\"", text);
            StringAssert.DoesNotContain("android:value=\"googleplay\"", text);
        }

        [Test]
        public void SetApplicationAttribute_SetsName()
        {
            WriteUnityManifest();
            string path = Path.Combine(Root, "unityLibrary", "src", "main", "AndroidManifest.xml");
            var m = CAndroidManifest.Load(path);
            m.SetApplicationAttribute("android:name", "com.demo.CustomApp");
            m.Save();
            StringAssert.Contains("android:name=\"com.demo.CustomApp\"", File.ReadAllText(path));
        }

        [Test]
        public void Step_BothManifests_SavesOnlyTheChangedFile()
        {
            // launcher 缺 ACCESS_NETWORK_STATE → 本次会被注入并保存
            WriteLauncherManifest();

            // unityLibrary 已含该权限，且刻意用 2 空格缩进 + CRLF：一旦被 Save() 就会被
            // 规范化成 4 空格 + LF，从而暴露"无改动却被重写"
            const string unityRaw =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\">\r\n" +
                "  <uses-permission android:name=\"android.permission.ACCESS_NETWORK_STATE\" />\r\n" +
                "  <application>\r\n" +
                "  </application>\r\n" +
                "</manifest>\r\n";
            WriteFile("unityLibrary/src/main/AndroidManifest.xml", unityRaw);

            var cfg = new CExportConfig { enable = true };
            cfg.android.manifestTarget = "both";
            cfg.android.permissions.Add("android.permission.ACCESS_NETWORK_STATE");

            var session = new CExportSession(CExportPlatform.Android, Root, cfg);
            new CAndroidManifestStep().Execute(session);

            // 第一份被正确注入
            StringAssert.Contains("android.permission.ACCESS_NETWORK_STATE",
                ReadFile("launcher/src/main/AndroidManifest.xml"));

            // 第二份零改动 → 必须逐字节保持原样
            // 回归点：早期实现把 any 同时当作"本文件是否有改动"，导致第一份改动后
            // 第二份也被 Save()，产生无谓的格式重排 diff（削弱幂等承诺）。
            Assert.AreEqual(unityRaw, ReadFile("unityLibrary/src/main/AndroidManifest.xml"),
                "零改动的 manifest 不应被重写");
        }

        [Test]
        public void ExpectedArtifact_ManifestInjections()
        {
            WriteLauncherManifest();
            string path = Path.Combine(Root, "launcher", "src", "main", "AndroidManifest.xml");
            var m = CAndroidManifest.Load(path);
            m.EnsurePermission("android.permission.ACCESS_NETWORK_STATE");
            m.EnsureApplicationMetaData("coffeebean_channel", "googleplay");
            m.Save();

            string text = File.ReadAllText(path);
            // 期望产物：属性级断言（兼容 Mono/.NET XmlWriter 自闭合空格差异）；结构合法
            StringAssert.Contains("android:name=\"android.permission.ACCESS_NETWORK_STATE\"", text);
            StringAssert.Contains("android:name=\"coffeebean_channel\"", text);
            StringAssert.Contains("android:value=\"googleplay\"", text);
            // application 仍闭合，整体结构完整
            StringAssert.Contains("</application>", text);
            StringAssert.Contains("</manifest>", text);
        }
    }
}
