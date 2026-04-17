namespace NewLife.ChatData.Entity;

/// <summary>模型配置接口扩展。承载导航属性与有效参数方法，供抽象层（IChatPipeline 等）访问提供商信息而无需依赖具体实体</summary>
/// <remarks>
/// 通过 partial interface 分文件扩展 <see cref="IModelConfig"/>，
/// 避免主接口被自动生成的代码覆盖时丢失手工成员。
/// </remarks>
public partial interface IModelConfig
{
    /// <summary>获取有效接口地址。从关联的提供商配置中获取</summary>
    /// <returns>API 接口地址，不存在时返回空字符串</returns>
    String GetEffectiveEndpoint();

    /// <summary>获取有效密钥。从关联的提供商配置中获取</summary>
    /// <returns>API 密钥，不存在时返回空字符串</returns>
    String GetEffectiveApiKey();

    /// <summary>获取有效的提供商代码。从关联的提供商配置中获取</summary>
    /// <returns>IAiProvider 实现类完整名，不存在时返回空字符串</returns>
    String GetEffectiveProvider();

    /// <summary>获取有效的 API 协议。从关联的提供商配置中获取</summary>
    /// <returns>API 协议标识，不存在时返回空字符串</returns>
    String GetEffectiveApiProtocol();
}
