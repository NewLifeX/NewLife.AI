using BenchmarkDotNet.Running;

// NewLife.AI 关键路径性能基准。Release 模式运行：dotnet run -c Release --project Benchmark
// 按需选择：dotnet run -c Release --project Benchmark --filter "*HashEmbedding*"
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
