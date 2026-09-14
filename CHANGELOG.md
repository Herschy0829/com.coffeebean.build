# Changelog

## [0.1.2] - 2026-09-14

### Fixed
- **iOS PBX 落盘路径无法编译（CS1501）**：`ExportBuildCallbacks` 调用 `CiOSXcodeAdapter.Apply` 时漏传
  `projectRoot`（3 实参 vs 4 形参）。该调用位于 `#if UNITY_IOS` 内，在 Windows / 非 iOS 编辑器下不参与编译，
  因此长期未被发现 —— 意味着 iOS 侧的 framework / 本地库 / BuildSettings / Capability 落盘在此前**从未在
  iOS 目标下编译通过**。现补传 `session.ProjectRoot`（`ResolveSource` 正是用它把 `Assets/...` 解析为绝对路径）。
- **`CAndroidManifestStep` 复用 `any` 导致零改动的 manifest 被重写**：`bool any` 同时充当“本文件是否有改动”
  与“本次是否注入过”两种语义，第一份 manifest 有改动后，后续 manifest 即使零改动也会执行 `Save()`，
  产生无谓的格式重排 diff，削弱了模块承诺的幂等性。改为按文件独立判定 `changed`，再用 `any |= changed`
  汇总。

### Added
- 回归测试 `CAndroidManifestTests.Step_BothManifests_SavesOnlyTheChangedFile`：同一轮注入中
  第一份 manifest 需改动、第二份零改动，断言后者逐字节保持原样。已验证该测试在修复前失败、修复后通过。

## [0.1.1] - 2026-09-03

### Added
- **Google Play 150MB AAB 分包（Play Asset Delivery asset packs）**：`CExportAndroidConfig.assetPacks` 配置
  （name / deliveryType：install-time|fast-follow|on-demand / sourceFolders）
- **`CAndroidAssetPacksStep`**（导出后处理，幂等 + dry-run）：为每个 pack 生成
  `<pack>/build.gradle`（`com.android.asset-pack` 插件）、`<pack>/src/main/AndroidManifest.xml`
  （package = 应用包名 `.assetpacks.<pack>`）、源目录内容拷贝进 `src/main/assets`；
  `settings.gradle` 追加 `include ':<pack>'`（去重）；存在非 install-time pack 时自动向
  `unityLibrary/build.gradle` 注入 `com.google.android.play:core:1.10.3`
- EditMode 测试 5 个（pack 生成物期望断言 / install-time 不加 Play Core / on-demand 加 Play Core 且幂等 /
  资源递归拷贝去重 / settings include 幂等 / dry-run 零产物）；Sample dry-run 演示附 asset pack

## [0.1.0] - 2026-09-03

### Added
- **导出后处理管线**：`CExportRunner` 按序执行注入步骤（Android 链 → iOS 链），支持 dry-run / 异常中止 / 幂等表 / 会话日志
- **Android 注入器（纯 C#，EditMode 可测）**：
  - `CAndroidManifest`：权限 / uses-feature / application meta-data / activity / service / application 属性注入，`xmlns:android`/`tools` 自动，全部幂等（先查后插）
  - `CGradleFile`：文本锚点注入（dependencies/repositories/signing 块插行，已存在跳过）
  - `CGradleProperties`：`key=value` 幂等读写（gradle.properties / local.properties）
  - `CAndroidLibs`：aar/jar 拷贝进 `libs/`（文件名+大小去重）
  - `CAndroidRes`：strings.xml `<string>` 追加 + proguard keep 行去重追加
  - `CAndroidEnv`：SDK/NDK/JDK 环境路径检查报告（CI 用）
- **iOS 注入器（纯 C# 计划层 + 自研 plist XML 读写，Windows 可测；PBX 落盘在 Editor `#if UNITY_IOS` 适配层）**：
  - `CIosPlist`：自研 plist XML 读写（string/bool/int/array/dict，幂等合并：SKAdNetworkItems 按 identifier 去重、URL scheme 去重）
  - `CIosPlan`：system framework / 三方 framework / 静态库 / Build Settings / Capability / 文件入 target 的声明计划（由 Editor PBXProject 适配层落盘）
- **Unity 适配层（Editor）**：Android `IPostGenerateGradleAndroidProject`（UnityEditor.CoreModule，导出 Gradle 工程触发）+ iOS `[PostProcessBuild]` + `#if UNITY_IOS`（PBXProject/PlistDocument）；`CExportConfigAsset` ScriptableObject 配置；Hub 工具窗口（dry-run / 打开配置）；`CoffeeBean.ExportCli` 批处理入口
- **NativeExportDemo 示例** + EditMode 测试（fake 导出工程 + 期望产物快照，幂等 / 中止 / dry-run / 配置合并）

### Notes
- 依赖 `com.coffeebean.tools`；Core 可选集成（Bridge）
- 与 EDM4U 互补：第三方 gradle/pods 依赖解析走 EDM4U，本模块专注工程文件内容定制
- 详见 `docs/design-native.md`
