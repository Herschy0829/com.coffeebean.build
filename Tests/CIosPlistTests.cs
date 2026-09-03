using System.IO;
using NUnit.Framework;

namespace CoffeeBean.Build.Tests
{
    /// <summary>CIosPlist 注入测试：DOCTYPE 加载 / string 策略 / 数组幂等。</summary>
    public class CIosPlistTests : FakeExportTestBase
    {
        [Test]
        public void Load_PlistWithDoctype_Works()
        {
            WritePlist();
            var plist = CIosPlist.Load(Path.Combine(Root, "Info.plist"));
            Assert.NotNull(plist);
        }

        [Test]
        public void SetString_OverwriteByDefault_SkipWhenPolicy()
        {
            WritePlist();
            string path = Path.Combine(Root, "Info.plist");
            var p = CIosPlist.Load(path);
            Assert.True(p.SetString("NSUserTrackingUsageDescription", "用于广告"));
            p.Save();

            var p2 = CIosPlist.Load(path);
            Assert.False(p2.SetString("NSUserTrackingUsageDescription", "覆盖", "skip"));
            Assert.True(p2.SetString("NSUserTrackingUsageDescription", "覆盖", "overwrite"));
            p2.Save();

            string text = File.ReadAllText(path);
            StringAssert.Contains("<string>覆盖</string>", text);
            StringAssert.DoesNotContain("用于广告", text);
        }

        [Test]
        public void SkAdNetworkIds_AddsOnce_Idempotent()
        {
            WritePlist();
            string path = Path.Combine(Root, "Info.plist");
            var p = CIosPlist.Load(path);
            Assert.AreEqual(2, p.EnsureSkAdNetworkIds(new[] { "abc.skadnetwork", "def.skadnetwork" }));
            p.Save();

            var p2 = CIosPlist.Load(path);
            Assert.AreEqual(0, p2.EnsureSkAdNetworkIds(new[] { "abc.skadnetwork" })); // 已存在
            p2.Save();

            string text = File.ReadAllText(path);
            int count = 0, i = 0;
            while ((i = text.IndexOf("abc.skadnetwork", i)) >= 0) { count++; i++; }
            Assert.AreEqual(1, count);
        }

        [Test]
        public void UrlSchemes_AddsOnce_Idempotent()
        {
            WritePlist();
            string path = Path.Combine(Root, "Info.plist");
            var p = CIosPlist.Load(path);
            Assert.AreEqual(1, p.EnsureUrlSchemes(new[] { "coffeebean" }));
            p.Save();

            var p2 = CIosPlist.Load(path);
            Assert.AreEqual(0, p2.EnsureUrlSchemes(new[] { "coffeebean" }));
            p2.Save();
            StringAssert.Contains("CFBundleURLSchemes", File.ReadAllText(path));
        }

        [Test]
        public void Save_RoundTrip_ValidXml()
        {
            WritePlist();
            string path = Path.Combine(Root, "Info.plist");
            var p = CIosPlist.Load(path);
            p.SetString("NSLocationWhenInUseUsageDescription", "定位");
            p.EnsureSkAdNetworkIds(new[] { "x.skadnetwork" });
            p.Save();

            // 再次加载不抛 = XML 合法
            Assert.DoesNotThrow(() => CIosPlist.Load(path));
            StringAssert.Contains("SKAdNetworkItems", File.ReadAllText(path));
        }
    }
}
