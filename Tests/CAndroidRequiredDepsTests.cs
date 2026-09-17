using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace CoffeeBean.Build.Tests
{
    /// <summary>
    /// 框架级必需 Gradle 依赖落盘测试（build 模块作为首选执行者）。
    /// 覆盖：注入、幂等、缺文件时不抛只告警、日志记录、空清单不动作。
    /// </summary>
    public class CAndroidRequiredDepsTests : FakeExportTestBase
    {
        private const string GradleRel = "unityLibrary/build.gradle";

        private static readonly string[] StandardGradle =
        {
            "apply plugin: 'com.android.library'",
            "",
            "dependencies {",
            "    implementation fileTree(dir: 'libs', include: ['*.jar'])",
            "}",
            ""
        };

        private void WriteStandardGradle()
            => WriteFile(GradleRel, string.Join("\n", StandardGradle));

        private static CAndroidGradleRequirement Req(string artifact, string reason = "测试")
            => new CAndroidGradleRequirement(artifact, reason);

        // ========== 注入 ==========

        [Test]
        public void Inject_WritesImplementationLine()
        {
            WriteStandardGradle();
            var log = new CExportLog();

            var reqs = new List<CAndroidGradleRequirement> { Req(CAndroidGradleRequirements.PlayInAppReviewArtifact) };
            int inserted = CAndroidRequiredDeps.Inject(Root, reqs, log);

            Assert.AreEqual(1, inserted);
            StringAssert.Contains("implementation 'com.google.android.play:review:2.0.2'", ReadFile(GradleRel));
            StringAssert.Contains("fileTree", ReadFile(GradleRel)); // 原内容保留
        }

        [Test]
        public void Inject_IsIdempotent()
        {
            WriteStandardGradle();
            var reqs = new List<CAndroidGradleRequirement> { Req("com.a:b:1") };

            int first = CAndroidRequiredDeps.Inject(Root, reqs, new CExportLog());
            int second = CAndroidRequiredDeps.Inject(Root, reqs, new CExportLog());

            Assert.AreEqual(1, first);
            Assert.AreEqual(0, second, "重复注入应为 0 行改动");
        }

        [Test]
        public void Inject_MultipleRequirements_AllWritten()
        {
            WriteStandardGradle();
            var reqs = new List<CAndroidGradleRequirement>
            {
                Req("com.a:b:1"),
                Req("com.c:d:2"),
            };

            int inserted = CAndroidRequiredDeps.Inject(Root, reqs, new CExportLog());

            Assert.AreEqual(2, inserted);
            string gradle = ReadFile(GradleRel);
            StringAssert.Contains("com.a:b:1", gradle);
            StringAssert.Contains("com.c:d:2", gradle);
        }

        [Test]
        public void Inject_IgnoresNullAndEmptyEntries()
        {
            WriteStandardGradle();
            var reqs = new List<CAndroidGradleRequirement>
            {
                null,
                new CAndroidGradleRequirement(null, "空"),
                new CAndroidGradleRequirement("", "空"),
                new CAndroidGradleRequirement("   ", "空白"), // 空白串必须挡掉，否则会写出 implementation ''
                Req("com.a:b:1"),
            };

            int inserted = CAndroidRequiredDeps.Inject(Root, reqs, new CExportLog());

            Assert.AreEqual(1, inserted);
            StringAssert.DoesNotContain("implementation ''", ReadFile(GradleRel));
        }

        // ========== 边界：不抛、只告警 ==========

        [Test]
        public void Inject_MissingGradleFile_WarnsAndReturnsZero()
        {
            var log = new CExportLog();
            int inserted = CAndroidRequiredDeps.Inject(Root, new List<CAndroidGradleRequirement> { Req("com.a:b:1") }, log);

            Assert.AreEqual(0, inserted);
            Assert.IsFalse(log.HasErrors, "缺文件不应算错误（依赖留待运行时暴露）");
            StringAssert.Contains("Warn", log.ToText());
            StringAssert.Contains(GradleRel, log.ToText());
        }

        [Test]
        public void Inject_EmptyRequirements_ReturnsZeroWithoutTouchingFile()
        {
            WriteStandardGradle();
            string before = ReadFile(GradleRel);

            int inserted = CAndroidRequiredDeps.Inject(Root, new List<CAndroidGradleRequirement>(), new CExportLog());

            Assert.AreEqual(0, inserted);
            Assert.AreEqual(before, ReadFile(GradleRel));
        }

        [Test]
        public void Inject_NullArguments_ReturnsZero()
        {
            Assert.AreEqual(0, CAndroidRequiredDeps.Inject(null, new List<CAndroidGradleRequirement> { Req("com.a:b:1") }));
            Assert.AreEqual(0, CAndroidRequiredDeps.Inject(Root, null));
        }

        [Test]
        public void Inject_LogRecordsWhatAndWhy()
        {
            WriteStandardGradle();
            var log = new CExportLog();
            CAndroidRequiredDeps.Inject(Root,
                new List<CAndroidGradleRequirement> { Req("com.google.android.play:review:2.0.2", "应用内评价") }, log);

            string text = log.ToText();
            StringAssert.Contains(CAndroidRequiredDeps.StepId, text);
            // 日志要写清「为什么需要」，否则出问题难排查
            StringAssert.Contains("应用内评价", text);
        }

        // ========== 常量契约 ==========

        [Test]
        public void Constants_AreThePathsUnityGenerates()
        {
            Assert.AreEqual("unityLibrary/build.gradle", CAndroidRequiredDeps.GradleRelativePath);
            Assert.AreEqual("dependencies {", CAndroidRequiredDeps.Anchor);
        }

        [Test]
        public void Inject_LeavesUnrelatedFilesAlone()
        {
            WriteStandardGradle();
            WriteFile("settings.gradle", "include ':launcher'\n");

            CAndroidRequiredDeps.Inject(Root, new List<CAndroidGradleRequirement> { Req("com.a:b:1") }, new CExportLog());

            Assert.AreEqual("include ':launcher'\n", ReadFile("settings.gradle"));
        }
    }
}
