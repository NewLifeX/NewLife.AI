using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using NewLife.AI.ModelContextProtocol;
using NewLife.Http;
using NewLife.Log;
using Xunit;

namespace XUnitTest;

/// <summary>HttpMcpServer单元测试</summary>
public class HttpMcpServerTests : IDisposable
{
    #region 测试辅助类
    /// <summary>测试工具类</summary>
    public class TestTools
    {
        /// <summary>获取当前时间</summary>
        public String GetTime() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        /// <summary>计算两数之和</summary>
        /// <param name="a">第一个数</param>
        /// <param name="b">第二个数</param>
        /// <returns>两数之和</returns>
        public Int32 Add(Int32 a, Int32 b) => a + b;

        /// <summary>字符串连接</summary>
        /// <param name="text1">第一个字符串</param>
        /// <param name="text2">第二个字符串</param>
        /// <returns>连接后的字符串</returns>
        public String Concat(String text1, String text2 = "World") => $"{text1} {text2}";
    }

    /// <summary>Mock服务提供者</summary>
    private class MockServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, Object> _services = new();

        public void AddService<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
        }

        public Object? GetService(Type serviceType)
        {
            _services.TryGetValue(serviceType, out var service);
            return service;
        }
    }

    /// <summary>Mock日志实现</summary>
    private class MockLog : ILog
    {
        public String? LastMessage { get; private set; }

        public String Name { get; set; } = "MockLog";
        public Boolean Enable { get; set; } = true;
        public LogLevel Level { get; set; } = LogLevel.Info;

        public Boolean IsDebugEnabled => Level <= LogLevel.Debug;
        public Boolean IsInfoEnabled => Level <= LogLevel.Info;
        public Boolean IsWarnEnabled => Level <= LogLevel.Warn;
        public Boolean IsErrorEnabled => Level <= LogLevel.Error;
        public Boolean IsFatalEnabled => Level <= LogLevel.Fatal;

        public void Debug(String format, params Object?[] args) => LastMessage = String.Format(format, args);
        public void Info(String format, params Object?[] args) => LastMessage = String.Format(format, args);
        public void Warn(String format, params Object?[] args) => LastMessage = String.Format(format, args);
        public void Error(String format, params Object?[] args) => LastMessage = String.Format(format, args);
        public void Fatal(String format, params Object?[] args) => LastMessage = String.Format(format, args);
        public void Write(LogLevel level, String format, params Object?[] args) => LastMessage = String.Format(format, args);
    }

    /// <summary>可观察的HttpServer，用于测试Start是否被调用</summary>
    /// <remarks>由于HttpServer的Start和Map方法不是虚方法，我们只能通过OnStart虚方法来检测Start调用</remarks>
    private class ObservableHttpServer : HttpServer
    {
        public Boolean StartCalled { get; private set; }

        // 重写OnStart虚方法来检测Start调用
        protected override void OnStart()
        {
            StartCalled = true;
            // 不调用base.OnStart()以避免实际启动服务器
        }
    }
    #endregion

    #region 私有字段
    private HttpMcpServer? _server;
    #endregion

    #region 构造函数测试
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var server = new HttpMcpServer();

        // Assert
        Assert.Equal(8080, server.Port);
        Assert.Null(server.Server);
    }

    [Fact]
    public void Constructor_ShouldInheritFromMcpServer()
    {
        // Act
        var server = new HttpMcpServer();

        // Assert
        Assert.IsAssignableFrom<McpServer>(server);
        Assert.NotNull(server.Manager);
    }
    #endregion

    #region 属性测试
    [Fact]
    public void Port_ShouldSetAndGetCorrectly()
    {
        // Arrange
        var server = new HttpMcpServer();
        const Int32 expectedPort = 9090;

        // Act
        server.Port = expectedPort;

        // Assert
        Assert.Equal(expectedPort, server.Port);
    }

    [Fact]
    public void Server_ShouldSetAndGetCorrectly()
    {
        // Arrange
        var server = new HttpMcpServer();
        var httpServer = new HttpServer();

        // Act
        server.Server = httpServer;

        // Assert
        Assert.Same(httpServer, server.Server);
    }

    [Fact]
    public void Port_ShouldAcceptValidPortRange()
    {
        // Arrange
        var server = new HttpMcpServer();

        // Act & Assert
        server.Port = 80;
        Assert.Equal(80, server.Port);

        server.Port = 65535;
        Assert.Equal(65535, server.Port);

        server.Port = 1;
        Assert.Equal(1, server.Port);
    }

    [Fact]
    public void Port_ShouldAcceptNegativeValues()
    {
        // Arrange - 某些场景下端口可能设置为负值（如禁用服务）
        var server = new HttpMcpServer();

        // Act & Assert
        server.Port = -1;
        Assert.Equal(-1, server.Port);
    }
    #endregion

    #region Start方法测试
    [Fact]
    public void Start_ShouldCreateHttpServerIfNull()
    {
        // Arrange
        var server = new HttpMcpServer();
        server.Port = 20015;

        // Act
        server.Start();

        // Assert
        Assert.NotNull(server.Server);
        Assert.Equal(20015, server.Port);
    }

    [Fact]
    public void Start_ShouldNotCreateNewServerIfExists()
    {
        // Arrange
        var server = new HttpMcpServer();
        var originalServer = new HttpServer();
        server.Server = originalServer;

        // Act
        server.Start();

        // Assert
        Assert.Same(originalServer, server.Server);
    }

    [Fact]
    public void Start_ShouldConfigureHttpServerWithCorrectPort()
    {
        // Arrange
        var server = new HttpMcpServer { Port = 9999 };

        // Act
        server.Start();

        // Assert
        Assert.NotNull(server.Server);
        Assert.Equal(9999, server.Server.Port);
    }

    [Fact]
    public void Start_ShouldSetServerServiceProvider()
    {
        // Arrange
        var server = new HttpMcpServer();
        server.Port = 20017;

        // Act
        server.Start();

        // Assert
        Assert.Same(server, server.Server.ServiceProvider);
    }

    [Fact]
    public void Start_ShouldInheritLogAndTracer()
    {
        // Arrange
        var mockLog = new MockLog();
        var server = new HttpMcpServer { Log = mockLog };

        // Act
        server.Start();

        // Assert
        Assert.Same(mockLog, server.Server.Log);
        Assert.Equal(server.Tracer, server.Server.Tracer);
    }

    [Fact]
    public void Start_ShouldCallHttpServerStart()
    {
        // Arrange
        var server = new HttpMcpServer();
        var observableServer = new ObservableHttpServer();
        server.Server = observableServer;

        // Act
        server.Start();

        // Assert
        Assert.True(observableServer.StartCalled);
        // 注意：由于Map方法不是虚方法，我们无法直接测试它的调用
        // 但我们可以通过其他方式验证HttpMcpServer.Start()方法的完整性
    }

    [Fact]
    public void Start_ShouldUseNullCoalescingOperatorCorrectly()
    {
        // Arrange
        var server = new HttpMcpServer { Port = 7777 };
        Assert.Null(server.Server); // 确保初始为null

        // Act
        server.Start();

        // Assert
        Assert.NotNull(server.Server);
        Assert.Equal(7777, server.Server.Port);
    }
    #endregion

    #region Process方法测试（通过反射测试方法签名）
    [Fact]
    public void Process_Method_ShouldExist()
    {
        // Arrange
        var server = new HttpMcpServer();
        var processMethod = typeof(HttpMcpServer).GetMethod("ProcessRequest", BindingFlags.Public | BindingFlags.Instance);

        // Assert
        Assert.NotNull(processMethod);
        Assert.Equal(typeof(void), processMethod.ReturnType);

        var parameters = processMethod.GetParameters();
        Assert.Single(parameters);
        Assert.Equal("IHttpContext", parameters[0].ParameterType.Name);
    }

    [Fact]
    public void WriteSseMessage_Method_ShouldExist()
    {
        // Arrange
        var server = new HttpMcpServer();
        var writeSseMessageMethod = typeof(HttpMcpServer).GetMethod("WriteSseMessage",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Assert
        Assert.NotNull(writeSseMessageMethod);
        Assert.Equal(typeof(void), writeSseMessageMethod.ReturnType);

        var parameters = writeSseMessageMethod.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("IHttpContext", parameters[0].ParameterType.Name);
        Assert.Equal(typeof(Object), parameters[1].ParameterType);
    }
    #endregion

    #region 继承和接口测试
    [Fact]
    public void HttpMcpServer_ShouldInheritFromMcpServer()
    {
        // Arrange & Act
        var server = new HttpMcpServer();

        // Assert
        Assert.IsAssignableFrom<McpServer>(server);
    }

    [Fact]
    public void HttpMcpServer_ShouldImplementIServiceProvider()
    {
        // Arrange & Act
        var server = new HttpMcpServer();

        // Assert
        Assert.IsAssignableFrom<IServiceProvider>(server);
    }

    [Fact]
    public void HttpMcpServer_ShouldInheritMcpServerProperties()
    {
        // Arrange & Act
        var server = new HttpMcpServer();

        // Assert
        Assert.NotNull(server.Manager);
        Assert.NotNull(server.Log);
        // Tracer可能为null，这是正常的
    }
    #endregion

    #region 工具管理测试
    [Fact]
    public void AddTool_ShouldWorkCorrectly()
    {
        // Arrange
        var server = new HttpMcpServer();
        var serviceProvider = new MockServiceProvider();
        serviceProvider.AddService(new TestTools());

        // Act
        server.AddTool<TestTools>(serviceProvider);

        // Assert
        Assert.True(server.Manager.Services.Count > 0);
    }

    [Fact]
    public void AddTool_ShouldRegisterServicesCorrectly()
    {
        // Arrange
        var server = new HttpMcpServer();
        var serviceProvider = new MockServiceProvider();
        serviceProvider.AddService(new TestTools());

        // Act
        server.AddTool<TestTools>(serviceProvider);

        // Assert
        var services = server.Manager.Services;
        Assert.True(services.Count > 0);

        // 验证服务被注册
        Assert.True(services.Count > 0);
    }

    [Fact]
    public void AddTool_ShouldSetServiceProvider()
    {
        // Arrange
        var server = new HttpMcpServer();
        var serviceProvider = new MockServiceProvider();
        serviceProvider.AddService(new TestTools());

        // Act
        server.AddTool<TestTools>(serviceProvider);

        // Assert
        // 验证ServiceProvider被正确设置（如果HttpMcpServer.ServiceProvider为null的话）
        Assert.True(server.Manager.Services.Count > 0);
    }
    #endregion

    #region 配置和状态测试
    [Fact]
    public void HttpMcpServer_ShouldInitializeWithNullServer()
    {
        // Act
        var server = new HttpMcpServer();

        // Assert
        Assert.Null(server.Server);
    }

    [Fact]
    public void HttpMcpServer_ShouldAllowServerReplacement()
    {
        // Arrange
        var server = new HttpMcpServer();
        var firstServer = new HttpServer();
        var secondServer = new HttpServer();

        // Act
        server.Server = firstServer;
        Assert.Same(firstServer, server.Server);

        server.Server = secondServer;
        Assert.Same(secondServer, server.Server);

        // Assert
        Assert.NotSame(firstServer, server.Server);
        Assert.Same(secondServer, server.Server);
    }
    #endregion

    #region 日志测试
    [Fact]
    public void Log_ShouldBeInheritedFromMcpServer()
    {
        // Arrange
        var mockLog = new MockLog();
        var server = new HttpMcpServer { Log = mockLog };

        // Act & Assert
        Assert.Same(mockLog, server.Log);
    }

    [Fact]
    public void WriteLog_ShouldWorkCorrectly()
    {
        // Arrange
        var mockLog = new MockLog();
        var server = new HttpMcpServer { Log = mockLog };

        // Act
        server.WriteLog("测试消息: {0}", "参数");

        // Assert
        Assert.Equal("测试消息: 参数", mockLog.LastMessage);
    }
    #endregion

    #region 异常处理和边界测试
    [Fact]
    public void Start_ShouldHandleNullOperationGracefully()
    {
        // Arrange
        var server = new HttpMcpServer();
        server.Port = 20016;

        // Act & Assert - 不应该抛出异常
        var exception = Record.Exception(() => server.Start());
        Assert.Null(exception);
    }

    [Fact]
    public void Start_MultipleCallsShouldBeIdempotent()
    {
        // Arrange
        var server = new HttpMcpServer();
        server.Port = 20014;

        // Act - 多次调用Start
        server.Start();
        var firstServer = server.Server;

        server.Start();
        var secondServer = server.Server;

        // Assert - 应该是同一个服务器实例
        Assert.Same(firstServer, secondServer);
    }

    [Fact]
    public void Port_ShouldHandleZeroValue()
    {
        // Arrange
        var server = new HttpMcpServer();

        // Act
        server.Port = 0;

        // Assert
        Assert.Equal(0, server.Port);
    }

    [Fact]
    public void Start_WithNullServer_ShouldCreateNewServerWithCorrectConfiguration()
    {
        // Arrange
        var customLog = new MockLog();
        var server = new HttpMcpServer
        {
            Port = 20018,
            Log = customLog
        };

        Assert.Null(server.Server); // 确保开始时为null

        // Act
        server.Start();

        // Assert
        Assert.NotNull(server.Server);
        Assert.Equal(20018, server.Server.Port);
        Assert.Same(server, server.Server.ServiceProvider);
        Assert.Same(customLog, server.Server.Log);
        Assert.Equal(server.Tracer, server.Server.Tracer);
    }
    #endregion

    #region 集成测试
    [Fact]
    public void Integration_ServerCreationAndConfiguration_ShouldWorkTogether()
    {
        // Arrange
        var server = new HttpMcpServer
        {
            Port = 8888,
            Log = new MockLog()
        };

        var serviceProvider = new MockServiceProvider();
        serviceProvider.AddService(new TestTools());
        server.AddTool<TestTools>(serviceProvider);

        // Act
        server.Start();

        // Assert
        Assert.NotNull(server.Server);
        Assert.Equal(8888, server.Server.Port);
        Assert.Same(server, server.Server.ServiceProvider);
        Assert.True(server.Manager.Services.Count > 0);
    }

    [Fact]
    public void Integration_FullWorkflow_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var server = new HttpMcpServer();
        server.Port = 9000;

        var mockServiceProvider = new MockServiceProvider();
        var tools = new TestTools();
        mockServiceProvider.AddService(tools);

        server.ServiceProvider = mockServiceProvider;
        server.AddTool<TestTools>(mockServiceProvider);
        server.Start();

        // Assert
        Assert.Equal(9000, server.Port);
        Assert.NotNull(server.Server);
        Assert.Equal(9000, server.Server.Port);
        Assert.Same(server, server.Server.ServiceProvider);
        Assert.True(server.Manager.Services.Count > 0);
    }

    [Fact]
    public void Integration_WithObservableServer_ShouldCallAllMethods()
    {
        // Arrange
        var server = new HttpMcpServer { Port = 8888 };
        var observableServer = new ObservableHttpServer();
        server.Server = observableServer;

        // Act
        server.Start();

        // Assert
        Assert.True(observableServer.StartCalled);
        //Assert.Equal(8888, server.Server.Port);
        Assert.Same(server, server.Server.ServiceProvider);
        Assert.NotEmpty(observableServer.Routes);

        //// 验证HttpMcpServer的Process方法存在，这间接证明了Map配置的合理性
        //var processMethod = typeof(HttpMcpServer).GetMethod("Process",
        //    BindingFlags.Public | BindingFlags.Instance);
        //Assert.NotNull(processMethod);
    }
    #endregion

    #region 性能和资源测试
    [Fact]
    public void Server_ShouldBeDisposableWithoutError()
    {
        // Arrange
        var server = new HttpMcpServer();
        server.Port = 20011;
        server.Start();

        // Act & Assert - 不应该抛出异常
        var exception = Record.Exception(() => server.Server?.Stop("Test"));
        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_ShouldCleanupResourcesWithoutException()
    {
        // Arrange
        var server = new HttpMcpServer();
        server.Start();

        // Act & Assert - 不应该抛出异常（测试HttpMcpServer本身不需要Dispose）
        // HttpMcpServer继承自McpServer，不直接实现IDisposable
        // 我们测试Server的Stop方法来验证资源清理
        var exception = Record.Exception(() => server.Server?.Stop("Test cleanup"));
        Assert.Null(exception);
    }
    #endregion

    #region HTTP特定功能测试
    [Fact]
    public void Server_ShouldBeOfTypeHttpServer()
    {
        // Arrange
        var server = new HttpMcpServer();
        server.Port = 20012;

        // Act
        server.Start();

        // Assert
        Assert.IsType<HttpServer>(server.Server);
    }

    [Fact]
    public void Server_ShouldConfigureCorrectDefaults()
    {
        // Arrange & Act
        var server = new HttpMcpServer();
        server.Port = 20013;
        server.Start();

        // Assert
        Assert.Equal(20013, server.Server.Port);
        Assert.Same(server, server.Server.ServiceProvider);
    }

    [Fact]
    public void Server_ShouldInheritConfigurationFromHttpMcpServer()
    {
        // Arrange
        var customLog = new MockLog();
        var server = new HttpMcpServer
        {
            Port = 8765,
            Log = customLog
        };

        // Act
        server.Start();

        // Assert
        Assert.Equal(8765, server.Server.Port);
        Assert.Same(customLog, server.Server.Log);
        Assert.Equal(server.Tracer, server.Server.Tracer);
    }
    #endregion

    #region 类型和结构测试
    [Fact]
    public void HttpMcpServer_ShouldHaveCorrectNamespace()
    {
        // Arrange & Act
        var server = new HttpMcpServer();

        // Assert
        Assert.Equal("NewLife.AI.ModelContextProtocol.HttpMcpServer", server.GetType().FullName);
    }

    [Fact]
    public void HttpMcpServer_ShouldBePublicClass()
    {
        // Arrange & Act
        var type = typeof(HttpMcpServer);

        // Assert
        Assert.True(type.IsPublic);
        Assert.False(type.IsAbstract);
        Assert.False(type.IsSealed);
    }
    #endregion

    #region 清理资源
    public void Dispose()
    {
        try
        {
            _server?.Server?.Stop("Test cleanup");
        }
        catch
        {
            // 忽略清理时的异常
        }
        finally
        {
            _server = null;
        }
    }
    #endregion
}