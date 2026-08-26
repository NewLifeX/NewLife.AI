using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using NewLife.AI.ModelContextProtocol;
using NewLife.Serialization;
using Xunit;

namespace XUnitTest.Mcp;

/// <summary>McpToolManager 工具注册与 schema 生成测试（自包含实现，不依赖 NewLife.Remoting）</summary>
[DisplayName("MCP 工具注册与 schema 测试")]
public class McpToolManagerTests
{
    /// <summary>含特殊成员的工具类：验证过滤规则（属性/运算符/泛型/Object 继承不注册）</summary>
    public class RegistrationTools
    {
        /// <summary>获取信息</summary>
        /// <param name="name">名称</param>
        /// <returns>信息</returns>
        public String GetInfo(String name) => name;

        /// <summary>异步获取</summary>
        /// <param name="url">地址</param>
        /// <returns>数据</returns>
        public async Task<String> FetchAsync(String url) { await Task.Yield(); return "data:" + url; }

        /// <summary>两数相加</summary>
        /// <param name="a">第一个数</param>
        /// <param name="b">第二个数</param>
        /// <returns>和</returns>
        public Int32 Add(Int32 a, Int32 b) => a + b;

        /// <summary>单数加一（重载）</summary>
        /// <param name="a">数</param>
        /// <returns>a+1</returns>
        public Int32 Add(Int32 a) => a + 1;

        /// <summary>名称（属性访问器，不应注册）</summary>
        public String Name { get; set; } = "";

        /// <summary>运算符重载，不应注册</summary>
        public static RegistrationTools operator +(RegistrationTools a, RegistrationTools b) => a;

        /// <summary>泛型方法，不应注册</summary>
        public T Echo<T>(T value) => value;

        /// <summary>Object 基类方法，不应注册</summary>
        public override String ToString() => "override";
    }

    /// <summary>含 IProgress/CancellationToken 参数的工具类：验证 schema 跳过基础设施参数</summary>
    public class InfrastructureParamTools
    {
        /// <summary>带进度与取消参数</summary>
        /// <param name="query">查询词</param>
        /// <param name="top">条数</param>
        /// <param name="progress">进度（框架注入）</param>
        /// <param name="cancellationToken">取消令牌（框架注入）</param>
        /// <returns>结果</returns>
        public String Search(String query, Int32 top = 5, IProgress<ProgressValue>? progress = null, System.Threading.CancellationToken cancellationToken = default)
            => $"{query}:{top}";
    }

    /// <summary>全类型参数工具类：验证 schema 类型映射（string/integer/number/boolean）</summary>
    public class TypeMappingTools
    {
        /// <summary>混合类型参数</summary>
        /// <param name="text">文本</param>
        /// <param name="count">数量</param>
        /// <param name="rate">比率</param>
        /// <param name="enabled">开关</param>
        /// <returns>拼接</returns>
        public String Mix(String text, Int32 count, Double rate, Boolean enabled) => $"{text}:{count}:{rate}:{enabled}";
    }

    /// <summary>无重载标量参数工具类：验证 schema 的 required 与类型（避免与注册重载测试混淆）</summary>
    public class ScalarParamTools
    {
        /// <summary>两数求和</summary>
        /// <param name="a">第一个数</param>
        /// <param name="b">第二个数</param>
        /// <returns>和</returns>
        public Int32 Sum(Int32 a, Int32 b) => a + b;

        /// <summary>带默认值参数</summary>
        /// <param name="name">名称</param>
        /// <param name="limit">条数上限</param>
        /// <returns>文本</returns>
        public String Query(String name, Int32 limit = 10) => $"{name}:{limit}";
    }

    private static McpToolManager CreateManager(Type type)
    {
        var manager = new McpToolManager();
        manager.Register(type);
        return manager;
    }

    #region 注册过滤
    [Fact]
    [DisplayName("注册—公共方法注册为 snake_case，Async 后缀剥离")]
    public void Register_ValidMethods_SnakeCase()
    {
        var manager = CreateManager(typeof(RegistrationTools));

        // GetInfo → get_info；FetchAsync → fetch（剥 Async）；Add → add
        Assert.Contains("get_info", manager.Tools.Keys);
        Assert.Contains("fetch", manager.Tools.Keys);
        Assert.Contains("add", manager.Tools.Keys);
    }

    [Fact]
    [DisplayName("注册—属性/运算符/泛型/Object 基类方法不注册")]
    public void Register_FiltersSpecialMembers()
    {
        var manager = CreateManager(typeof(RegistrationTools));

        // 属性访问器（Name）、运算符（op_Addition）、泛型（Echo）、Object 基类（ToString）都不应注册
        Assert.DoesNotContain("name", manager.Tools.Keys);
        Assert.DoesNotContain("op_addition", manager.Tools.Keys);
        Assert.DoesNotContain("echo", manager.Tools.Keys);
        Assert.DoesNotContain("tostring", manager.Tools.Keys);
        // 只有 3 个合法工具名：get_info/fetch/add（Add 重载同名覆盖）
        Assert.Equal(3, manager.Tools.Count);
    }

    [Fact]
    [DisplayName("注册—重载方法后定义覆盖前定义")]
    public void Register_Overload_LastWins()
    {
        var manager = CreateManager(typeof(RegistrationTools));

        // Add(Int32,Int32) 与 Add(Int32) 同名为 add，后注册者（Add(Int32)）覆盖
        var tool = manager.Find("add");
        Assert.NotNull(tool);
        Assert.Equal(1, tool.Method.GetParameters().Length);
    }

    [Fact]
    [DisplayName("注册—Find 大小写不敏感")]
    public void Find_CaseInsensitive()
    {
        var manager = CreateManager(typeof(RegistrationTools));

        Assert.NotNull(manager.Find("GET_INFO"));
        Assert.NotNull(manager.Find("Add"));
    }
    #endregion

    #region schema 生成（经 McpServer.tools/list 真实流程）
    private static McpServer CreateServer(Type type)
    {
        var server = new McpServer();
        server.AddTool(type);
        return server;
    }

    private static McpContext CreateContext(McpServer server)
        => new()
        {
            Services = server,
            GetRequest = key => key == "Mcp-Session-Id" ? "test-session" : null,
            SetResponse = (key, value) => { },
        };

    [Fact]
    [DisplayName("schema—标量参数类型映射与 required")]
    public void Schema_ScalarTypes_AndRequired()
    {
        var server = CreateServer(typeof(ScalarParamTools));
        var response = server.Process(new JsonRpcRequest("2.0", "tools/list", null, 1), CreateContext(server));

        var result = response.Result as ToolListResult;
        Assert.NotNull(result);
        var sum = result.Tools.FirstOrDefault(t => t.Name == "sum");
        Assert.NotNull(sum);
        var json = sum.InputSchema!.ToJson();

        // Sum(Int32 a, Int32 b)：两个 integer 必填
        Assert.Contains("\"a\"", json);
        Assert.Contains("\"b\"", json);
        Assert.Contains("\"integer\"", json);
        Assert.Contains("\"required\"", json);
    }

    [Fact]
    [DisplayName("schema—可选参数不进 required")]
    public void Schema_OptionalParam_NotRequired()
    {
        var server = CreateServer(typeof(ScalarParamTools));
        var response = server.Process(new JsonRpcRequest("2.0", "tools/list", null, 1), CreateContext(server));

        var result = response.Result as ToolListResult;
        Assert.NotNull(result);
        var query = result.Tools.FirstOrDefault(t => t.Name == "query");
        Assert.NotNull(query);
        var json = query.InputSchema!.ToJson();

        // name 必填、limit 有默认值（可选，不在 required）
        Assert.Contains("\"name\"", json);
        Assert.Contains("\"limit\"", json);
        Assert.DoesNotContain("\"limit\"", json.Substring(json.IndexOf("\"required\"", StringComparison.Ordinal)));
    }

    [Fact]
    [DisplayName("schema—IProgress/CancellationToken 不进入 schema")]
    public void Schema_SkipsInfrastructureParams()
    {
        var server = CreateServer(typeof(InfrastructureParamTools));
        var response = server.Process(new JsonRpcRequest("2.0", "tools/list", null, 1), CreateContext(server));

        var result = response.Result as ToolListResult;
        Assert.NotNull(result);
        var search = result.Tools.FirstOrDefault(t => t.Name == "search");
        Assert.NotNull(search);
        var json = search.InputSchema!.ToJson();

        // query/top 保留
        Assert.Contains("query", json);
        Assert.Contains("top", json);
        // progress/cancellation_token 不暴露（框架注入参数）
        Assert.DoesNotContain("progress", json);
        Assert.DoesNotContain("cancellation_token", json);
    }

    [Fact]
    [DisplayName("schema—类型映射覆盖 string/integer/number/boolean/object")]
    public void Schema_TypeMapping()
    {
        var server = CreateServer(typeof(TypeMappingTools));
        var response = server.Process(new JsonRpcRequest("2.0", "tools/list", null, 1), CreateContext(server));

        var result = response.Result as ToolListResult;
        Assert.NotNull(result);
        var mix = result.Tools.FirstOrDefault(t => t.Name == "mix");
        Assert.NotNull(mix);
        var json = mix.InputSchema!.ToJson();

        Assert.Contains("\"text\":{\"type\":\"string\"}", json);
        Assert.Contains("\"count\":{\"type\":\"integer\"}", json);
        Assert.Contains("\"rate\":{\"type\":\"number\"}", json);
        Assert.Contains("\"enabled\":{\"type\":\"boolean\"}", json);
    }
    #endregion
}
