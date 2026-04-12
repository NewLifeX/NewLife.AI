#nullable enable
using System;
using System.Net.Http;
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

    private static Boolean IsOllamaAvailable()
    {
        if (_available.HasValue) return _available.Value;

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
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
