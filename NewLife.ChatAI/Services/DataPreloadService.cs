using NewLife.AI.Tools;
using NewLife.ChatData.Entity;
using NewLife.Log;

namespace NewLife.ChatAI.Services;

/// <summary>数据预热服务。启动时执行内置工具元数据同步等预热任务</summary>
/// <remarks>实例化数据预热服务</remarks>
/// <param name="registry">工具注册表，包含所有已注册工具类型</param>
public class DataPreloadService(ToolRegistry registry) : IHostedService
{
    #region IHostedService

    /// <summary>启动时执行数据预热</summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(() => Preload(), cancellationToken);

        return Task.CompletedTask;
    }

    /// <summary>停止时无需特殊处理</summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    #endregion

    #region 预热

    /// <summary>执行预热任务</summary>
    private void Preload()
    {
        try
        {
            var count = registry.SyncNativeTools<NativeTool>(NativeTool.FindByName, static e => e.Save(), XTrace.WriteException);
            if (count > 0)
                XTrace.WriteLine("内置工具同步完成，处理 {0} 个工具", count);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }

    #endregion
}