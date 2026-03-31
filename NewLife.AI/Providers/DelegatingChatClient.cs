using NewLife.AI.Models;

namespace NewLife.AI.Providers;

/// <summary>委托式 AI 对话客户端基类。实现 IChatClient 的可组合中间件管道模式</summary>
/// <remarks>
/// 参考 MEAI 的 DelegatingChatClient 设计，所有方法默认转发给内层客户端。
/// 子类只需 override 关注的方法，即可实现日志、用量统计、重试、限流等横切关注点，
/// 而无需重新实现完整协议逻辑。
/// </remarks>
public abstract class DelegatingChatClient : IChatClient
{
    #region 属性

    /// <summary>内层客户端。所有方法的默认转发目标</summary>
    protected IChatClient InnerClient { get; }

    #endregion

    #region 构造

    /// <summary>初始化委托式客户端</summary>
    /// <param name="innerClient">内层客户端，不可为 null</param>
    protected DelegatingChatClient(IChatClient innerClient)
    {
        if (innerClient == null) throw new ArgumentNullException(nameof(innerClient));
        InnerClient = innerClient;
    }

    #endregion

    #region 方法

    /// <summary>非流式对话完成（默认转发给内层客户端）</summary>
    /// <param name="request">内部对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>完整的对话响应</returns>
    public virtual Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
        => InnerClient.GetResponseAsync(request, cancellationToken);

    /// <summary>流式对话完成（默认转发给内层客户端）</summary>
    /// <param name="request">内部对话请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>流式响应块的异步枚举</returns>
    public virtual IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
        => InnerClient.GetStreamingResponseAsync(request, cancellationToken);

    #endregion

    #region 释放

    /// <summary>释放托管资源</summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected virtual void Dispose(Boolean disposing)
    {
        if (disposing) InnerClient?.Dispose();
    }

    /// <summary>释放资源</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
