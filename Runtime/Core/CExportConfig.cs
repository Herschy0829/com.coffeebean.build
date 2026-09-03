using System;
using System.Collections.Generic;

namespace CoffeeBean
{
    /// <summary>通用 key-value 配置项（JSON/ScriptableObject 序列化友好，避免 Dictionary）。</summary>
    [Serializable]
    public sealed class CExportKv
    {
        public string key;
        public string value;

        public CExportKv() { }

        public CExportKv(string key, string value)
        {
            this.key = key;
            this.value = value;
        }
    }

    /// <summary>本地（三方）framework/库条目描述。</summary>
    [Serializable]
    public sealed class CIosLocalLibConfig
    {
        /// <summary>源资产路径（Assets/... 或绝对路径，.framework/.xcframework/.a/.m/.swift/.bundle）。</summary>
        public string source;

        /// <summary>动态 framework 是否 Embed &amp; Sign。</summary>
        public bool embed;

        /// <summary>加入哪个 target：main / framework / both（默认 framework）。</summary>
        public string target = "framework";

        public CIosLocalLibConfig() { }

        public CIosLocalLibConfig(string source, bool embed = false, string target = "framework")
        {
            this.source = source;
            this.embed = embed;
            this.target = target;
        }
    }

    /// <summary>
    /// Google Play Asset Delivery（PAD）asset pack 配置：超 Google Play 大小限制（150MB 档）时，
    /// 把大资源拆成独立 asset pack（install-time / fast-follow / on-demand）。
    /// </summary>
    [Serializable]
    public sealed class CAndroidAssetPackConfig
    {
        /// <summary>pack 名（目录名 + packName；小写字母数字下划线，如 "hd_assets"）。</summary>
        public string name;

        /// <summary>投放类型：install-time / fast-follow / on-demand（默认 install-time）。</summary>
        public string deliveryType = "install-time";

        /// <summary>
        /// 源资源目录（Assets/... 相对 Unity 工程根，或绝对路径）。
        /// 每个目录的**内容**被拷贝进 pack 的 src/main/assets/ 根。
        /// </summary>
        public List<string> sourceFolders = new List<string>();

        public CAndroidAssetPackConfig() { }

        public CAndroidAssetPackConfig(string name, string deliveryType = "install-time")
        {
            this.name = name;
            this.deliveryType = deliveryType;
        }

        /// <summary>是否非 install-time（需要 Play Core 运行时下载）。</summary>
        public bool NeedsPlayCore =>
            deliveryType != null &&
            !deliveryType.Equals("install-time", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Android 段配置（Manifest / gradle / properties / libs / res / env / asset packs）。</summary>
    [Serializable]
    public sealed class CExportAndroidConfig
    {
        /// <summary>Android 段总开关。</summary>
        public bool enabled = true;

        /// <summary>Manifest 注入目标：launcher / unityLibrary / both（默认 both）。</summary>
        public string manifestTarget = "both";

        /// <summary>&lt;uses-permission android:name&gt; 列表。</summary>
        public List<string> permissions = new List<string>();

        /// <summary>&lt;uses-feature android:name&gt; 列表。</summary>
        public List<string> usesFeatures = new List<string>();

        /// <summary>&lt;application&gt; 下 &lt;meta-data&gt;（key=android:name，value=android:value）。</summary>
        public List<CExportKv> applicationMetaData = new List<CExportKv>();

        /// <summary>&lt;application&gt; 属性（key=属性名如 android:name，value=属性值）。</summary>
        public List<CExportKv> applicationAttributes = new List<CExportKv>();

        /// <summary>gradle 注入目标文件（相对导出根），如 unityLibrary/build.gradle、launcher/build.gradle。</summary>
        public List<string> gradleFiles = new List<string>();

        /// <summary>文本锚点（块起始行，trim 匹配），如 "dependencies {"。</summary>
        public string gradleAnchor = "dependencies {";

        /// <summary>锚点块内要插入的行（trim 去重）。</summary>
        public List<string> gradleLines = new List<string>();

        /// <summary>gradle.properties / local.properties 键值（key=value 幂等）。</summary>
        public List<CExportKv> gradleProperties = new List<CExportKv>();

        /// <summary>local.properties 的 sdk.dir（可选补写）。</summary>
        public string localSdkDir;

        /// <summary>libs 源文件（.aar/.jar 资产路径），拷贝进目标 libs 目录。</summary>
        public List<string> libSources = new List<string>();

        /// <summary>libs 目标目录（相对导出根），默认 unityLibrary/libs。</summary>
        public string libsTargetFolder = "unityLibrary/libs";

        /// <summary>strings.xml 追加项（key=name，value=文本）。</summary>
        public List<CExportKv> resStrings = new List<CExportKv>();

        /// <summary>proguard keep 规则行（按行去重追加）。</summary>
        public List<string> proguardLines = new List<string>();

        /// <summary>proguard 目标文件（相对导出根），默认 unityLibrary/proguard-unity.txt。</summary>
        public string proguardTargetFile = "unityLibrary/proguard-unity.txt";

        /// <summary>环境策略：warn（默认，缺 SDK/NDK/JDK 仅警告）/ abort（缺则中止）。</summary>
        public string envPolicy = "warn";

        /// <summary>Google Play Asset Delivery asset packs（超 150MB 分包）。</summary>
        public List<CAndroidAssetPackConfig> assetPacks = new List<CAndroidAssetPackConfig>();
    }

    /// <summary>iOS 段配置（plist / frameworks / build settings / capabilities）。</summary>
    [Serializable]
    public sealed class CExportIosConfig
    {
        /// <summary>iOS 段总开关。</summary>
        public bool enabled = true;

        /// <summary>plist 字符串键（key=plist key，value=字符串值），权限文案等。</summary>
        public List<CExportKv> plistStrings = new List<CExportKv>();

        /// <summary>SKAdNetworkItems 的 SKAdNetworkIdentifier 列表（按 identifier 幂等合并）。</summary>
        public List<string> skAdNetworkIds = new List<string>();

        /// <summary>CFBundleURLTypes 的 URL scheme 列表（按 scheme 幂等合并）。</summary>
        public List<string> urlSchemes = new List<string>();

        /// <summary>要追加的系统 framework 名（AdSupport/StoreKit/AppTrackingTransparency...）。</summary>
        public List<string> systemFrameworks = new List<string>();

        /// <summary>弱链接系统 framework 名。</summary>
        public List<string> weakSystemFrameworks = new List<string>();

        /// <summary>本地（三方）framework/库文件条目。</summary>
        public List<CIosLocalLibConfig> localLibs = new List<CIosLocalLibConfig>();

        /// <summary>Other Linker Flags 追加（-ObjC 等）。</summary>
        public List<string> linkerFlags = new List<string>();

        /// <summary>GCC_PREPROCESSOR_DEFINITIONS 追加宏（自动保 $(inherited)）。</summary>
        public List<string> macros = new List<string>();

        /// <summary>Build Settings 键值（覆盖式；追加类请走 macros/linkerFlags）。</summary>
        public List<CExportKv> buildSettings = new List<CExportKv>();

        /// <summary>Capability 类型名列表：PushNotifications / BackgroundModes / SignInWithApple / InAppPurchase / GameCenter。</summary>
        public List<string> capabilities = new List<string>();
    }

    /// <summary>
    /// 导出定制总配置。JsonUtility 兼容（[Serializable] + public 字段，无字典）。
    /// 三通道：包内默认 → 工程 ScriptableObject 资产 → 代码步骤改本对象。
    /// </summary>
    [Serializable]
    public sealed class CExportConfig
    {
        /// <summary>总开关（false 时管线直接跳过）。</summary>
        public bool enable = true;

        public CExportAndroidConfig android = new CExportAndroidConfig();

        public CExportIosConfig ios = new CExportIosConfig();

        /// <summary>仅对指定平台生效（空=全部；值：Android / IOS）。</summary>
        public List<string> exportEnabledFor = new List<string>();

        public bool PlatformEnabled(CExportPlatform platform)
        {
            if (!enable) return false;
            if (exportEnabledFor.Count == 0) return true;
            string want = platform == CExportPlatform.Android ? "Android" : "IOS";
            foreach (var p in exportEnabledFor)
                if (string.Equals(p, want, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
