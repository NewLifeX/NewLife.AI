using NewLife.AI.Clients.OpenAI;

namespace NewLife.AI.Clients;

/// <summary>模型家族注册表。集中注册各模型家族档案，供所有服务商共享推断</summary>
/// <remarks>
/// 家族为全局共享：任何服务商（含 OpenAI 兼容的第三方平台）发现家族内模型时，
/// 通过 <see cref="OpenAIClientBase.InferModelCapabilities"/> 基类自动按家族规则推断能力。
/// 注册顺序决定匹配优先级：模型标识命中多个家族时，先注册者生效（如 qwen-media 先于 qwen）。
/// </remarks>
public static class ModelFamilyRegistry
{
    private static readonly List<ModelFamily> _families = [];

    /// <summary>已注册的家族列表（按注册顺序）</summary>
    public static ModelFamily[] Families
    {
        get
        {
            ModelFamilies.EnsureRegistered();
            lock (_families) return [.. _families];
        }
    }

    /// <summary>注册家族。重复注册同名家族时后者原位替换（保持注册顺序）</summary>
    /// <param name="family">家族档案</param>
    public static void Register(ModelFamily family)
    {
        if (family == null || family.Name.IsNullOrEmpty()) return;

        lock (_families)
        {
            var idx = _families.FindIndex(e => e.Name.Equals(family.Name, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                _families[idx] = family;
            else
                _families.Add(family);
        }
    }

    /// <summary>按名称查找家族</summary>
    /// <param name="name">家族名称</param>
    /// <returns>家族档案，未找到返回 null</returns>
    public static ModelFamily? Find(String? name)
    {
        if (name.IsNullOrEmpty()) return null;

        ModelFamilies.EnsureRegistered();
        lock (_families)
        {
            return _families.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>按模型标识匹配家族并推断能力。按注册顺序返回首个命中家族的结果</summary>
    /// <param name="modelId">模型标识</param>
    /// <returns>推断出的能力，无家族命中时返回 null</returns>
    public static AiProviderCapabilities? Match(String? modelId)
    {
        if (modelId.IsNullOrEmpty()) return null;

        ModelFamilies.EnsureRegistered();
        ModelFamily[] families;
        lock (_families)
        {
            families = [.. _families];
        }
        foreach (var family in families)
        {
            var caps = family.Infer(modelId!);
            if (caps != null) return caps;
        }
        return null;
    }
}
