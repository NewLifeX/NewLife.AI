#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using NewLife;
using NewLife.Serialization;

namespace XUnitTest.Helpers;

/// <summary>DashScope 测试配置（JSON 文件结构）</summary>
public class DashScopeTestConfig
{
    public String? ApiKey { get; set; }
    public String? CustomVoiceId { get; set; }
    public String? Organization { get; set; }
}

/// <summary>DashScope 密钥加载器。统一从 config/DashScope.key 加载配置</summary>
/// <remarks>
/// 新格式：JSON（DashScopeTestConfig，含 ApiKey/CustomVoiceId/Organization）
/// 旧格式：纯文本（首行为 ApiKey）
/// 旧格式首次读取后自动转为 JSON 写回；文件不存在时自动创建空白 JSON 配置。
/// 环境变量 DASHSCOPE_API_KEY / DASHSCOPE_CUSTOM_VOICE_ID / DASHSCOPE_ORGANIZATION 可覆盖文件值。
/// </remarks>
public static class DashScopeKeyLoader
{
    /// <summary>从指定路径加载配置。自动识别 JSON 或纯文本格式，旧格式自动转为 JSON 写回</summary>
    /// <param name="configPath">配置文件路径，默认 config/DashScope.key</param>
    /// <returns>解析后的配置对象，文件不存在时返回空配置</returns>
    public static DashScopeTestConfig? LoadConfig(String? configPath = null)
    {
        var path = (configPath ?? "config/DashScope.key").GetFullPath();
        var dir = Path.GetDirectoryName(path);
        if (!String.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(path))
        {
            // 文件不存在，创建一个空的 JSON 配置
            var empty = new DashScopeTestConfig();
            File.WriteAllText(path, empty.ToJson(true));
            return empty;
        }

        var content = File.ReadAllText(path).Trim();

        // 检测是否为 JSON 格式
        if (content.StartsWith("{"))
        {
            try
            {
                var cfg = content.ToJsonEntity<DashScopeTestConfig>();
                if (cfg != null)
                {
                    // 处理旧格式：ApiKey 字段包含嵌套 JSON（双重序列化），扁平化后写回
                    if (cfg.ApiKey != null && cfg.ApiKey.StartsWith("{"))
                    {
                        try
                        {
                            var nested = cfg.ApiKey.ToJsonEntity<DashScopeTestConfig>();
                            if (nested != null)
                            {
                                cfg = new DashScopeTestConfig
                                {
                                    ApiKey = nested.ApiKey,
                                    CustomVoiceId = nested.CustomVoiceId ?? cfg.CustomVoiceId,
                                    Organization = nested.Organization ?? cfg.Organization,
                                };
                                // 写回正确的扁平格式
                                File.WriteAllText(path, cfg.ToJson(true));
                            }
                        }
                        catch { }
                    }
                    return cfg;
                }
            }
            catch { }
        }

        // 旧格式纯文本：首行为 ApiKey，转为 JSON 后写回
        var apiKey = content;
        if (!apiKey.IsNullOrEmpty())
        {
            var cfg = new DashScopeTestConfig { ApiKey = apiKey };
            File.WriteAllText(path, cfg.ToJson(true));
            return cfg;
        }

        return null;
    }

    /// <summary>从 config/DashScope.key 或环境变量加载 ApiKey</summary>
    public static String? LoadApiKey(String? configPath = null)
    {
        var cfg = LoadConfig(configPath);
        if (cfg != null && !cfg.ApiKey.IsNullOrEmpty())
            return cfg.ApiKey;
        return Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY");
    }
}
