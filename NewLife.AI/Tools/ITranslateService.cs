namespace NewLife.AI.Tools;

/// <summary>文本翻译服务接口。支持多实现链式降级（MyMemory → 远程兜底）</summary>
public interface ITranslateService
{
    /// <summary>将文本翻译为目标语言</summary>
    /// <param name="text">要翻译的文本内容</param>
    /// <param name="targetLang">目标语言代码，如 zh、en、ja</param>
    /// <param name="sourceLang">源语言代码，auto 表示自动检测</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功返回翻译结果；失败或不可用返回 null</returns>
    Task<TranslateModel?> TranslateAsync(String text, String targetLang = "zh", String sourceLang = "auto", CancellationToken cancellationToken = default);
}

/// <summary>翻译结果</summary>
public class TranslateModel
{
    /// <summary>原始文本</summary>
    public String? Original { get; set; }

    /// <summary>翻译后文本</summary>
    public String? Translated { get; set; }

    /// <summary>源语言代码</summary>
    public String? SourceLang { get; set; }

    /// <summary>目标语言代码</summary>
    public String? TargetLang { get; set; }
}
