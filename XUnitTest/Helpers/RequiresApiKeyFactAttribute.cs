using System;
using Xunit;

namespace XUnitTest.Helpers;

/// <summary>需要 API Key 的集成测试特性。未启用集成测试或未检测到密钥时在发现阶段 Skip</summary>
/// <remarks>
/// 检测顺序：
/// 0) 环境变量 NEWLIFE_AI_INTEGRATION 设为 0/false/off/no 时直接跳过（不访问网络）
/// 1) 显式文件路径（可相对路径）
/// 2) 主环境变量
/// 3) 备用环境变量
/// 开关的存在是因为“配置了密钥”不等于“密钥可用”：密钥过期、被封或额度耗尽时服务商返回 401/403，
/// 这类失败与代码回归无关，需要一条不联网即可关闭整批集成测试的通道。
/// </remarks>
public sealed class RequiresApiKeyFactAttribute : FactAttribute
{
    /// <summary>创建 RequiresApiKeyFact 特性</summary>
    /// <param name="environmentVariable">主环境变量，如 DASHSCOPE_API_KEY</param>
    /// <param name="filePath">可选密钥文件路径，如 config/DashScope.key（纯文本）</param>
    /// <param name="fallbackEnvironmentVariables">可选备用环境变量</param>
    public RequiresApiKeyFactAttribute(String environmentVariable, String? filePath = null, params String[] fallbackEnvironmentVariables)
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
