using NewLife.Security;
using System.Text;

namespace NewLife.AI.Embedding;

/// <summary>基于 CJK Unigram+Bigram + Murmur128 哈希的本地文本向量化实现</summary>
/// <remarks>
/// 实现原理：
/// <list type="number">
/// <item><description>CJK 字符段落生成 Unigram（单字）和 Bigram（相邻双字）；非 CJK 字符按空白/标点切词</description></item>
/// <item><description>统计词频（TF = count / total）</description></item>
/// <item><description>对每个词项的 UTF-8 字节序列计算 Murmur128 哈希，取前 4 字节映射到 [0, Dimensions) 桶</description></item>
/// <item><description>多个词项可映射到同一桶（累加 TF 权重）</description></item>
/// <item><description>对结果向量进行 L2 归一化</description></item>
/// </list>
/// 模型名称固定为 <c>local-hash-v1</c>，不含维度信息。维度由 <see cref="Dimensions"/> 属性单独表达。
/// 陈旧检测通过 <see cref="VectorData.IsStale(String, Int32)"/> 同时比较模型名和维度数实现。
/// </remarks>
public class HashTextEmbedder : ILocalTextEmbedder
{
    #region 属性

    /// <summary>模型名称，固定为 local-hash-v2（v2 起新增 Unigram），不含维度信息</summary>
    public String ModelName { get; }

    /// <summary>向量维度数，默认 512</summary>
    public Int32 Dimensions { get; }

    #endregion

    #region 构造

    /// <summary>实例化，指定向量维度</summary>
    /// <param name="dimensions">向量维度，默认 512；更大维度可降低哈希碰撞但不提升语义质量</param>
    public HashTextEmbedder(Int32 dimensions = 512)
    {
        if (dimensions <= 0) throw new ArgumentOutOfRangeException(nameof(dimensions), "维度必须大于 0");
        Dimensions = dimensions;
        ModelName = "local-hash-v2";
    }

    #endregion

    #region 方法

    /// <summary>将文本转换为 L2 归一化的 Single[] 向量</summary>
    /// <param name="text">待嵌入文本</param>
    /// <returns>L2 归一化向量；输入为空时返回零向量</returns>
    public Single[] Embed(String text)
    {
        var vector = new Single[Dimensions];
        if (text.IsNullOrWhiteSpace()) return vector;

        var tokens = Tokenize(text);
        if (tokens.Count == 0) return vector;

        // TF：词项 → 出现次数
        var tf = new Dictionary<String, Int32>(tokens.Count);
        foreach (var token in tokens)
        {
            if (tf.TryGetValue(token, out var cnt))
                tf[token] = cnt + 1;
            else
                tf[token] = 1;
        }

        // 哈希映射并累加 TF 权重
        var total = (Single)tokens.Count;
        foreach (var (token, count) in tf)
        {
            var tokenBytes = Encoding.UTF8.GetBytes(token);
            using var murmur = new Murmur128(0u);
            var hash = murmur.ComputeHash(tokenBytes);
            var bucket = (Int32)(BitConverter.ToUInt32(hash, 0) % (UInt32)Dimensions);
            vector[bucket] += count / total;
        }

        // L2 归一化
        var norm = 0.0f;
        foreach (var v in vector)
            norm += v * v;

        if (norm < 1e-10f) return vector;

        norm = MathF.Sqrt(norm);
        for (var i = 0; i < vector.Length; i++)
            vector[i] /= norm;

        return vector;
    }

    #endregion

    #region 辅助

    /// <summary>对文本进行分词：CJK 字符段提取 Bigram，非 CJK 按空白/标点切词</summary>
    /// <param name="text">原始文本</param>
    /// <returns>词项列表</returns>
    private static List<String> Tokenize(String text)
    {
        var tokens = new List<String>(text.Length);
        var cjkBuf = new StringBuilder(16);
        var wordBuf = new StringBuilder(32);

        void FlushCjk()
        {
            if (cjkBuf.Length == 0) return;
            var seg = cjkBuf.ToString();
            if (seg.Length == 1)
            {
                tokens.Add(seg);
            }
            else
            {
                // 提取 Unigram（单字）：捕获字符级重叠，提升相关文本相似度
                for (var i = 0; i < seg.Length; i++)
                    tokens.Add(seg[i].ToString());
                // 提取所有相邻 Bigram：捕获词组级特征
                for (var i = 0; i < seg.Length - 1; i++)
                    tokens.Add(seg.Substring(i, 2));
            }
            cjkBuf.Clear();
        }

        void FlushWord()
        {
            if (wordBuf.Length < 2) { wordBuf.Clear(); return; }
            tokens.Add(wordBuf.ToString().ToLowerInvariant());
            wordBuf.Clear();
        }

        foreach (var ch in text)
        {
            if (IsCjk(ch))
            {
                FlushWord();
                cjkBuf.Append(ch);
            }
            else if (Char.IsLetter(ch) || Char.IsDigit(ch))
            {
                FlushCjk();
                wordBuf.Append(ch);
            }
            else
            {
                FlushCjk();
                FlushWord();
            }
        }

        FlushCjk();
        FlushWord();

        return tokens;
    }

    /// <summary>判断字符是否属于 CJK 统一汉字区</summary>
    /// <param name="c">字符</param>
    /// <returns>是 CJK 字符返回 true</returns>
    private static Boolean IsCjk(Char c) =>
        (c >= '\u4E00' && c <= '\u9FFF')   // CJK 统一汉字
        || (c >= '\u3400' && c <= '\u4DBF') // CJK 扩展 A
        || (c >= '\uF900' && c <= '\uFAFF') // CJK 兼容汉字
        || (c >= '\u3040' && c <= '\u30FF') // 平假名 / 片假名
        || (c >= '\uAC00' && c <= '\uD7AF'); // 韩文音节

    #endregion
}
