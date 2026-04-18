using System;
using System.Linq;
using System.Net.Http;
using NewLife.AI.Clients.Ollama;
using Xunit;

namespace XUnitTest.Helpers;

/// <summary>当本机 Ollama 服务不可用时自动跳过的 Fact 特性</summary>
/// <remarks>
/// 首次使用时探测 http://localhost:11434/api/version，
/// 若请求失败则设置 Skip 原因，后续测试不再重复探测。
/// </remarks>
public sealed class OllamaFactAttribute : FactAttribute
{
    private static Boolean? _available;

    public OllamaFactAttribute()
    {
        if (!IsOllamaAvailable())
            Skip = "Ollama 服务未在 localhost:11434 运行，跳过集成测试";
    }

    internal static Boolean IsOllamaAvailable()
    {
        if (_available.HasValue) return _available.Value;

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = client.GetAsync("http://localhost:11434/api/version").Result;
            _available = response.IsSuccessStatusCode;
        }
        catch
        {
            _available = false;
        }

        return _available.Value;
    }
}

/// <summary>当本机 Ollama 未安装指定重量模型时自动跳过的 Fact 特性</summary>
/// <remarks>探测 Ollama 服务并检查 qwen3.5:latest 是否已拉取，未安装则跳过。</remarks>
public sealed class OllamaHeavyFactAttribute : FactAttribute
{
    private const String HeavyModel = "qwen3.5:latest";
    private static Boolean? _available;

    public OllamaHeavyFactAttribute()
    {
        if (!OllamaFactAttribute.IsOllamaAvailable())
        {
            Skip = "Ollama 服务未在 localhost:11434 运行，跳过集成测试";
            return;
        }
        if (!IsHeavyModelAvailable())
            Skip = $"{HeavyModel} 未安装，跳过重量模型测试（执行 ollama pull {HeavyModel} 安装）";
    }

    private static Boolean IsHeavyModelAvailable()
    {
        if (_available.HasValue) return _available.Value;

        try
        {
            using var client = new OllamaChatClient(null, HeavyModel);
            var result = client.ListModelsAsync().GetAwaiter().GetResult();
            _available = result?.Models?.Any(m =>
                String.Equals(m.Model, HeavyModel, StringComparison.OrdinalIgnoreCase) ||
                String.Equals(m.Name, HeavyModel, StringComparison.OrdinalIgnoreCase)) ?? false;
        }
        catch
        {
            _available = false;
        }

        return _available.Value;
    }
}
