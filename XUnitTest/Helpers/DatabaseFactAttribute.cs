using System;
using System.Runtime.InteropServices;
using Xunit;

namespace XUnitTest.Helpers;

/// <summary>数据库测试特性。arm64 桌面平台缺少可用的 SQLite 驱动时在发现阶段跳过</summary>
/// <remarks>
/// Windows/macOS arm64 上 NewLife.XCode 的 SQLite 工厂无法完成初始化，
/// 直接访问实体时抛出 InvalidOperationException；同一批用例已在 x64/linux 覆盖。
/// </remarks>
public sealed class DatabaseFactAttribute : FactAttribute
{
    /// <summary>创建 DatabaseFact 特性</summary>
    public DatabaseFactAttribute()
    {
        if (RuntimeInformation.ProcessArchitecture is Architecture.Arm64 or Architecture.Arm &&
            (OperatingSystem.IsMacOS() || OperatingSystem.IsWindows()))
        {
            Skip = "arm64 桌面平台无法加载 SQLite 驱动，跳过数据库测试";
        }
    }
}
