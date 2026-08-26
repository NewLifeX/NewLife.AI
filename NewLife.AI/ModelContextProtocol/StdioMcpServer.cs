using System.IO;
using System.Text;
using NewLife.Serialization;

namespace NewLife.AI.ModelContextProtocol;

/// <summary>标准输入输出 MCP 服务器。读取 stdin 的 newline-delimited JSON-RPC 请求，响应写入 stdout（对齐官方 StdioServerTransport）</summary>
/// <remarks>
/// 用于被外部进程（IDE、Claude Desktop 等）作为子进程拉起：客户端启动本进程并接管 stdin/stdout。
/// 每条 JSON-RPC 消息独占一行；响应同样以单行 JSON 输出（UTF-8 无 BOM）。与 stdio 客户端配套可实现本地 MCP 服务。
/// </remarks>
public class StdioMcpServer : McpServer
{
    #region 属性
    /// <summary>输入流。默认 <see cref="Console.OpenStandardInput"/></summary>
    public Stream Input { get; set; } = null!;

    /// <summary>输出流。默认 <see cref="Console.OpenStandardOutput"/></summary>
    public Stream Output { get; set; } = null!;

    /// <summary>是否正在运行</summary>
    public Boolean Running { get; private set; }
    #endregion

    #region 方法
    /// <summary>启动读取循环。阻塞直到输入流结束或取消</summary>
    /// <param name="cancellationToken">取消令牌</param>
    public void Run(CancellationToken cancellationToken = default)
    {
        Input ??= Console.OpenStandardInput();
        Output ??= Console.OpenStandardOutput();

        // leaveOpen 避免释放 stdin/stdout（进程级流不应被关闭）
        using var reader = new StreamReader(Input, Encoding.UTF8, false, 4096, leaveOpen: true);
        using var writer = new StreamWriter(Output, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };

        Running = true;
        String? line;
        while (!cancellationToken.IsCancellationRequested && (line = reader.ReadLine()) != null)
        {
            if (line.IsNullOrWhiteSpace()) continue;

            var request = line.ToJsonEntity<JsonRpcRequest>();
            if (request == null) continue;

            var ctx = new McpContext
            {
                Services = this,
                GetRequest = _ => null,
                SetResponse = (_, _) => { },
            };

            try
            {
                var rs = Process(request, ctx);
                if (rs != null) writer.WriteLine(rs.ToJson(false, true, true));
            }
            catch (Exception ex)
            {
                WriteLog("MCP stdio 处理 {0} 失败：{1}", request.Method, ex.Message);
            }
        }
        Running = false;
    }
    #endregion
}
