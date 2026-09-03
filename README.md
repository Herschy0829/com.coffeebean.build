# CoffeeBean Build

CoffeeBean 原生导出定制模块：处理 **Android Studio（Gradle）工程** 与 **iOS（Xcode）工程** 导出后的定制注入。
基于 Unity 构建回调在导出后立即执行注入，纯 C# 注入引擎（EditMode 可测），Unity API 只在薄适配层。

- 文档：`docs/design-native.md`（框架根仓库）
- 依赖：`com.coffeebean.tools`；Core 可选集成（Bridge）

## 能力一览

### Android（导出 Gradle / Android Studio 工程后）
| 注入器 | 能力 |
|--------|------|
| `CAndroidManifest` | 权限 / uses-feature / application meta-data / activity / service / application 属性；`xmlns`/`tools` 自动；幂等 |
| `CGradleFile` | 文本锚点注入：dependencies / repositories / signing 块插行 |
| `CGradleProperties` | gradle.properties / local.properties `key=value` 幂等读写 |
| `CAndroidLibs` | aar/jar 拷贝进 `unityLibrary/libs`（去重） |
| `CAndroidRes` | strings.xml 追加；proguard keep 行追加 |
| `CAndroidEnv` | SDK/NDK/JDK 环境路径检查报告（CI） |
| `CAndroidAssetPacksStep` | **Google Play 150MB AAB 分包（Play Asset Delivery）**：按配置把大资源拆成 asset packs（install-time / fast-follow / on-demand），自动生成 pack 的 build.gradle（`com.android.asset-pack`）+ manifest + `src/main/assets` 资源拷贝 + `settings.gradle include`；存在非 install-time pack 时自动注入 `com.google.android.play:core` 依赖 |

### iOS（导出 Xcode 工程后；PBX 落盘需 mac + iOS 平台）
| 注入器 | 能力 |
|--------|------|
| `CIosPlist` | Info.plist 键值注入（ATT/权限文案/SKAdNetworkItems/URL Scheme/ATS/自定义），幂等合并；自研 XML 读写 Windows 可测 |
| `CIosPlan` | system framework / 三方 framework（embed）/ 静态库 / Build Settings / Capability / 文件入 target 的声明计划 |
| Editor 适配层 | `#if UNITY_IOS` 内用 PBXProject/PlistDocument 落盘 |

## 使用

### 1. 安装
```jsonc
// Packages/manifest.json
{
  "dependencies": {
    "com.coffeebean.build": "https://github.com/Herschy0829/com.coffeebean.build.git#v0.1.1"
  }
}
```

### 2. 配置（三通道，低→高覆盖）
1. 包内默认（关闭）
2. `Assets/CoffeeBean/ExportConfig.asset`（ScriptableObject，`Window/CoffeeBean` Hub 里打开编辑，或菜单 **CoffeeBean/Build Export Config**）
3. 代码注册 `IExportStep`（SDK 集成方用）

### 3. 触发
- **Android**：勾选 *Export Project* 构建（或直接构建）→ `IPostGenerateGradleAndroidProject` 回调 → 注入 launcher/unityLibrary 工程文件
- **iOS**：mac 上切 iOS 平台构建 → `[PostProcessBuild]`（`#if UNITY_IOS`）→ plist 注入 + PBX 计划落盘

### 4. CI 批处理
```bash
Unity.exe -batchmode -quit -projectPath <proj> \
  -executeMethod CoffeeBean.ExportCli.Run \
  -exportRoot <导出工程根> -platform Android -configPath <配置.json>
# 成功读 <导出根>/CoffeeBeanExport.log
```

## 配置示例（ScriptableObject 或 JSON）
```jsonc
{
  "enable": true,
  "android": {
    "manifest": {
      "permissions": ["android.permission.INTERNET", "android.permission.ACCESS_NETWORK_STATE"],
      "applicationMetaData": { "coffeebean_channel": "googleplay" }
    },
    "gradle": {
      "files": ["unityLibrary/build.gradle"],
      "anchor": "dependencies {",
      "lines": ["implementation(name: 'mysdk', ext: 'aar')"]
    },
    "gradleProperties": { "android.useAndroidX": "true" },
    "env": { "policy": "warn" },
    "assetPacks": [
      { "name": "hd_assets", "deliveryType": "install-time", "sourceFolders": ["Assets/StreamingAssets/hd"] },
      { "name": "extra_video", "deliveryType": "on-demand", "sourceFolders": ["Assets/StreamingAssets/videos"] }
    ]
  },
  "ios": {
    "plist": {
      "strings": {
        "NSUserTrackingUsageDescription": "用于广告归因与个性化推荐",
        "NSLocationWhenInUseUsageDescription": "用于附近玩法"
      },
      "skAdNetworkIds": ["cstr6suwn9.skadnetwork"],
      "urlSchemes": ["coffeebean"]
    },
    "frameworks": {
      "system": ["AdSupport", "StoreKit"],
      "local": [ { "source": "Assets/Plugins/iOS/MySdk.framework", "embed": true } ],
      "linkerFlags": ["-ObjC"]
    },
    "buildSettings": { "ENABLE_BITCODE": "NO" },
    "capabilities": [ { "type": "PushNotifications" } ]
  }
}
```

## Sample
- `Samples~/NativeExportDemo`：最小配置 + fake 导出目录 dry-run 演练（Android 演示含 PAD asset pack）

## Google Play 150MB 分包（Play Asset Delivery，v0.1.1）
AAB 超过 Google Play 下载大小限制（150MB 档）时，把大资源拆成 asset pack 可显著缩小安装包：

1. **配置 assetPacks**（见上示例）：每个 pack 指定 `name`、`deliveryType`
   （`install-time` 随安装包一起下载；`fast-follow` 安装后可立即后台拉取；`on-demand` 按需拉取）与
   `sourceFolders`（要拆出的资源目录，内容拷进 pack 的 `src/main/assets`）
2. **导出时自动完成**（无需手改 gradle）：
   - 生成 `<pack>/build.gradle`（`com.android.asset-pack` 插件 + `packName` + `deliveryType`）
   - 生成 `<pack>/src/main/AndroidManifest.xml`（package = 应用包名 `.assetpacks.<pack>`）
   - 资源拷贝 + `settings.gradle` 追加 `include ':<pack>'`（全幂等，重复导出零重复）
   - 存在 fast-follow/on-demand pack 时自动向 `unityLibrary/build.gradle` 注入
     `com.google.android.play:core:1.10.3`（运行时下载依赖）
3. **运行时**：install-time 包直接可用；fast-follow/on-demand 用 Play Core API 请求/监听
   （`com.google.android.play:asset-delivery` 或 split install 相关 API，属业务层，不在本模块范围）
4. **游戏侧资源迁移提示**：被拆出的资源不要再放 `Resources/`（会进 base 包）；
   建议从 `StreamingAssets` 迁出后按 pack 目录组织，运行时经文件路径/AB 加载

## 测试
- EditMode（fake 导出工程 + 期望产物快照）：Manifest/gradle/properties/libs/res 注入、plist 注入与幂等、计划模型、管线（顺序/中止/dry-run/合并）、asset packs（pack 生成物/Play Core 条件注入/include 幂等/资源去重）
