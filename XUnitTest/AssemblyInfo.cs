using Xunit;

// 测试涉及进程级共享状态（Localization.Culture、XCode 连接、WebApplicationFactory 宿主、随机端口），
// 并行执行会相互干扰并导致跨平台偶发失败；统一串行运行以保证稳定。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
