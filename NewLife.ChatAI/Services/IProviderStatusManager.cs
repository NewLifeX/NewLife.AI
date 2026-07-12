namespace NewLife.ChatAI.Services;

/// <summary>提供商状态管理器接口。用于跟踪 provider 调用错误、屏蔽不可用提供商，实现主备切换</summary>
public interface IProviderStatusManager
{
    /// <summary>检查提供商是否可用。被屏蔽且未到期时返回 false；已到期自动恢复</summary>
    /// <param name="providerConfigId">提供商配置编号</param>
    /// <returns>true 表示可用</returns>
    Boolean IsAvailable(Int32 providerConfigId);

    /// <summary>记录一次调用失败。达到错误阈值后自动标记为屏蔽</summary>
    /// <param name="providerConfigId">提供商配置编号</param>
    void RecordFailure(Int32 providerConfigId);
}
