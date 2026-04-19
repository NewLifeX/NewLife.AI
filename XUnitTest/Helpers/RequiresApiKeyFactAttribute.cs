using System;
using System.IO;
using Xunit;

namespace XUnitTest.Helpers;

/// <summary>需要 API Key 的集成测试特性。未检测到有效密钥时自动 Skip，避免因环境未配置导致失败</summary>
/// <remarks>
/// 检测顺序：
/// 1) 显式文件路径（可相对路径）
/// 2) 主环境变量
/// 3) 备用环境变量
/// </remarks>
public sealed class RequiresApiKeyFactAttribute : FactAttribute
{
    /// <summary>创建 RequiresApiKeyFact 特性</summary>
    /// <param name="environmentVariable">主环境变量，如 OPENAI_API_KEY</param>
    /// <param name="filePath">可选密钥文件路径，如 config/OpenAI.key（纯文本）</param>
    /// <param name="fallbackEnvironmentVariables">可选备用环境变量</param>
    public RequiresApiKeyFactAttribute(String environmentVariable, String? filePath = null, params String[] fallbackEnvironmentVariables)
    {
        if (HasKey(environmentVariable, filePath, fallbackEnvironmentVariables)) return;

        var source = String.IsNullOrEmpty(filePath)
            ? environmentVariable
            : $"{filePath} 或 {environmentVariable}";
        Skip = $"未检测到可用 API Key（{source}），跳过集成测试";
    }

    private static Boolean HasKey(String environmentVariable, String? filePath, String[] fallbackEnvironmentVariables)
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
