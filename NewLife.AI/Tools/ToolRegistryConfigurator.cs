namespace NewLife.AI.Tools;

/// <summary>ToolRegistry 配置器。封装延迟执行的工具注册动作，支持 DI 聚合模式</summary>
/// <remarks>
/// 配合 ASP.NET Core DI 的 <c>IEnumerable&lt;T&gt;</c> 聚合机制使用：
/// <code>
/// // 内部注册内置工具
/// services.AddSingleton(new ToolRegistryConfigurator((sp, r) => r.AddTools(new MyTool())));
/// // 外部追加自定义工具
/// services.AddSingleton(new ToolRegistryConfigurator((sp, r) => r.AddTools(new CustomTool())));
/// // 工厂统一执行
/// services.TryAddSingleton(sp => { ... GetServices&lt;ToolRegistryConfigurator&gt;() ... });
/// </code>
/// </remarks>
/// <remarks>创建配置器实例</remarks>
/// <param name="configure">配置动作，接收服务提供者和 ToolRegistry 实例</param>
public class ToolRegistryConfigurator(Action<IServiceProvider, ToolRegistry> configure)
{
    private readonly Action<IServiceProvider, ToolRegistry> _configure = configure ?? throw new ArgumentNullException(nameof(configure));

    /// <summary>执行配置动作，将工具注册到 ToolRegistry</summary>
    /// <param name="serviceProvider">DI 服务提供者</param>
    /// <param name="registry">目标 ToolRegistry 实例</param>
    public void Configure(IServiceProvider serviceProvider, ToolRegistry registry) =>
        _configure(serviceProvider, registry);
}
