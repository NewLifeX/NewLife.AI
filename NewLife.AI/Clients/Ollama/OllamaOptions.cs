using System.Runtime.Serialization;

namespace NewLife.AI.Clients.Ollama;

/// <summary>Ollama 模型参数选项</summary>
public class OllamaOptions
{
    /// <summary>温度</summary>
    public Double? Temperature { get; set; }

    /// <summary>Top P</summary>
    [DataMember(Name = "top_p")]
    public Double? TopP { get; set; }

    /// <summary>Top K</summary>
    [DataMember(Name = "top_k")]
    public Int32? TopK { get; set; }

    /// <summary>最大生成 token 数</summary>
    [DataMember(Name = "num_predict")]
    public Int32? NumPredict { get; set; }

    /// <summary>停止序列</summary>
    public List<String>? Stop { get; set; }

    /// <summary>随机种子</summary>
    public Int32? Seed { get; set; }

    /// <summary>重复惩罚</summary>
    [DataMember(Name = "repeat_penalty")]
    public Double? RepeatPenalty { get; set; }

    /// <summary>存在惩罚</summary>
    [DataMember(Name = "presence_penalty")]
    public Double? PresencePenalty { get; set; }

    /// <summary>频率惩罚</summary>
    [DataMember(Name = "frequency_penalty")]
    public Double? FrequencyPenalty { get; set; }
}
