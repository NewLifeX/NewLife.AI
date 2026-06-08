using System.Reflection;
using NewLife.Log;
using NewLife.Serialization;

namespace NewLife.AI.Clients.DashScope;

/// <summary>CosyVoice 音色信息</summary>
/// <param name="Id">音色标识。如 longxiaochun_v3</param>
/// <param name="Name">音色名称。如 龙小淳</param>
/// <param name="Description">音色描述</param>
/// <param name="Age">年龄段</param>
/// <param name="Language">支持语言</param>
/// <param name="Scenario">适用场景</param>
/// <param name="Gender">性别。male/female</param>
public sealed record VoiceInfo(String Id, String Name, String Description, String Age, String Language, String Scenario, String Gender);

/// <summary>CosyVoice 模型音色组</summary>
/// <param name="Code">模型编码。如 cosyvoice-v3-flash</param>
/// <param name="Name">模型名称</param>
/// <param name="Voices">音色列表</param>
public sealed record ModelVoices(String Code, String Name, IReadOnlyList<VoiceInfo> Voices);

/// <summary>CosyVoice 音色列表。从嵌入资源加载，提供按模型查询音色的能力</summary>
/// <remarks>
/// 数据来源：阿里云官方文档"CosyVoice音色列表"（https://help.aliyun.com/zh/model-studio/cosyvoice-voice-list）<br/>
/// 嵌入资源路径：NewLife.AI.Resources.CosyVoiceVoices.json
/// </remarks>
public static class CosyVoiceVoiceList
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
    /// <param name="modelCode">模型编码。如 cosyvoice-v3-flash</param>
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
    /// <returns>合法音色返回 true；不在列表内返回 false（含 OpenAI 兼容音色如 alloy/echo 等，这些由客户端映射处理）</returns>
    public static Boolean IsValidVoice(String modelCode, String voiceId)
    {
        if (voiceId.IsNullOrEmpty()) return false;

        // OpenAI 兼容音色由客户端内部映射，不校验
        if (voiceId.EqualIgnoreCase("alloy", "echo", "fable", "nova", "onyx", "shimmer"))
            return true;

        var voices = GetVoices(modelCode);
        return voices.Any(v => v.Id.EqualIgnoreCase(voiceId));
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
                var assembly = typeof(CosyVoiceVoiceList).Assembly;
                using var stream = assembly.GetManifestResourceStream("NewLife.AI.Resources.CosyVoiceVoices.json");
                if (stream == null)
                {
                    XTrace.WriteLine("CosyVoiceVoiceList: 嵌入资源未找到，音色列表为空");
                    _models = [];
                    return;
                }

                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();

                var root = json.ToJsonEntity<CosyVoiceRoot>();
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

    private class CosyVoiceRoot
    {
        public List<CosyVoiceModelDto>? Models { get; set; }
    }

    private class CosyVoiceModelDto
    {
        public String? Code { get; set; }
        public String? Name { get; set; }
        public List<CosyVoiceDto>? Voices { get; set; }
    }

    private class CosyVoiceDto
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
