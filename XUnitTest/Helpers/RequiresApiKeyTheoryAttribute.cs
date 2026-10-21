using System;
using Xunit;

namespace XUnitTest.Helpers;

/// <summary>需要 API Key 的集成测试 Theory 特性。未启用集成测试或未检测到密钥时在发现阶段 Skip</summary>
/// <remarks>与 <see cref="RequiresApiKeyFactAttribute"/> 判定规则一致，仅适用于参数化测试。</remarks>
public sealed class RequiresApiKeyTheoryAttribute : TheoryAttribute
{
    /// <summary>创建 RequiresApiKeyTheory 特性</summary>
    /// <param name="environmentVariable">主环境变量，如 DASHSCOPE_API_KEY</param>
    /// <param name="filePath">可选密钥文件路径，如 config/DashScope.key（纯文本）</param>
    /// <param name="fallbackEnvironmentVariables">可选备用环境变量</param>
    public RequiresApiKeyTheoryAttribute(String environmentVariable, String? filePath = null, params String[] fallbackEnvironmentVariables)
    {
        if (!ApiKeyProbe.IsIntegrationEnabled())
        {
            Skip = $"已通过环境变量 {ApiKeyProbe.EnableVariable} 关闭集成测试";
            return;
        }

        if (ApiKeyProbe.HasKey(environmentVariable, filePath, fallbackEnvironmentVariables)) return;

        Skip = ApiKeyProbe.BuildSkipMessage(environmentVariable, filePath);
    }
}
