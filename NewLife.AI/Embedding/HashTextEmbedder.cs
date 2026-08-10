using NewLife.Security;
using System.Text;

namespace NewLife.AI.Embedding;

/// <summary>基于 CJK Unigram+Bigram + Murmur128 哈希的本地文本向量化实现</summary>
/// <remarks>
/// <para>使用示例（本地嵌入，无外部服务依赖）：</para>
/// <code>
/// var embedder = new HashTextEmbedder(512);
/// Single[] vector = embedder.Embed("需要向量化的中文文本");
/// var data = VectorData.FromVector(embedder.ModelName, vector);   // 持久化
/// </code>
/// <para>实现原理：</para>
/// <list type="number">
/// <item><description>CJK 字符段落生成 Unigram（单字）和 Bigram（相邻双字）；非 CJK 字符按空白/标点切词</description></item>
/// <item><description>统计词频（TF = count / total）</description></item>
/// <item><description>对每个词项的 UTF-8 字节序列计算 Murmur128 哈希，取前 4 字节映射到 [0, Dimensions) 桶</description></item>
/// <item><description>多个词项可映射到同一桶（累加 TF 权重）</description></item>
/// <item><description>对结果向量进行 L2 归一化</description></item>
/// </list>
/// 模型名称固定为 <c>local-hash-v2</c>，不含维度信息。维度由 <see cref="Dimensions"/> 属性单独表达。
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

        // 混合去重哈希：
        // 1. CJK unigram/bigram 用 Int64 打包键去重（单字 key=char，双字 key=(char<<16)|char），
        //    避免物化 token 字符串（实测 200 句中文文本原实现分配 896KB，字符串物化占 ~500KB）
        // 2. 英文单词保留 String 键（需 string.ToLowerInvariant() 语义，可能多字符展开如 İ → i̇）
        // 3. 仅对唯一 token 调用 Murmur128——实测每次 ComputeHash 分配 ~144B（HashAlgorithm 内部开销），
        //    按出现次数逐次哈希会放大调用数（12600 次 vs 去重后 ~2000 次）
        // 数学等价：按唯一 token 累加 count/total，L2 归一化前向量逐位一致（黄金值测试锁定）。
        using var murmur = new Murmur128(0u);
        var cjkCounts = new Dictionary<Int64, Int32>();
        var wordCounts = new Dictionary<String, Int32>();
        var total = 0;

        var cjkBuf = new StringBuilder(16);
        var wordBuf = new StringBuilder(32);

        void FlushCjk()
        {
            if (cjkBuf.Length == 0) return;
            // Unigram（单字）：捕获字符级重叠
            for (var i = 0; i < cjkBuf.Length; i++)
            {
                var key = cjkBuf[i];
                if (cjkCounts.TryGetValue(key, out var cnt)) cjkCounts[key] = cnt + 1;
                else cjkCounts[key] = 1;
                total++;
            }
            // Bigram（相邻双字）：捕获词组级特征
            for (var i = 0; i < cjkBuf.Length - 1; i++)
            {
                var key = ((Int64)cjkBuf[i] << 16) | cjkBuf[i + 1];
                if (cjkCounts.TryGetValue(key, out var cnt)) cjkCounts[key] = cnt + 1;
                else cjkCounts[key] = 1;
                total++;
            }
            cjkBuf.Clear();
        }

        void FlushWord()
        {
            if (wordBuf.Length < 2) { wordBuf.Clear(); return; }
            // 保持原语义：string.ToLowerInvariant()（可能产生多字符展开，如 İ → i̇），故不逐字符转小写
            var word = wordBuf.ToString().ToLowerInvariant();
            wordBuf.Clear();
            if (wordCounts.TryGetValue(word, out var cnt)) wordCounts[word] = cnt + 1;
            else wordCounts[word] = 1;
            total++;
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

        if (total == 0) return vector;

        var t = (Single)total;
        var charPair = new Char[2];
        var byteBuffer = new Byte[8];

        // 哈希唯一 CJK token 并累加 TF 权重（单字 key ≤ 0xFFFF；双字 key ≥ 0x10000）
        foreach (var kv in cjkCounts)
        {
            Int32 charCount;
            if (kv.Key <= 0xFFFF)
            {
                charPair[0] = (Char)kv.Key;
                charCount = 1;
            }
            else
            {
                charPair[0] = (Char)(kv.Key >> 16);
                charPair[1] = (Char)(kv.Key & 0xFFFF);
                charCount = 2;
            }
            var byteCount = Encoding.UTF8.GetBytes(charPair, 0, charCount, byteBuffer, 0);
            var hash = murmur.ComputeHash(byteBuffer, 0, byteCount);
            var bucket = (Int32)(BitConverter.ToUInt32(hash, 0) % (UInt32)Dimensions);
            vector[bucket] += kv.Value / t;
        }

        // 哈希唯一英文单词 token
        foreach (var kv in wordCounts)
        {
            var bytes = Encoding.UTF8.GetBytes(kv.Key);
            var hash = murmur.ComputeHash(bytes);
            var bucket = (Int32)(BitConverter.ToUInt32(hash, 0) % (UInt32)Dimensions);
            vector[bucket] += kv.Value / t;
        }

        // L2 归一化
        var norm = 0.0f;
        foreach (var v in vector)
            norm += v * v;

        if (norm < 1e-10f) return vector;

#if NETSTANDARD2_1_OR_GREATER
        norm = MathF.Sqrt(norm);
#else
        norm = (Single)Math.Sqrt(norm);
#endif
        for (var i = 0; i < vector.Length; i++)
            vector[i] /= norm;

        return vector;
    }
    #endregion

    #region 辅助

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
