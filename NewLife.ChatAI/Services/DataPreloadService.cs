using NewLife.AI.Tools;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Services;
using NewLife.Log;

namespace NewLife.ChatAI.Services;

/// <summary>数据预热服务。启动时执行内置工具元数据同步、模型发现等预热任务</summary>
/// <remarks>实例化数据预热服务</remarks>
/// <param name="registry">工具注册表，包含所有已注册工具类型</param>
/// <param name="modelService">模型服务，用于启动时同步一次模型列表</param>
public class DataPreloadService(ToolRegistry registry, ModelService modelService) : IHostedService
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

    /// <summary>执行预热任务。依次同步内置工具元数据、触发一次模型发现</summary>
    private async Task Preload()
    {
        // 同步内置工具元数据
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

        // 启动时同步一次模型列表；后续如需更新，请在 Web 管理界面手动触发
        try
        {
            await modelService.DoDiscoverAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }

    #endregion
}