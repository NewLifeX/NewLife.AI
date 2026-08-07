#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using Xunit;

namespace XUnitTest.Models;

/// <summary>验证 NewLife.Core CollectionHelper.ToDictionary 对 JsonElement 的支持行为（A-106 重构前置分析）</summary>
[DisplayName("CollectionHelper.ToDictionary JsonElement 支持验证")]
public class ToDictionaryJsonElementTests
{
    [Fact]
    [DisplayName("ToDictionary—JsonElement对象递归转换")]
    public void JsonElement_Object_ToDictionary()
    {
        var json = """{"name":"get_weather","description":"查询天气","input_schema":{"type":"object","properties":{"city":{"type":"string"}}}}""";
        using var doc = JsonDocument.Parse(json);
        var dic = doc.RootElement.ToDictionary();

        Assert.NotNull(dic);
        Assert.Equal("get_weather", dic["name"] as String);
        Assert.Equal("查询天气", dic["description"] as String);

        // 嵌套对象 input_schema 应为字典（NullableDictionary 大小写不敏感）
        var schema = Assert.IsAssignableFrom<IDictionary<String, Object?>>(dic["input_schema"]);
        Assert.Equal("object", schema["type"] as String);
    }

    [Fact]
    [DisplayName("ToDictionary—JsonElement嵌套数组转换")]
    public void JsonElement_Array_ToDictionary()
    {
        var json = """{"functionDeclarations":[{"name":"get_weather","description":"查询天气","parameters":{"type":"object"}}]}""";
        using var doc = JsonDocument.Parse(json);
        var dic = doc.RootElement.ToDictionary();

        // functionDeclarations 应为 IList<Object?>，元素为字典
        var decls = Assert.IsAssignableFrom<IList<Object?>>(dic["functionDeclarations"]);
        Assert.Single(decls);
        var first = Assert.IsAssignableFrom<IDictionary<String, Object?>>(decls[0]);
        Assert.Equal("get_weather", first["name"] as String);
    }
}
