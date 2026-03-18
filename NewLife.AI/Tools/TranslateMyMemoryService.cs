using NewLife.Serialization;

namespace NewLife.AI.Tools;

/// <summary>MyMemory 文本翻译实现。免费，每天 5000 词额度，支持 60+ 种语言</summary>
/// <remarks>初始化 MyMemory 翻译服务</remarks>
/// <param name="httpClient">HTTP 客户端；为 null 时自动创建默认实例</param>
public class TranslateMyMemoryService(HttpClient? httpClient = null) : ITranslateService
{
    private readonly HttpClient _http = httpClient ?? ToolHelper.CreateDefaultHttpClient();

    /// <summary>将文本翻译为目标语言</summary>
    /// <param name="text">要翻译的文本内容</param>
    /// <param name="targetLang">目标语言代码</param>
    /// <param name="sourceLang">源语言代码，auto 表示自动检测</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功返回翻译结果；失败返回 null</returns>
    public async Task<TranslateModel?> TranslateAsync(String text, String targetLang = "zh", String sourceLang = "auto", CancellationToken cancellationToken = default)
    {
        try
        {
            var q = Uri.EscapeDataString(text);
            var pair = Uri.EscapeDataString($"{sourceLang}|{targetLang}");
            var resp = await _http.GetAsync(
                $"https://api.mymemory.translated.net/get?q={q}&langpair={pair}",
                cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            var data = json.ToJsonEntity<MyMemoryResponse>();

            if (data?.ResponseStatus != 200) return null;

            return new TranslateModel
            {
                Original = text,
                Translated = data.ResponseData?.TranslatedText,
                SourceLang = sourceLang,
                TargetLang = targetLang,
            };
        }
        catch
        {
            return null;
        }
    }

    #region 内部模型
    private class MyMemoryTranslation { public String? TranslatedText { get; set; } }
    private class MyMemoryResponse
    {
        public MyMemoryTranslation? ResponseData { get; set; }
        public Int32 ResponseStatus { get; set; }
        public String? ResponseDetails { get; set; }
    }
    #endregion
}
