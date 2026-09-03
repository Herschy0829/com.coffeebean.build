#if COFFEEBEAN_CORE
using CoffeeBean;

[assembly: CoffeeBeanModule(
    "com.coffeebean.build",
    "0.1.0",
    DisplayName = "Build",
    Description = "Native export customization: Android Studio (Gradle) & iOS (Xcode) project post-process injection (manifest/plist/gradle/frameworks).",
    Dependencies = new[] { "com.coffeebean.core", "com.coffeebean.tools" }
)]

namespace CoffeeBean
{
    /// <summary>
    /// Core 集成：Build 为构建期（Editor）工具模块，无运行时生命周期资源；
    /// 本模块标记使 build 可被 Core 发现与版本检查。
    /// </summary>
    public sealed class BuildModule : ICoffeeBeanModule
    {
        public void OnLoad(CoffeeBeanContext context)
        {
            context.Log("CoffeeBean.Build integrated (export post-process; Editor-only).");
        }

        public void OnStart()
        {
        }

        public void OnShutdown()
        {
        }
    }
}
#endif
