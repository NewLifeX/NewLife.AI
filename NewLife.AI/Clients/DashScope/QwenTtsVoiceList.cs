using System.Reflection;
using NewLife.Log;
using NewLife.Serialization;

namespace NewLife.AI.Clients.DashScope;

/// <summary>Qwen-TTS 音色列表。从嵌入资源加载，提供按模型查询音色的能力</summary>
/// <remarks>
/// 数据来源：阿里云官方文档"Qwen-TTS音色列表"（https://help.aliyun.com/zh/model-studio/qwen-tts-voice-list）<br/>
/// 嵌入资源路径：NewLife.AI.Resources.QwenTtsVoices.json<br/>
/// 支持模型：qwen-tts、qwen3-tts-flash、qwen3-tts-instruct-flash 及对应 Realtime 变体
/// </remarks>
public static class QwenTtsVoiceList
{
    private static List<ModelVoices>? _models;
    private static readonly Object _lock = new();

    /// <summary>获取全部模型及其音色列表</summary>
    public static IReadOnlyList<ModelVoices> GetAll()
    {
        EnsureLoaded();
        return _models!.AsReadOnly();
    }

    /// <summary>获取指定模型的音色列表</summary>
    /// <param name="modelCode">模型编码。如 qwen-tts、qwen3-tts-flash</param>
    /// <returns>音色列表；模型不存在时返回空</returns>
    public static IReadOnlyList<VoiceInfo> GetVoices(String modelCode)
    {
        EnsureLoaded();
        return _models?.FirstOrDefault(m => m.Code.EqualIgnoreCase(modelCode))?.Voices
               ?? (IReadOnlyList<VoiceInfo>)new VoiceInfo[0];
    }

    /// <summary>校验音色是否在指定模型的合法范围内</summary>
    /// <param name="modelCode">模型编码</param>
    /// <param name="voiceId">音色标识</param>
    /// <returns>合法音色返回 true</returns>
    /// <remarks>
    /// qwen3-tts-vd（声音设计）和 qwen3-tts-vc（声音复刻）模型无系统音色列表，
    /// 视为仅支持专属音色，接受任何非已知系统音色的 ID。
    /// </remarks>
    public static Boolean IsValidVoice(String modelCode, String voiceId)
    {
        if (voiceId.IsNullOrEmpty()) return false;

        // OpenAI 兼容音色由客户端内部映射，不校验
        if (voiceId.EqualIgnoreCase("alloy", "echo", "fable", "nova", "onyx", "shimmer"))
            return true;

        var voices = GetVoices(modelCode);

        // 模型有音色列表时，直接校验
        if (voices.Count > 0)
            return voices.Any(v => v.Id.EqualIgnoreCase(voiceId));

        // 模型无音色列表（如 vd/vc 系列，仅支持专属音色）：
        // 拒绝已知系统音色（引导用户使用正确模型），接受自定义专属音色 ID
        if (IsKnownSystemVoice(voiceId))
            return false;

        return true;
    }

    /// <summary>检查音色是否为任意模型下的已知系统音色</summary>
    private static Boolean IsKnownSystemVoice(String voiceId)
    {
        EnsureLoaded();
        return _models!.Any(m => m.Voices.Any(v => v.Id.EqualIgnoreCase(voiceId)));
    }

    #region 加载

    private static void EnsureLoaded()
    {
        if (_models != null) return;

        lock (_lock)
        {
            if (_models != null) return;

            try
            {
                var assembly = typeof(QwenTtsVoiceList).Assembly;
                using var stream = assembly.GetManifestResourceStream("NewLife.AI.Resources.QwenTtsVoices.json");
                if (stream == null)
                {
                    XTrace.WriteLine("QwenTtsVoiceList: 嵌入资源未找到，音色列表为空");
                    _models = [];
                    return;
                }

                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();

                var root = json.ToJsonEntity<QwenTtsRoot>();
                _models = root?.Models?.Select(m => new ModelVoices(
                    m.Code ?? String.Empty,
                    m.Name ?? String.Empty,
                    m.Voices?.Select(v => new VoiceInfo(
                        v.Id ?? String.Empty,
                        v.Name ?? String.Empty,
                        v.Description ?? String.Empty,
                        v.Age ?? String.Empty,
                        v.Language ?? String.Empty,
                        v.Scenario ?? String.Empty,
                        v.Gender ?? String.Empty
                    )).ToList() ?? []
                )).ToList() ?? [];
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
                _models = [];
            }
        }
    }

    #endregion

    #region JSON 结构

    private class QwenTtsRoot
    {
        public List<QwenTtsModel>? Models { get; set; }
    }

    private class QwenTtsModel
    {
        public String? Code { get; set; }
        public String? Name { get; set; }
        public List<QwenTtsVoice>? Voices { get; set; }
    }

    private class QwenTtsVoice
    {
        public String? Id { get; set; }
        public String? Name { get; set; }
        public String? Description { get; set; }
        public String? Age { get; set; }
        public String? Language { get; set; }
        public String? Scenario { get; set; }
        public String? Gender { get; set; }
    }

    #endregion
}
