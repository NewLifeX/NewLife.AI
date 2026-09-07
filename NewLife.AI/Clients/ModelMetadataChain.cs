using NewLife.AI.Models;

namespace NewLife.AI.Clients;

/// <summary>模型元数据解析结果。全字段可空：null/0 = 该解析器未声明，交由低优先级解析器补齐；
/// 作为解析链（<see cref="ModelMetadataResolverChain"/>）各级之间与消费方的传输对象，统一承载
/// 模型显示名、能力位、上下文/输出长度、推理强度与价格（Token 四档 或 非 Token 单价）</summary>
/// <param name="Name">显示名</param>
/// <param name="SupportThinking">支持思考</param>
/// <param name="SupportFunction">支持函数调用</param>
/// <param name="SupportVision">支持视觉</param>
/// <param name="SupportAudio">支持语音识别</param>
/// <param name="SupportSpeech">支持语音合成</param>
/// <param name="SupportImage">支持文生图</param>
/// <param name="SupportVideo">支持文生视频</param>
/// <param name="SupportEmbedding">支持嵌入向量</param>
/// <param name="SupportRerank">支持重排序</param>
/// <param name="ContextLength">上下文长度（Token 数）</param>
/// <param name="MaxOutputTokens">最大输出长度（Token 数）</param>
/// <param name="ReasoningEfforts">推理强度选项，如 "high,max"</param>
/// <param name="PricingMode">计费模式。Token/Image/Video/Embedding/Speech；null=未声明（按 Token 处理）</param>
/// <param name="Pricing">Token 四档价格。非 Token 模式为 null</param>
/// <param name="UnitPrice">非 Token 模式单价（元/张、元/秒、元/百万Token、元/千字符）</param>
/// <param name="UnitName">单价单位标签</param>
/// <param name="FromFallbackPricing">价格是否来自能力分级兜底。写库方据此保留管理员已设置的价格</param>
public record AiModelMetadata(
    String? Name = null,
    Boolean? SupportThinking = null,
    Boolean? SupportFunction = null,
    Boolean? SupportVision = null,
    Boolean? SupportAudio = null,
    Boolean? SupportSpeech = null,
    Boolean? SupportImage = null,
    Boolean? SupportVideo = null,
    Boolean? SupportEmbedding = null,
    Boolean? SupportRerank = null,
    Int32? ContextLength = null,
    Int32? MaxOutputTokens = null,
    String? ReasoningEfforts = null,
    PricingMode? PricingMode = null,
    AiModelPricing? Pricing = null,
    Decimal? UnitPrice = null,
    String? UnitName = null,
    Boolean FromFallbackPricing = false)
{
    /// <summary>是否声明了任一能力位/上下文/推理强度字段。用于判断是否为能力覆盖，纯价格条目返回 false</summary>
    public Boolean HasCapability =>
        SupportThinking != null || SupportFunction != null || SupportVision != null || SupportAudio != null ||
        SupportSpeech != null || SupportImage != null || SupportVideo != null || SupportEmbedding != null ||
        SupportRerank != null || ContextLength != null || ReasoningEfforts != null;

    /// <summary>是否声明了任一字段（能力/名称/价格）。全空表示解析器未识别该模型</summary>
    public Boolean HasAny =>
        HasCapability || Name != null || MaxOutputTokens != null || PricingMode != null || Pricing != null ||
        UnitPrice != null || UnitName != null;
}

/// <summary>模型元数据解析器。按 <see cref="Order"/> 升序参与解析链合成，高优先级（Order 小）声明的字段生效，
/// 低优先级只补未声明字段。解析器只负责"识别并产出元数据块"，不负责写库</summary>
public interface IModelMetadataResolver
{
    /// <summary>优先级。越小越先；多个解析器声明同一字段时，高优先级（Order 小）生效</summary>
    Int32 Order { get; }

    /// <summary>解析指定模型的部分元数据。无法识别返回 null 或仅填可空字段</summary>
    /// <param name="current">当前已合成结果（比本解析器更高优先级已贡献的部分），供按能力位分级等场景读取</param>
    /// <param name="provider">提供商编码（Code 优先，Provider 兜底），可为空</param>
    /// <param name="model">模型编码</param>
    /// <returns>元数据块；未声明字段保持 null，交低优先级解析器补齐</returns>
    AiModelMetadata? Resolve(AiModelMetadata? current, String? provider, String? model);
}

/// <summary>模型元数据解析链。静态注册表（同 <see cref="AiClientRegistry"/> 风格），把"模型元数据解析"
/// 收敛为一条有序链：上层（如 StarChat ModelData 元数据表）注册 Order 最小的解析器即可"精确值优先"，
/// NewLife.AI 内置描述符推断与分级兜底作为默认。所有写库入口统一消费 <see cref="Resolve"/> 结果，
/// 能力与价格同一来源，避免多个写者相互覆盖</summary>
public static class ModelMetadataResolverChain
{
    /// <summary>已注册解析器（按 Order 升序）。写时复制：注册时加锁整体替换数组快照，读取零锁零拷贝</summary>
    public static IModelMetadataResolver[] List { get; private set; } = [];

    private static readonly Object _sync = new();

    static ModelMetadataResolverChain()
    {
        // 默认解析器静态就位：描述符推断（能力/名称/价格）+ 能力分级兜底价，开箱即用
        List = [new DescriptorModelMetadataResolver(), new FallbackModelMetadataResolver()];
    }

    /// <summary>注册解析器。同类型同 Order 已存在时后注册者覆盖（允许上层在测试/不同目录场景替换
    /// 同款解析器实例）；不同 Order 的同类型按各自优先级分别注册，Order 相同后注册者排后。
    /// 注册为写时复制：由当前快照构造列表，替换/追加后按 Order 排序写回新数组，并发读取不受影响</summary>
    /// <param name="resolver">解析器</param>
    public static void Register(IModelMetadataResolver resolver)
    {
        if (resolver == null) throw new ArgumentNullException(nameof(resolver));

        lock (_sync)
        {
            var list = List.ToList();
            var i = list.FindIndex(e => e.GetType() == resolver.GetType() && e.Order == resolver.Order);
            if (i >= 0)
                list[i] = resolver;
            else
                list.Add(resolver);

            List = [.. list.OrderBy(e => e.Order)];
        }
    }

    /// <summary>合成解析：按 Order 升序逐个解析，低优先级只补高优先级未声明的字段</summary>
    /// <param name="provider">提供商编码（Code 优先，Provider 兜底），可为空</param>
    /// <param name="model">模型编码</param>
    /// <returns>合成元数据；所有解析器均未识别时返回 null</returns>
    public static AiModelMetadata? Resolve(String? provider, String? model)
    {
        AiModelMetadata? md = null;
        foreach (var resolver in List)
        {
            var block = resolver.Resolve(md, provider, model);
            if (block == null) continue;
            md = md == null ? block : Merge(md, block);
        }
        return md;
    }

    /// <summary>把低优先级块 <paramref name="block"/> 补进已合成结果 <paramref name="md"/>：
    /// 仅当 <paramref name="md"/> 某字段未声明时填入 <paramref name="block"/> 的值。
    /// 价格作为整体维度：<paramref name="md"/> 已有任何价格（Token 四档或非 Token 单价）时，
    /// 低优先级的另一形态价格不再补入，避免 Token/单价并存</summary>
    /// <param name="md">已合成结果（更高优先级贡献）</param>
    /// <param name="block">低优先级解析器贡献的元数据块</param>
    /// <returns>合并后的结果</returns>
    private static AiModelMetadata Merge(AiModelMetadata md, AiModelMetadata block)
    {
        var hasPrice = md.Pricing != null || md.PricingMode != null || md.UnitPrice != null;

        return md with
        {
            Name = md.Name ?? block.Name,
            SupportThinking = md.SupportThinking ?? block.SupportThinking,
            SupportFunction = md.SupportFunction ?? block.SupportFunction,
            SupportVision = md.SupportVision ?? block.SupportVision,
            SupportAudio = md.SupportAudio ?? block.SupportAudio,
            SupportSpeech = md.SupportSpeech ?? block.SupportSpeech,
            SupportImage = md.SupportImage ?? block.SupportImage,
            SupportVideo = md.SupportVideo ?? block.SupportVideo,
            SupportEmbedding = md.SupportEmbedding ?? block.SupportEmbedding,
            SupportRerank = md.SupportRerank ?? block.SupportRerank,
            ContextLength = md.ContextLength ?? block.ContextLength,
            MaxOutputTokens = md.MaxOutputTokens ?? block.MaxOutputTokens,
            ReasoningEfforts = md.ReasoningEfforts ?? block.ReasoningEfforts,
            PricingMode = md.PricingMode ?? (hasPrice ? null : block.PricingMode),
            Pricing = md.Pricing ?? (hasPrice ? null : block.Pricing),
            UnitPrice = md.UnitPrice ?? (hasPrice ? null : block.UnitPrice),
            UnitName = md.UnitName ?? (hasPrice ? null : block.UnitName),
            FromFallbackPricing = hasPrice ? md.FromFallbackPricing : block.FromFallbackPricing,
        };
    }
}
