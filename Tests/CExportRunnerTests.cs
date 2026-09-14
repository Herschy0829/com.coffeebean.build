using System.IO;
using NUnit.Framework;

namespace CoffeeBean.Build.Tests
{
    /// <summary>CExportRunner 管线测试：全链路落盘 / dry-run 不改盘 / 配置开关 / iOS plist+plan。</summary>
    public class CExportRunnerTests : FakeExportTestBase
    {
        static CExportConfig AndroidConfig()
        {
            var cfg = new CExportConfig { enable = true };
            cfg.android.permissions.Add("android.permission.ACCESS_NETWORK_STATE");
            cfg.android.applicationMetaData.Add(new CExportKv("coffeebean_channel", "googleplay"));
            cfg.android.gradleFiles.Add("unityLibrary/build.gradle");
            cfg.android.gradleLines.Add("implementation(name: 'demo-sdk', ext: 'aar')");
            cfg.android.gradleProperties.Add(new CExportKv("android.useAndroidX", "true"));
            cfg.android.envPolicy = "warn";
            return cfg;
        }

        [Test]
        public void Android_Run_AppliesAllInjections()
        {
            WriteLauncherManifest();
            WriteUnityManifest();
            WriteFile("unityLibrary/build.gradle",
                "apply plugin: 'com.android.library'\n\ndependencies {\n    implementation fileTree(dir: 'libs', include: ['*.jar'])\n}\n");
            WriteFile("gradle.properties", "org.gradle.jvmargs=-Xmx1024m\n");

            var session = new CExportSession(CExportPlatform.Android, Root, AndroidConfig());
            Assert.True(CExportRunner.Run(session));

            string manifest = ReadFile("launcher/src/main/AndroidManifest.xml");
            StringAssert.Contains("android.permission.ACCESS_NETWORK_STATE", manifest);
            StringAssert.Contains("coffeebean_channel", manifest);

            string gradle = ReadFile("unityLibrary/build.gradle");
            StringAssert.Contains("demo-sdk", gradle);

            string props = ReadFile("gradle.properties");
            StringAssert.Contains("android.useAndroidX=true", props);
        }

        [Test]
        public void Android_Run_IsIdempotent_SecondRunNoChange()
        {
            WriteLauncherManifest();
            WriteUnityManifest();
            WriteFile("unityLibrary/build.gradle", "apply plugin: 'x'\ndependencies {\n}\n");
            WriteFile("gradle.properties", "");

            var s1 = new CExportSession(CExportPlatform.Android, Root, AndroidConfig());
            CExportRunner.Run(s1);
            string after1 = ReadFile("launcher/src/main/AndroidManifest.xml");

            var s2 = new CExportSession(CExportPlatform.Android, Root, AndroidConfig());
            CExportRunner.Run(s2);
            Assert.AreEqual(after1, ReadFile("launcher/src/main/AndroidManifest.xml"));
        }

        [Test]
        public void DryRun_DoesNotModifyFiles()
        {
            WriteLauncherManifest();
            WriteUnityManifest();
            WriteFile("unityLibrary/build.gradle", "dependencies {\n}\n");
            WriteFile("gradle.properties", "");

            string before = ReadFile("launcher/src/main/AndroidManifest.xml");
            var session = new CExportSession(CExportPlatform.Android, Root, AndroidConfig()) { DryRun = true };
            CExportRunner.Run(session);
            Assert.AreEqual(before, ReadFile("launcher/src/main/AndroidManifest.xml"));
        }

        [Test]
        public void ConfigDisabled_ReturnsFalse_NoChanges()
        {
            WriteLauncherManifest();
            WriteUnityManifest();
            var cfg = AndroidConfig();
            cfg.enable = false;
            var session = new CExportSession(CExportPlatform.Android, Root, cfg);
            Assert.False(CExportRunner.Run(session));
            StringAssert.DoesNotContain("ACCESS_NETWORK_STATE", ReadFile("launcher/src/main/AndroidManifest.xml"));
        }

        [Test]
        public void Ios_Run_AppliesPlistAndFillsPlan()
        {
            WritePlist();
            var cfg = new CExportConfig { enable = true };
            cfg.ios.plistStrings.Add(new CExportKv("NSUserTrackingUsageDescription", "广告归因"));
            cfg.ios.skAdNetworkIds.Add("abc.skadnetwork");
            cfg.ios.systemFrameworks.Add("AdSupport");
            cfg.ios.linkerFlags.Add("-ObjC");
            cfg.ios.capabilities.Add("PushNotifications");

            var session = new CExportSession(CExportPlatform.IOS, Root, cfg);
            Assert.True(CExportRunner.Run(session));
            StringAssert.Contains("NSUserTrackingUsageDescription", ReadFile("Info.plist"));
            Assert.AreEqual(1, session.IosPlan.SystemFrameworks.Count);
            Assert.AreEqual(1, session.IosPlan.Capabilities.Count);
            StringAssert.Contains("-ObjC", session.IosPlan.LinkerFlags[0]);
        }

        [Test]
        public void Run_LogsAppliedInjectionCount_AndMarksFullIdempotenceOnRerun()
        {
            WriteLauncherManifest();
            WriteUnityManifest();
            WriteFile("unityLibrary/build.gradle", "apply plugin: 'x'\ndependencies {\n}\n");
            WriteFile("gradle.properties", "");

            var s1 = new CExportSession(CExportPlatform.Android, Root, AndroidConfig());
            CExportRunner.Run(s1);
            string first = s1.Log.ToText();
            StringAssert.Contains("应用注入", first, "完成日志应给出本次应用的注入项数");
            StringAssert.DoesNotContain("完全幂等", first, "首次运行有改动，不应标记为完全幂等");

            // 第二次运行：所有注入都已存在 → 零改动，日志应直接标出完全幂等
            var s2 = new CExportSession(CExportPlatform.Android, Root, AndroidConfig());
            CExportRunner.Run(s2);
            string second = s2.Log.ToText();
            StringAssert.Contains("应用注入 0 项", second, "幂等重跑应报告 0 项注入");
            StringAssert.Contains("完全幂等", second, "幂等重跑应标记为完全幂等");
        }

        [Test]
        public void CustomStep_RunsBeforeBuiltins_CanMutateConfig()
        {
            WriteLauncherManifest();
            WriteUnityManifest();
            var cfg = new CExportConfig { enable = true };
            var custom = new AddPermissionStep();
            var session = new CExportSession(CExportPlatform.Android, Root, cfg);
            CExportRunner.Run(session, new IExportStep[] { custom });
            StringAssert.Contains("android.permission.VIBRATE", ReadFile("launcher/src/main/AndroidManifest.xml"));
        }

        sealed class AddPermissionStep : IExportStep
        {
            public string Id => "test.add-permission";
            public int Order => 100; // 落盘步骤(10000)之前 → 改 Config 生效
            public bool IsActive(CExportSession s) => s.Platform == CExportPlatform.Android;
            public void Execute(CExportSession s) => s.Config.android.permissions.Add("android.permission.VIBRATE");
        }
    }
}
