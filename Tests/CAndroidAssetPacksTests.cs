using System;
using System.IO;
using NUnit.Framework;

namespace CoffeeBean.Build.Tests
{
    /// <summary>Google Play Asset Delivery asset packs 步骤测试。</summary>
    public class CAndroidAssetPacksTests : FakeExportTestBase
    {
        string _srcDir;

        [SetUp]
        public void PackSetUp()
        {
            _srcDir = Path.Combine(Path.GetTempPath(), "CBAPackSrc_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_srcDir);
            // 源资源：两个文件 + 一个子目录
            File.WriteAllBytes(Path.Combine(_srcDir, "a.bin"), new byte[] { 1, 2, 3 });
            File.WriteAllBytes(Path.Combine(_srcDir, "b.bin"), new byte[] { 4, 5 });
            Directory.CreateDirectory(Path.Combine(_srcDir, "sub"));
            File.WriteAllBytes(Path.Combine(_srcDir, "sub", "c.bin"), new byte[] { 6 });
        }

        [TearDown]
        public void PackTearDown()
        {
            try { if (_srcDir != null && Directory.Exists(_srcDir)) Directory.Delete(_srcDir, true); } catch { }
        }

        void WriteAndroidProject()
        {
            WriteLauncherManifest(); // package=com.demo.game
            WriteUnityManifest();
            WriteFile("unityLibrary/build.gradle",
                "apply plugin: 'com.android.library'\n\ndependencies {\n    implementation fileTree(dir: 'libs', include: ['*.jar'])\n}\n");
            WriteFile("settings.gradle", "include ':launcher'\ninclude ':unityLibrary'\n");
        }

        static CExportConfig PackConfig(string deliveryType)
        {
            var cfg = new CExportConfig { enable = true };
            cfg.android.assetPacks.Add(new CAndroidAssetPackConfig("hd_assets", deliveryType));
            return cfg;
        }

        [Test]
        public void InstallTimePack_GeneratesPackStructure_NoPlayCore()
        {
            WriteAndroidProject();
            var session = new CExportSession(CExportPlatform.Android, Root, PackConfig("install-time")) { ProjectRoot = Root };
            CExportRunner.Run(session);

            // build.gradle
            string packGradle = ReadFile("hd_assets/build.gradle");
            StringAssert.Contains("com.android.asset-pack", packGradle);
            StringAssert.Contains("packName = 'hd_assets'", packGradle);
            StringAssert.Contains("deliveryType = 'install-time'", packGradle);

            // manifest package 用 launcher base package 拼
            string packManifest = ReadFile("hd_assets/src/main/AndroidManifest.xml");
            StringAssert.Contains("package=\"com.demo.game.assetpacks.hd_assets\"", packManifest);

            // settings include
            StringAssert.Contains("include ':hd_assets'", ReadFile("settings.gradle"));

            // install-time：不加 Play Core
            StringAssert.DoesNotContain("play:core", ReadFile("unityLibrary/build.gradle"));
        }

        [Test]
        public void OnDemandPack_AddsPlayCore_Idempotent()
        {
            WriteAndroidProject();
            var cfg = PackConfig("on-demand");
            cfg.android.assetPacks[0].sourceFolders.Add(_srcDir);

            var s1 = new CExportSession(CExportPlatform.Android, Root, cfg) { ProjectRoot = Root };
            CExportRunner.Run(s1);
            string gradle = ReadFile("unityLibrary/build.gradle");
            StringAssert.Contains("com.google.android.play:core:1.10.3", gradle);

            // 第二次执行：不重复
            var s2 = new CExportSession(CExportPlatform.Android, Root, cfg) { ProjectRoot = Root };
            CExportRunner.Run(s2);
            int count = 0, i = 0;
            string text = ReadFile("unityLibrary/build.gradle");
            while ((i = text.IndexOf("play:core", i)) >= 0) { count++; i++; }
            Assert.AreEqual(1, count);
        }

        [Test]
        public void ResourceFolders_CopiedOnce_Recursive()
        {
            WriteAndroidProject();
            var cfg = PackConfig("install-time");
            cfg.android.assetPacks[0].sourceFolders.Add(_srcDir);

            var s1 = new CExportSession(CExportPlatform.Android, Root, cfg) { ProjectRoot = Root };
            CExportRunner.Run(s1);
            Assert.True(File.Exists(Path.Combine(Root, "hd_assets", "src", "main", "assets", "a.bin")));
            Assert.True(File.Exists(Path.Combine(Root, "hd_assets", "src", "main", "assets", "sub", "c.bin")));

            // 再跑：文件级幂等，无变化
            var s2 = new CExportSession(CExportPlatform.Android, Root, cfg) { ProjectRoot = Root };
            CExportRunner.Run(s2);
            Assert.AreEqual(3, Directory.GetFiles(Path.Combine(Root, "hd_assets", "src", "main", "assets"), "*", SearchOption.AllDirectories).Length);
        }

        [Test]
        public void SettingsInclude_Idempotent()
        {
            WriteAndroidProject();
            var s1 = new CExportSession(CExportPlatform.Android, Root, PackConfig("install-time")) { ProjectRoot = Root };
            CExportRunner.Run(s1);
            var s2 = new CExportSession(CExportPlatform.Android, Root, PackConfig("install-time")) { ProjectRoot = Root };
            CExportRunner.Run(s2);

            string settings = ReadFile("settings.gradle");
            int count = 0, i = 0;
            while ((i = settings.IndexOf("include ':hd_assets'", i)) >= 0) { count++; i++; }
            Assert.AreEqual(1, count);
        }

        [Test]
        public void DryRun_GeneratesNothing()
        {
            WriteAndroidProject();
            var cfg = PackConfig("install-time");
            cfg.android.assetPacks[0].sourceFolders.Add(_srcDir);
            var session = new CExportSession(CExportPlatform.Android, Root, cfg) { ProjectRoot = Root, DryRun = true };
            CExportRunner.Run(session);

            Assert.False(Directory.Exists(Path.Combine(Root, "hd_assets")));
            StringAssert.DoesNotContain("hd_assets", ReadFile("settings.gradle"));
        }
    }
}
