using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.AI.Tools;
using NewLife.Caching;
using NewLife.IP;
using Stardust.Extensions;

namespace NewLife.StarChat.Controllers;

/// <summary>公共工具 API。免密钥对外提供 IP 归属地、天气、翻译、搜索、网页爬取等能力</summary>
/// <remarks>初始化工具 API 控制器</remarks>
/// <param name="serviceProvider">服务提供者，用于按需解析各工具服务</param>
[ApiController]
[AllowAnonymous]
public class ToolApiController(IServiceProvider serviceProvider, ICacheProvider cacheProvider) : ControllerBase
{
    /// <summary>查询 IP 地址归属地信息</summary>
    /// <param name="ip">要查询的 IP 地址，留空则查询调用方公网 IP</param>
    [HttpGet("api/ip")]
    public async Task<IActionResult> GetIpLocation(String? ip)
    {
        // 未传入 IP 时使用请求来源 IP
        if (ip.IsNullOrWhiteSpace()) ip = HttpContext.GetUserHost();
        if (!ip.IsNullOrEmpty() && (ip.EqualIgnoreCase("::1") || ip.StartsWith("127.")))
            return Ok(new IpLocationModel { Ip = ip, Address = "本机" });

        var key = $"iploc:{ip}";
        var result = cacheProvider.InnerCache.Get<IpLocationModel>(key);
        if (result != null) return Ok(result);

        foreach (var svc in serviceProvider.GetServices<IIpLocationService>())
        {
            result = await svc.GetLocationAsync(ip, HttpContext.RequestAborted).ConfigureAwait(false);
        }

        // 启用内置IP库
        if (result == null && !ip.IsNullOrWhiteSpace() && NetHelper.IpResolver is IpResolver resolver)
        {
            var (area, addr) = resolver.GetAddress(ip);
            if (!addr.IsNullOrWhiteSpace())
            {
                result = new IpLocationModel { Ip = ip, City = area, Address = addr };
            }
        }

        if (result != null)
        {
            cacheProvider.InnerCache.Set(key, result, 10 * 60);

            return Ok(result);
        }

        return NotFound(new { error = "ip location not found" });
    }

    /// <summary>查询指定城市的实时天气</summary>
    /// <param name="city">城市名称</param>
    /// <param name="unit">温度单位：C 或 F</param>
    [HttpGet("api/weather")]
    public async Task<IActionResult> GetWeather(String city, String unit = "C")
    {
        if (String.IsNullOrWhiteSpace(city)) return BadRequest(new { error = "city is required" });

        var key = $"weather:{city}:{unit}";
        var cached = cacheProvider.InnerCache.Get<WeatherModel>(key);
        if (cached != null) return Ok(cached);

        foreach (var svc in serviceProvider.GetServices<IWeatherService>())
        {
            var result = await svc.GetWeatherAsync(city, unit, HttpContext.RequestAborted).ConfigureAwait(false);
            if (result != null)
            {
                cacheProvider.InnerCache.Set(key, result, 10 * 60);
                return Ok(result);
            }
        }

        return NotFound(new { error = "weather data not found" });
    }

    /// <summary>文本翻译</summary>
    /// <param name="text">要翻译的文本</param>
    /// <param name="targetLang">目标语言代码</param>
    /// <param name="sourceLang">源语言代码</param>
    [HttpGet("api/translate")]
    public async Task<IActionResult> Translate(String text, String targetLang = "zh", String sourceLang = "auto")
    {
        if (String.IsNullOrEmpty(text)) return BadRequest(new { error = "text is required" });

        var key = $"translate:{sourceLang}:{targetLang}:{text}";
        var cached = cacheProvider.InnerCache.Get<TranslateModel>(key);
        if (cached != null) return Ok(cached);

        foreach (var svc in serviceProvider.GetServices<ITranslateService>())
        {
            var result = await svc.TranslateAsync(text, targetLang, sourceLang, HttpContext.RequestAborted).ConfigureAwait(false);
            if (result != null)
            {
                cacheProvider.InnerCache.Set(key, result, 10 * 60);
                return Ok(result);
            }
        }

        return NotFound(new { error = "translation failed" });
    }

    /// <summary>搜索互联网信息</summary>
    /// <param name="query">搜索关键词</param>
    /// <param name="count">返回结果数量</param>
    [HttpGet("api/search")]
    public async Task<IActionResult> Search(String query, Int32 count = 5)
    {
        if (String.IsNullOrWhiteSpace(query)) return BadRequest(new { error = "query is required" });

        count = Math.Max(1, Math.Min(count, 10));

        var key = $"search:{query}:{count}";
        var cached = cacheProvider.InnerCache.Get<SearchModel>(key);
        if (cached != null) return Ok(cached);

        foreach (var svc in serviceProvider.GetServices<ISearchService>())
        {
            var result = await svc.SearchAsync(query, count, HttpContext.RequestAborted).ConfigureAwait(false);
            if (result != null && result.Items.Count > 0)
            {
                cacheProvider.InnerCache.Set(key, result, 10 * 60);
                return Ok(result);
            }
        }

        return NotFound(new { error = "no search results" });
    }

    /// <summary>爬取网页内容并提取正文文本</summary>
    /// <param name="url">网页地址</param>
    /// <param name="maxLength">最大返回字符数</param>
    [HttpGet("api/fetch")]
    public async Task<IActionResult> Fetch(String url, Int32 maxLength = 5000)
    {
        if (String.IsNullOrWhiteSpace(url)) return BadRequest(new { error = "url is required" });

        var key = $"fetch:{url}:{maxLength}";
        var cached = cacheProvider.InnerCache.Get<WebFetchModel>(key);
        if (cached != null) return Ok(cached);

        foreach (var svc in serviceProvider.GetServices<IWebFetchService>())
        {
            var result = await svc.FetchAsync(url, maxLength, HttpContext.RequestAborted).ConfigureAwait(false);
            if (result != null)
            {
                cacheProvider.InnerCache.Set(key, result, 10 * 60);
                return Ok(result);
            }
        }

        return NotFound(new { error = "fetch failed" });
    }
}
