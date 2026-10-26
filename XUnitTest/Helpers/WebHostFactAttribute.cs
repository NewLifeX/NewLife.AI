using System;
using System.Runtime.InteropServices;
using Xunit;

namespace XUnitTest.Helpers;

/// <summary>Web 宿主集成测试特性。arm64 桌面平台缺少可用的 SQLite 驱动时在发现阶段跳过</summary>
/// <remarks>
/// Windows/macOS arm64 上 NewLife.XCode 的 SQLite 工厂无法完成初始化，
/// WebApplicationFactory 启动宿主时在 EntityFactory.InitAll 处抛出 InvalidOperationException；
/// 同一批用例已在 win-x64/linux-x64/linux-arm64/osx-x64 覆盖，这里跳过以免平台驱动问题阻断 CI。
/// </remarks>
public sealed class WebHostFactAttribute : FactAttribute
{
    /// <summary>创建 WebHostFact 特性</summary>
    public WebHostFactAttribute()
    {
        if (RuntimeInformation.ProcessArchitecture is Architecture.Arm64 or Architecture.Arm &&
            (OperatingSystem.IsMacOS() || OperatingSystem.IsWindows()))
        {
            Skip = "arm64 桌面平台无法加载 SQLite 驱动，跳过 Web 宿主集成测试";
        }
    }
}
