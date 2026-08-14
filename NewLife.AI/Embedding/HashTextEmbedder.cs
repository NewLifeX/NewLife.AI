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
        // 3. 仅对唯一 token 哈希——实测 Murmur128.ComputeHash 每次分配 ~144B（HashAlgorithm 内部开销），
        //    按出现次数逐次哈希会放大调用数（12600 次 vs 去重后 ~2000 次）
        // 4. 内联零分配 Murmur128（ComputeMurmur128）：NAI 目标框架 net45/netstandard2.0/netstandard2.1
        //    无法编译 .NET 5+ 的 HashAlgorithm.TryComputeHash（netstandard2.1 引用程序集无该 API），
        //    故按 NewLife.Security.Murmur128（seed=0）逐位复刻写入预分配缓冲区，消除逐 token 分配
        // 数学等价：按唯一 token 累加 count/total，L2 归一化前向量逐位一致（黄金值测试锁定）。
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
        var hashBuffer = new Byte[16];
        var wordBytes = new Byte[64];

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
            ComputeMurmur128(byteBuffer, 0, byteCount, hashBuffer);
            var bucket = (Int32)(BitConverter.ToUInt32(hashBuffer, 0) % (UInt32)Dimensions);
            vector[bucket] += kv.Value / t;
        }

        // 哈希唯一英文单词 token（复用缓冲区，避免逐词分配 byte[]）
        foreach (var kv in wordCounts)
        {
            var byteCount = Encoding.UTF8.GetByteCount(kv.Key);
            if (byteCount > wordBytes.Length) wordBytes = new Byte[byteCount];
            Encoding.UTF8.GetBytes(kv.Key, 0, kv.Key.Length, wordBytes, 0);
            ComputeMurmur128(wordBytes, 0, byteCount, hashBuffer);
            var bucket = (Int32)(BitConverter.ToUInt32(hashBuffer, 0) % (UInt32)Dimensions);
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

    const UInt64 C1 = 0x87c37b91114253d5;
    const UInt64 C2 = 0x4cf5ad432745937f;

    /// <summary>内联 Murmur128 哈希（seed=0），零分配写入 16 字节目标缓冲区</summary>
    /// <param name="src">源数据</param>
    /// <param name="offset">起始偏移</param>
    /// <param name="count">字节数</param>
    /// <param name="dst">目标缓冲区，长度至少 16，写入小端序哈希值</param>
    /// <remarks>
    /// 与 NewLife.Security.Murmur128（seed=0）输出逐位一致，由黄金值测试锁定。
    /// 内联原因：本库目标框架 net45/netstandard2.0/netstandard2.1 无法编译 .NET 5+ 的
    /// HashAlgorithm.TryComputeHash，且 Murmur128.ComputeHash 每次调用分配 ~144B，
    /// 内联后写入预分配缓冲区，彻底消除逐 token 分配。
    /// </remarks>
    private static void ComputeMurmur128(Byte[] src, Int32 offset, Int32 count, Byte[] dst)
    {
        var h1 = 0uL;
        var h2 = 0uL;

        var remainder = count & 15;
        var aligned = offset + count - remainder;
        for (var i = offset; i < aligned; i += 16)
        {
            h1 ^= RotateLeft(ToUInt64(src, i) * C1, 31) * C2;
            h1 = (RotateLeft(h1, 27) + h2) * 5 + 0x52dce729;

            h2 ^= RotateLeft(ToUInt64(src, i + 8) * C2, 33) * C1;
            h2 = (RotateLeft(h2, 31) + h1) * 5 + 0x38495ab5;
        }

        if (remainder > 0)
        {
            // 尾部不足 16 字节的处理（与 NewLife.Security.Murmur128 逐位一致）
            var k1 = 0uL;
            var k2 = 0uL;
            switch (remainder)
            {
                case 15: k2 ^= (UInt64)src[aligned + 14] << 48; goto case 14;
                case 14: k2 ^= (UInt64)src[aligned + 13] << 40; goto case 13;
                case 13: k2 ^= (UInt64)src[aligned + 12] << 32; goto case 12;
                case 12: k2 ^= (UInt64)src[aligned + 11] << 24; goto case 11;
                case 11: k2 ^= (UInt64)src[aligned + 10] << 16; goto case 10;
                case 10: k2 ^= (UInt64)src[aligned + 9] << 8; goto case 9;
                case 9: k2 ^= (UInt64)src[aligned + 8]; goto case 8;
                case 8: k1 ^= (UInt64)src[aligned + 7] << 56; goto case 7;
                case 7: k1 ^= (UInt64)src[aligned + 6] << 48; goto case 6;
                case 6: k1 ^= (UInt64)src[aligned + 5] << 40; goto case 5;
                case 5: k1 ^= (UInt64)src[aligned + 4] << 32; goto case 4;
                case 4: k1 ^= (UInt64)src[aligned + 3] << 24; goto case 3;
                case 3: k1 ^= (UInt64)src[aligned + 2] << 16; goto case 2;
                case 2: k1 ^= (UInt64)src[aligned + 1] << 8; goto case 1;
                case 1: k1 ^= src[aligned]; break;
            }

            h2 ^= RotateLeft(k2 * C2, 33) * C1;
            h1 ^= RotateLeft(k1 * C1, 31) * C2;
        }

        var len = (UInt64)count;
        h1 ^= len; h2 ^= len;
        h1 += h2; h2 += h1;
        h1 = FMix(h1); h2 = FMix(h2);
        h1 += h2; h2 += h1;

        // 小端序写入（与 BitConverter.GetBytes 在主流平台一致）
        dst[0] = (Byte)h1;
        dst[1] = (Byte)(h1 >> 8);
        dst[2] = (Byte)(h1 >> 16);
        dst[3] = (Byte)(h1 >> 24);
        dst[4] = (Byte)(h1 >> 32);
        dst[5] = (Byte)(h1 >> 40);
        dst[6] = (Byte)(h1 >> 48);
        dst[7] = (Byte)(h1 >> 56);
        dst[8] = (Byte)h2;
        dst[9] = (Byte)(h2 >> 8);
        dst[10] = (Byte)(h2 >> 16);
        dst[11] = (Byte)(h2 >> 24);
        dst[12] = (Byte)(h2 >> 32);
        dst[13] = (Byte)(h2 >> 40);
        dst[14] = (Byte)(h2 >> 48);
        dst[15] = (Byte)(h2 >> 56);
    }

    /// <summary>读取 8 字节小端序 UInt64</summary>
    private static UInt64 ToUInt64(Byte[] data, Int32 i) =>
        (UInt64)data[i] | ((UInt64)data[i + 1] << 8) | ((UInt64)data[i + 2] << 16) |
        ((UInt64)data[i + 3] << 24) | ((UInt64)data[i + 4] << 32) | ((UInt64)data[i + 5] << 40) |
        ((UInt64)data[i + 6] << 48) | ((UInt64)data[i + 7] << 56);

    private static UInt64 RotateLeft(UInt64 x, Byte r) => (x << r) | (x >> (64 - r));

    private static UInt64 FMix(UInt64 h)
    {
        h = (h ^ (h >> 33)) * 0xff51afd7ed558ccd;
        h = (h ^ (h >> 33)) * 0xc4ceb9fe1a85ec53;

        return (h ^ (h >> 33));
    }

    #endregion
}
