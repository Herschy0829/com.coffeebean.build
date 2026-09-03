# Changelog

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
