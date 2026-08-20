using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using NewLife.AI.Benchmark;

// NewLife.AI 关键路径性能基准。Release 模式运行：dotnet run -c Release --project Benchmark
// 使用 InProcessEmitToolchain：进程内运行，不生成样板工程。
// 原因：默认 CsProj 工具链生成的样板工程强制 Deterministic=true，与 NewLife.AI 的 AssemblyVersion 通配符（1.5.*）冲突（CS8357）。
// 进程内运行对热点识别足够准确；如需完整隔离基准，需先移除库的 AssemblyVersion 通配符。
var config = DefaultConfig.Instance
    .AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));

BenchmarkRunner.Run<HashEmbeddingBenchmark>(config);
BenchmarkRunner.Run<VectorDataBenchmark>(config);
BenchmarkRunner.Run<MessageParseBenchmark>(config);
BenchmarkRunner.Run<SchemaBuildBenchmark>(config);
BenchmarkRunner.Run<BuildBodyBenchmark>(config);
BenchmarkRunner.Run<ToolLoopBenchmark>(config);
BenchmarkRunner.Run<StreamChunkParseBenchmark>(config);
BenchmarkRunner.Run<ToolArgParsingBenchmark>(config);
BenchmarkRunner.Run<SchemaSizeBenchmark>(config);
