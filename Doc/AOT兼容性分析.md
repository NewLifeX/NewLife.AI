# NewLife.AI AOT 兼容性分析

> 分析日期：2026-10-20
> 目标框架：net45 / netstandard2.0 / netstandard2.1 / net8.0 / net10.0
> AOT 标准：.NET NativeAOT（`PublishAot=true`）+ `IsAotCompatible` 裁剪分析器

---

## 1. 结论

NewLife.AI 内核的**主链路已支持 NativeAOT**：服务商注册、客户端创建、工具 Schema 与调用、ToolChatClient 多轮循环、MCP 服务端、对话消息 JSON、Token 估算、哈希嵌入、限流、规划器等 33 个场景在 `win-x64` 原生发布件中全部通过。

- `net8.0` / `net10.0` 目标启用 `IsAotCompatible=true`，库自身裁剪/AOT 分析器告警为 0。
- 冒烟工程 `AotSmoke` 以同一份代码双跑 JIT 与 NativeAOT，便于区分"运行时行为变化"与"AOT 特有问题"。
- 少数动态能力按设计需要裁剪根或显式注册，边界见第 4 节。

---

## 2. 冒烟矩阵

| RID | 运行方式 | 状态 |
|-----|----------|------|
| win-x64 | 原生发布运行 | 阻断门禁 |
| win-arm64 | 原生发布运行 | 观察期 |
| linux-x64 | 原生发布运行 | 阻断门禁 |
| linux-arm64 | 原生发布运行 | 阻断门禁 |
| linux-musl-x64 / linux-musl-arm64 | alpine 容器内原生发布 | 观察期 |
| osx-x64 / osx-arm64 | 原生发布运行 | 观察期 |
| win-x86 | 交叉发布后运行 | 观察期 |
| linux-arm | QEMU 容器内 JIT 冒烟 + 交叉发布探索 | 观察期 |
| linux-loongarch64 | QEMU + 龙芯 SDK：JIT 冒烟 + 原生发布探索，人工触发 | 观察期 |
| linux-riscv64 | QEMU + 社区 SDK：JIT 冒烟 + 原生发布探索，人工触发 | 观察期 |

> 官方无 LA64 / RISC-V 的 runtime pack 与 NativeAOT 承诺，这两个 RID 只做"有产物才运行"的发布探索；主信号是同一份冒烟代码在该架构上的 JIT 行为一致性。

冒烟覆盖 33 个场景：

1. 基础类型转换
2. `ChatMessage` JSON 往返
3. `AiClientRegistry` 默认扫描
4. 服务商描述符元数据（模型/能力/价格）
5. 按编码创建客户端
6. `ChatClientBuilder` 管道装配
7. 工具 Schema 构建（基础类型/数组/嵌套对象）
8. 工具注册与参数类型转换
9. 工具别名与未知工具
10. `ToolChatClient` 多轮工具循环
11. Token 估算与预算截断
12. 哈希嵌入与向量相似度
13. MCP 工具管理器反射调用
14. MCP 服务器 JSON-RPC（initialize / tools/list）
15. 流式事件模型
16. 错误分类
17. 模型家族能力推断
18. 消息限流器
19. 内置工具（时间）
20. 编码计划 JSON 往返
21. `FunctionCallingPlanner` 解析
22. 本地化英文回退
23. OpenAI 请求体构建与 JSON 序列化
24. OpenAI 响应解析
25. Anthropic 请求/响应解析
26. Gemini 请求/响应解析
27. Ollama 响应解析
28. DashScope 请求构建与响应解析
29. MCP `tools/call` 完整 JSON-RPC
30. MCP `resources/read`
31. 内存向量库写入与检索
32. 显式注册服务商（自定义程序集场景）
33. 内置计算器（`DataTable.Compute` 在 AOT 下的行为）

---

## 3. 关键改造

| 位置 | 改造 |
|------|------|
| `NewLife.AI.csproj` | 新增 net8.0/net10.0 目标；net8+ 启用 `IsAotCompatible` |
| `AiClientRegistry` | `Register(Assembly)` 标记 `RequiresUnreferencedCode`/`RequiresDynamicCode`；`Register(Type)`/`Register<T>` 声明 `PublicConstructors` 保留要求；`Default` 的整程序集扫描由裁剪根兜底 |
| `ToolRegistry` | `AddTools<T>`/`GetToolMethods` 声明 `PublicMethods`；`AddToolsFromAssembly` 标记 `RequiresUnreferencedCode`；`Task<T>`/`ValueTask<T>` 反射读取 Result 处显式抑制并说明 |
| `ToolSchemaBuilder` | 递归 Schema 构建声明 `PublicProperties`，参数类型图由调用方保留 |
| `McpServer` / `McpToolManager` | 工具类型声明 `PublicMethods + PublicParameterlessConstructor + NonPublicConstructors`，`CreateInstance` 增加 `Activator` 非公构造回退；`Task<T>` 常用返回类型改模式匹配解包，避免 AOT 下反射读取 `Result` 失败；`IProgress<T>` 的 `MakeGenericType` 标注边界 |
| `AotSmoke` | `TrimmerRootAssembly Include="NewLife.AI"`，保证内置服务商与工具类型在原生发布件中可被扫描 |

---

## 4. 边界与不支持项

| 能力 | 状态 | 说明 |
|------|------|------|
| 内置 46 家服务商注册 | ✅ | 依赖裁剪根保留 `NewLife.AI` 程序集；自定义服务商放在独立程序集时，用 `Register<T>()` 显式注册或对宿主程序集加裁剪根 |
| `AddToolsFromAssembly` 程序集扫描 | ⚠️ | 标记 `RequiresUnreferencedCode`；AOT 下改用 `AddTools<T>()` / `AddTools(instance)` |
| 内置计算器 `DataTable.Compute` | ✅ | win-x64 NativeAOT 实测可用；其他 RID 由冒烟矩阵验证，若不可用会转为错误信息，不影响其余工具 |
| MCP 工具的 `IProgress<T>` 参数 | ⚠️ | 按运行期类型构造泛型实例，AOT 需保留对应泛型实参；冒烟不覆盖该路径 |
| 工具返回非常用 `Task<T>` 类型 | ⚠️ | 常用类型（string/int/long/bool/double/float/decimal/byte[]/object）走模式匹配，其余回退反射读取 `Result` |
| `NewLife.AI.Extensions`（ASP.NET Core 宿主） | ⚠️ | 依赖 ASP.NET Core 反射 JSON；AOT 部署建议直接使用 `NewLife.AI` 的 `McpServer` 自建端点 |
| 插件式动态加载程序集 | ❌ | 与 AOT 静态分析根本冲突，按设计不支持 |

---

## 5. 验证方式

```powershell
# 库自身分析器（0 告警）
dotnet build NewLife.AI\NewLife.AI.csproj -f net8.0 -c Release -p:WarningsAsErrors=IL2026,IL2067,IL2070,IL2072,IL2075,IL2091,IL3050

# JIT 对照
dotnet run -c Release --project AotSmoke\AotSmoke.csproj

# NativeAOT 发布运行
dotnet publish AotSmoke\AotSmoke.csproj -c Release -r win-x64 -o AotSmoke\bin\aot-out
.\AotSmoke\bin\aot-out\AotSmoke.exe
```

CI 工作流 `.github/workflows/aot.yml` 执行分析器检查与多 RID 矩阵，任一阻断门禁失败即失败。
