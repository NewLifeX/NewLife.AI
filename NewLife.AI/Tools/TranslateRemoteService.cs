using NewLife.Serialization;

namespace NewLife.AI.Tools;

/// <summary>远程翻译实现。通过 HTTP 调用远端 API（默认 ai.newlifex.com），作为兜底方案</summary>
/// <remarks>初始化远程翻译服务</remarks>
/// <param name="baseUrl">远程服务基础 URL，默认 https://ai.newlifex.com</param>
/// <param name="httpClient">HTTP 客户端；为 null 时自动创建默认实例</param>
public class TranslateRemoteService(String baseUrl = "https://ai.newlifex.com", HttpClient? httpClient = null) : ITranslateService
{
    private readonly HttpClient _http = httpClient ?? ToolHelper.CreateDefaultHttpClient();
    private readonly String _baseUrl = baseUrl.TrimEnd('/');

    /// <summary>将文本翻译为目标语言</summary>
    /// <param name="text">要翻译的文本内容</param>
    /// <param name="targetLang">目标语言代码</param>
    /// <param name="sourceLang">源语言代码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功返回翻译结果；失败返回 null</returns>
    public async Task<TranslateModel?> TranslateAsync(String text, String targetLang = "zh", String sourceLang = "auto", CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_baseUrl}/api/translate?text={Uri.EscapeDataString(text)}&targetLang={Uri.EscapeDataString(targetLang)}&sourceLang={Uri.EscapeDataString(sourceLang)}";
            var resp = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return json.ToJsonEntity<TranslateModel>();
        }
        catch
        {
            return null;
        }
    }
}
