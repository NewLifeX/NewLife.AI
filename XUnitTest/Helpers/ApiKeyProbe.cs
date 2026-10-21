using System;
using System.IO;

namespace XUnitTest.Helpers;

/// <summary>集成测试密钥探测。供 RequiresApiKeyFact / RequiresApiKeyTheory 在发现阶段决定是否跳过</summary>
/// <remarks>
/// 集成测试在发现阶段就要决定跑还是跳过：本仓使用的 xunit 2.x + VSTest 适配器不识别运行时
/// 动态跳过（通过抛出 SkipException 跳过），异常会被记为失败，因此“密钥不可用就跳过”只能落在特性上。
/// </remarks>
public static class ApiKeyProbe
{
    /// <summary>集成测试开关环境变量。设为 0/false/off/no 时整批跳过，不访问网络</summary>
    public const String EnableVariable = "NEWLIFE_AI_INTEGRATION";

    /// <summary>判断集成测试是否启用。未设置或设为其它值时启用</summary>
    /// <returns>启用返回 true</returns>
    public static Boolean IsIntegrationEnabled()
    {
        var value = Environment.GetEnvironmentVariable(EnableVariable);
        if (String.IsNullOrWhiteSpace(value)) return true;

        var flag = value.Trim().ToLowerInvariant();
        return flag is not ("0" or "false" or "off" or "no");
    }

    /// <summary>判断密钥是否可用（文件或环境变量存在且非空）</summary>
    /// <param name="environmentVariable">主环境变量</param>
    /// <param name="filePath">可选的密钥文件路径，可为相对路径</param>
    /// <param name="fallbackEnvironmentVariables">备用环境变量</param>
    /// <returns>存在可用密钥返回 true</returns>
    public static Boolean HasKey(String environmentVariable, String? filePath, params String[]? fallbackEnvironmentVariables)
    {
        if (!String.IsNullOrEmpty(filePath) && TryReadFileKey(filePath!)) return true;
        if (TryReadEnv(environmentVariable)) return true;

        if (fallbackEnvironmentVariables != null)
        {
            foreach (var env in fallbackEnvironmentVariables)
            {
                if (TryReadEnv(env)) return true;
            }
        }

        return false;
    }

    /// <summary>构造跳过原因</summary>
    /// <param name="environmentVariable">主环境变量</param>
    /// <param name="filePath">可选的密钥文件路径</param>
    /// <returns>跳过原因文本</returns>
    public static String BuildSkipMessage(String environmentVariable, String? filePath)
    {
        var source = String.IsNullOrEmpty(filePath)
            ? environmentVariable
            : $"{filePath} 或 {environmentVariable}";
        return $"未检测到可用 API Key（{source}），跳过集成测试";
    }

    private static Boolean TryReadEnv(String? name)
    {
        if (String.IsNullOrWhiteSpace(name)) return false;
        var value = Environment.GetEnvironmentVariable(name);
        return !String.IsNullOrWhiteSpace(value);
    }

    private static Boolean TryReadFileKey(String filePath)
    {
        var path = filePath;
        if (!Path.IsPathRooted(path))
            path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, filePath));

        if (!File.Exists(path))
        {
            // 尝试当前目录
            var cwdPath = Path.GetFullPath(filePath, Environment.CurrentDirectory);
            if (!File.Exists(cwdPath)) return false;
            path = cwdPath;
        }

        var key = File.ReadAllText(path).Trim();
        return !String.IsNullOrWhiteSpace(key);
    }
}
