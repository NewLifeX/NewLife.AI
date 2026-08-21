using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using NewLife.AI.Tools;
using NewLife.Log;
using JsonArray = System.Text.Json.Nodes.JsonArray;

namespace NewLife.ChatAI.Tools;

/// <summary>结构化图表工具。LLM 输出紧凑 JSON 规范（~200 tokens），前端 ECharts 立即渲染交互式图表</summary>
/// <remarks>
/// <para>对比 show_widget（生成完整 HTML）：</para>
/// <list type="table">
/// <listheader><term>指标</term><term>show_widget</term><term>show_chart</term></listheader>
/// <item><term>LLM 生成量</term><term>10~20k tokens HTML</term><term>~200 tokens JSON</term></item>
/// <item><term>等待时间</term><term>40~80 s</term><term>1~3 s</term></item>
/// <item><term>图表质量</term><term>静态</term><term>交互式（缩放/悬停/导出图片）</term></item>
/// </list>
/// <para>支持图表类型：bar / line / pie / scatter / radar / map_china / heatmap / gauge / funnel / treemap</para>
/// </remarks>
/// <param name="log">日志</param>
public class ChartToolService(ILog log)
{
    #region 常量

    private static readonly HashSet<String> _validTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "bar", "line", "pie", "scatter", "radar",
        "map_china", "heatmap", "gauge", "funnel", "treemap",
    };

    #endregion

    #region 工具方法

    /// <summary>渲染交互式数据图表到对话气泡。只需 JSON 规范（~200 tokens），前端 ECharts 即时渲染，比 show_widget 快 20 倍</summary>
    /// <param name="type">图表类型：bar | line | pie | scatter | radar | map_china | heatmap | gauge | funnel | treemap</param>
    /// <param name="title">图表标题（≤ 30 字）</param>
    /// <param name="data">
    /// JSON 对象，结构因 type 而异：
    /// - bar/line：{"xAxis":["Q1","Q2"],"series":[{"name":"销售","data":[100,200],"smooth":true,"area":false}],"unit":"万元","legend":true}
    /// - pie/funnel/treemap：{"series":[{"name":"占比","data":[{"name":"A","value":40},{"name":"B","value":60}]}]}
    /// - scatter：{"xAxis":{"name":"身高(cm)"},"yAxis":{"name":"体重(kg)"},"series":[{"name":"样本","data":[[170,65],[175,70]]}]}
    /// - radar：{"indicators":[{"name":"速度","max":100},{"name":"力量","max":100}],"series":[{"name":"角色A","data":[80,90]},{"name":"角色B","data":[70,85]}]}
    ///   ⚠️ radar 强制约束：① indicators 数量必须等于每个 series[].data 的长度；② series[].data 必须是纯数值数组 [v1,v2,...]，禁止写成属性式 {速度:80,力量:90}
    ///   radar 简化格式（系统自动转换）：{"dimensions":["速度","力量"],"series":[{"name":"A","values":[80,90]},{"name":"B","values":[70,85]}]}
    /// - map_china：{"series":[{"name":"GDP","data":[{"name":"广东","value":135673},{"name":"江苏","value":122875}]}],"unit":"亿元"}
    /// - heatmap：{"xAxis":["周一","周二","周三"],"yAxisCategories":["0时","1时","2时"],"series":[{"name":"访问量","data":[[0,0,5],[0,1,3],[1,0,8]]}],"unit":"次"}
    /// - gauge：{"series":[{"name":"完成率","data":[{"name":"完成","value":75}]}],"unit":"%"}
    /// 通用可选字段：legend（boolean）、colors（string[]，覆盖默认配色）、animation（boolean，默认 true，密集数据建议 false）、backgroundColor（string，默认透明，如 "#f8fafc"）
    /// </param>
    /// <param name="context">工具调用上下文（框架自动注入）</param>
    /// <returns>ToolResult（ForUser=完整Chart JSON + ForLlm=简短确认）</returns>
    [ToolDescription("show_chart", IsSystem = false,
        Triggers = "柱状图,折线图,饼图,散点图,雷达图,热力图,仪表盘,漏斗图,中国地图,图表,数据可视化,占比分析,对比分析,数值统计",
        AssistantTriggers = "柱状图,折线图,饼图,散点图,雷达图,热力图,仪表盘,漏斗图,中国地图,数据图表,可视化展示",
        ReadOnly = true)]
    [DisplayName("交互式图表")]
    [Description("将数据渲染为交互式图表（可缩放/悬停/导出图片）。比 show_widget 快 20 倍：只需输出 JSON 规范（约 200 tokens），前端 ECharts 立即渲染。支持：bar（柱状）、line（折线/面积）、pie（饼/环形）、scatter（散点）、radar（雷达）、map_china（中国省份热力地图）、heatmap（矩阵热力）、gauge（仪表盘）、funnel（漏斗）、treemap（矩形树图）。适用于数值比较、时序数值变化（折线图）、占比分析、地域分布等场景。⚠️ 行程规划、历史事件、里程碑、时间线等场景请使用 show_timeline，本工具不支持 timeline 类型。🎨 视觉风格：请通过 data.colors 数组主动传递配色方案（不要依赖默认 ECharts 配色）。选色建议：单系列用品牌主色、多系列用色相环均匀分布（10~12 色）、对比图用互补色、趋势图用单色渐变。财务数据用蓝金、健康医疗用蓝绿白、科技 AI 用紫蓝青。大数据量建议 data.animation=false 减少渲染压力。")]
    public ToolResult ShowChart(
        [Description("图表类型：bar | line | pie | scatter | radar | map_china | heatmap | gauge | funnel | treemap")] String type,
        [Description("图表标题（≤ 30 字），如『2024年各省 GDP 分布』")] String title,
        [Description(@"JSON 对象，结构见说明。bar示例：{""xAxis"":[""Q1"",""Q2""],""series"":[{""name"":""收入"",""data"":[100,200]}],""unit"":""万元""} ；radar多系列示例：{""indicators"":[{""name"":""速度"",""max"":100},{""name"":""力量"",""max"":100}],""series"":[{""name"":""A"",""data"":[80,90]},{""name"":""B"",""data"":[70,85]}]} ；⚠️radar要求：indicators数量==每个series[i].data长度，data必须是纯数值数组")] Object data,
        ToolCallContext? context = null)
    {
        if (data == null)
            throw new ToolException("参数错误：data 不能为 null", "请提供合法的 JSON 数据对象后重试，或直接回复用户说明无法生成图表。数据格式参见工具说明。");

        // 兼容：AI 常把 type 写进 data 内部（如 {"data":{"type":"bar",...}}），从 data 中自动提取
        if (type.IsNullOrEmpty())
            (type, data) = TryExtractTypeFromData(type, data);

        if (!_validTypes.Contains(type))
            throw new ToolException($"不支持的图表类型 '{type}'", $"请选择有效类型后重试，或直接回复用户说明情况。");

        var normalizedType = type.ToLower();

        // 统一转为 JSON 字符串：LLM 可能传 JSON 对象（推荐）或已转义的 JSON 字符串（兼容旧版）
        var dataStr = data as String;
        if (dataStr == null) dataStr = JsonSerializer.Serialize(data);
        if (dataStr.IsNullOrEmpty())
            throw new ToolException("参数错误：data 不能为空", "请提供合法的 JSON 数据对象后重试，或直接回复用户说明无法生成图表。");

        // 验证并解析 data 为 JSON 节点（确保合法 JSON，同时准备嵌入返回值）
        JsonNode dataNode;
        try
        {
            dataNode = JsonNode.Parse(dataStr);
            if (dataNode == null)
                throw new ToolException("data 解析后为 null", "请检查 JSON 格式后重试，或直接回复用户说明无法生成图表。");
        }
        catch (JsonException ex)
        {
            throw new ToolException($"data JSON 格式错误：{ex.Message}", $"请检查 JSON 语法后重试，确保 data 为合法 JSON 对象，或直接回复用户说明情况。");
        }

        dataNode = NormalizeDataNode(normalizedType, title, dataNode);

        var chartId = context?.ToolCallId;
        if (chartId.IsNullOrEmpty()) chartId = $"chart_{Guid.NewGuid():N}";

        // 将 data 作为嵌套对象（非字符串）写入返回 JSON，避免前端二次解析
        var result = new JsonObject
        {
            ["chartId"] = JsonValue.Create(chartId),
            ["type"]    = JsonValue.Create(normalizedType),
            ["title"]   = JsonValue.Create(title),
            ["data"]    = dataNode,
        };

        log.Info("[Chart] {0} '{1}'，id={2}", type, title, chartId);

        // 从 JsonSerializerOptions.Default 派生以携带 TypeInfoResolver，避免 ToJsonString 内部
        // 对 JsonValueCustomized 节点调用 MakeReadOnly() 时抛 "must specify a TypeInfoResolver"
        var writeOptions = new JsonSerializerOptions(JsonSerializerOptions.Default) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var resultJson = result.ToJsonString(writeOptions);
        return ToolResult.ForAudiences(resultJson, $"[已渲染图表到客户端：{title}]");
    }

    /// <summary>
    /// 兼容 AI 常见错误：把 type 写进 data 对象内部（如 {"data":{"type":"bar",...}}）。
    /// 从 data 中提取 type 并返回剥离后的 data。
    /// </summary>
    /// <returns>(提取到的type, 剥离type后的data)。若无法提取则原样返回</returns>
    private static (String type, Object data) TryExtractTypeFromData(String type, Object data)
    {
        // 将 data 统一转为 JsonObject，便于查找和剥离 type 字段
        JsonObject? obj = data switch
        {
            JsonObject jo => jo,
            JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Object => JsonSerializer.Deserialize<JsonObject>(je.GetRawText()),
            String str => TryParseJsonObject(str),
            // 工具框架将 JSON 对象反序列化为 Dictionary<String, Object?>，
            // 需要转为 JsonObject 才能提取内嵌的 type 字段
            IDictionary<String, Object?> dict => TryParseJsonObject(JsonSerializer.Serialize(dict)),
            _ => null,
        };

        if (obj == null) return (type, data);

        // 查找 type 字段
        var innerType = obj["type"] is JsonValue jv ? jv.GetValue<String>() : null;
        if (innerType.IsNullOrEmpty() || !_validTypes.Contains(innerType))
            return (type, data);

        // 剥离 type 字段后返回
        obj.Remove("type");
        return (innerType, obj);
    }

    private static JsonObject? TryParseJsonObject(String str)
    {
        try { return JsonNode.Parse(str) as JsonObject; }
        catch { return null; }
    }

    private static JsonNode NormalizeDataNode(String type, String title, JsonNode dataNode)
    {
        if (dataNode is JsonArray array)
        {
            return type switch
            {
                "pie" or "funnel" or "treemap" or "gauge" => WrapSeries(title, array),
                _ => dataNode,
            };
        }

        if (dataNode is not JsonObject obj) return dataNode;

        // 【适配0】LLM 偶发把 series 输出为单个对象而非数组（如 {"series":{"name":"A","data":[...]}}）
        // 包装为单元素数组，避免前端对非数组调用 .map 渲染崩溃
        if (obj["series"] is JsonObject singleSeries)
        {
            obj["series"] = new JsonArray { singleSeries.DeepClone() };
        }

        // 【适配1】LLM 输出 data.radar.indicator 格式（将雷达维度嵌在 radar 对象内）
        // 转换为标准格式：indicators 直接在 data 下
        if (type == "radar" && obj["radar"] is JsonObject radarObj && obj["indicators"] == null)
        {
            if (radarObj["indicator"] is JsonArray indicators)
            {
                var result = new JsonObject();
                foreach (var kvp in obj)
                {
                    if (kvp.Key != "radar")
                    {
                        result[kvp.Key] = kvp.Value?.DeepClone();
                    }
                }
                result["indicators"] = indicators.DeepClone();
                return result;
            }
        }

        // 【适配1.5】LLM 输出 data.radar[] + data.indicator[] 并列格式（直观的多系列雷达图格式）
        // 输入：{"radar":[{name:"北京","保值得分":90,"配套成熟度":95,...},{name:"上海",...}],"indicator":[{name:"保值得分",max:100},...]}
        // 输出：{"series":[{name:"北京",data:[90,95,...]},{name:"上海",data:[...]}],"indicators":[{name:"保值得分",max:100},...]}
        if (type == "radar" && obj["radar"] is JsonArray radarArr && obj["indicator"] is JsonArray indicatorArr && obj["series"] == null && obj["indicators"] == null)
        {
            // 提取维度名称列表（维度顺序）
            var dimensionNames = new List<String>();
            var indicatorsResult = new JsonArray();
            foreach (var ind in indicatorArr.OfType<JsonObject>())
            {
                if (ind["name"] is JsonNode nameNode && nameNode.GetValueKind() == System.Text.Json.JsonValueKind.String)
                {
                    var dimName = nameNode.GetValue<String>();
                    dimensionNames.Add(dimName);
                    
                    var indObj = new JsonObject { ["name"] = JsonValue.Create(dimName) };
                    if (ind["max"] is JsonNode maxNode) indObj["max"] = maxNode.DeepClone();
                    indicatorsResult.Add(indObj);
                }
            }

            // 转换 radar 对象数组为 series（按维度顺序提取数值）
            var seriesResult = new JsonArray();
            foreach (var radarItem in radarArr.OfType<JsonObject>())
            {
                var seriesName = radarItem["name"]?.GetValue<String>() ?? "数据";
                var dataArray = new JsonArray();
                
                foreach (var dimName in dimensionNames)
                {
                    if (radarItem[dimName] is JsonNode valNode)
                    {
                        dataArray.Add(valNode.DeepClone());
                    }
                    else
                    {
                        dataArray.Add(JsonValue.Create(0));
                    }
                }
                
                seriesResult.Add(new JsonObject
                {
                    ["name"] = JsonValue.Create(seriesName),
                    ["data"] = dataArray,
                });
            }

            var result = new JsonObject();
            foreach (var kvp in obj)
            {
                if (kvp.Key != "radar" && kvp.Key != "indicator")
                {
                    result[kvp.Key] = kvp.Value?.DeepClone();
                }
            }
            result["series"] = seriesResult;
            result["indicators"] = indicatorsResult;
            return result;
        }

        // 【适配2】若 LLM 输出 datasets + categories 多系列格式，转换为规范格式
        if (obj["series"] == null && obj["datasets"] is JsonArray datasets)
        {
            return NormalizeMultiSeriesFormat(type, obj, datasets);
        }

        // 【适配3】LLM 输出 dataset（单数）+ series 格式：dataset[].name 作为雷达维度名
        // 此格式 series 已存在但缺少 indicators，导致 ECharts 内部 push 报错
        if (type == "radar" && obj["dataset"] is JsonArray datasetArr && obj["indicators"] == null)
        {
            var indicatorsArr = new JsonArray();
            foreach (var item in datasetArr)
            {
                if (item is JsonObject dimObj && dimObj["name"] is JsonNode nameNode)
                {
                    var indObj = new JsonObject { ["name"] = nameNode.DeepClone() };
                    if (dimObj["max"] is JsonNode maxNode) indObj["max"] = maxNode.DeepClone();
                    indicatorsArr.Add(indObj);
                }
            }
            if (indicatorsArr.Count > 0)
            {
                var result = new JsonObject();
                foreach (var kvp in obj)
                {
                    result[kvp.Key] = kvp.Value?.DeepClone();
                }
                result["indicators"] = indicatorsArr;
                return result;
            }
        }

        // 【适配4】LLM 输出 labels + series[].value 格式（labels 作为雷达维度，value 字段代替 data）
        // 例：{labels:["租金回报",...], series:[{name:"北京", value:[50,85,...]}, ...]}
        if (type == "radar" && obj["labels"] is JsonArray labelsArr && obj["indicators"] == null)
        {
            var result = new JsonObject();
            // labels → indicators
            var indArr = new JsonArray();
            foreach (var label in labelsArr)
                indArr.Add(new JsonObject { ["name"] = label?.DeepClone() ?? JsonValue.Create("") });
            result["indicators"] = indArr;
            // series[].value → series[].data
            if (obj["series"] is JsonArray srcSeries)
            {
                var normSeries = new JsonArray();
                foreach (var s in srcSeries)
                {
                    if (s is JsonObject sObj)
                    {
                        var ns = new JsonObject();
                        foreach (var kvp in sObj)
                        {
                            ns[kvp.Key == "value" ? "data" : kvp.Key] = kvp.Value?.DeepClone();
                        }
                        normSeries.Add(ns);
                    }
                    else
                    {
                        normSeries.Add(s?.DeepClone());
                    }
                }
                result["series"] = normSeries;
            }
            // 透传其他字段
            foreach (var kvp in obj)
            {
                if (kvp.Key != "labels" && kvp.Key != "series")
                    result[kvp.Key] = kvp.Value?.DeepClone();
            }
            return result;
        }

        // 【适配5】饼图/漏斗图"扁平"格式：每个 series 项是 {data: number, name: string}
        // 输入：{ series: [{data: 45, name: "编码开发"}, {data: 15, name: "代码审查"}, ...] }
        // 输出：{ series: [{data: [{name: "编码开发", value: 45}, ...]}] }
        if ((type == "pie" || type == "funnel") && obj["series"] is JsonArray flatSeries)
        {
            var isFlatFormat = flatSeries.Count > 0 && flatSeries.Cast<JsonObject?>().All(s =>
                s != null &&
                (s["data"] is JsonValue dv && (dv.GetValueKind() is System.Text.Json.JsonValueKind.Number or System.Text.Json.JsonValueKind.String) ||
                 s["data"] is JsonNode dn && (dn is not JsonArray and not JsonObject)) &&
                s["name"] is JsonNode);

            if (isFlatFormat)
            {
                var normalized = new JsonArray();
                foreach (var s in flatSeries.OfType<JsonObject>())
                {
                    // 数值转换：支持 number 和 string 类型
                    Double numValue = 0;
                    if (s["data"] is JsonValue dv)
                    {
                        if (dv.GetValueKind() == System.Text.Json.JsonValueKind.Number)
                            numValue = (Double?)dv ?? 0;
                        else if (dv.GetValueKind() == System.Text.Json.JsonValueKind.String)
                            Double.TryParse(dv.GetValue<String>(), out numValue);
                    }

                    var item = new JsonObject
                    {
                        ["name"] = s["name"]!.DeepClone(),
                        ["value"] = numValue,
                    };
                    normalized.Add(item);
                }

                var result = new JsonObject();
                foreach (var kvp in obj)
                {
                    if (kvp.Key != "series")
                    {
                        result[kvp.Key] = kvp.Value?.DeepClone();
                    }
                }

                result["series"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = flatSeries[0] is JsonObject first ? (first["name"]?.DeepClone() ?? JsonValue.Create("数据")) : JsonValue.Create("数据"),
                        ["data"] = normalized,
                    }
                };

                return result;
            }
        }

        // 【适配5.1】饼图/漏斗图多系列合并：LLM 将各类别名错误地作为 series[].name，且每个 series.data 是相同的完整数值数组
        // 输入：{ series: [{data: [25,30,15,15,10], name: "代码编写"}, {data: [25,30,15,15,10], name: "代码审查"}, ...] }
        // 输出：{ series: [{name: "数据", data: [{name: "代码编写", value: 25}, {name: "代码审查", value: 30}, ...]}] }
        if ((type == "pie" || type == "funnel" || type == "treemap") && obj["series"] is JsonArray dupSeries && dupSeries.Count > 1 && obj["xAxis"] == null)
        {
            // 检查是否所有 series 的 data 都是等长数值数组
            var allHaveName = true;
            var allDataAreNumericArrays = true;
            var dataLen = -1;
            foreach (var s in dupSeries.OfType<JsonObject>())
            {
                if (s["name"] == null) { allHaveName = false; break; }
                if (s["data"] is not JsonArray arr) { allDataAreNumericArrays = false; break; }
                if (dataLen < 0) dataLen = arr.Count;
                else if (arr.Count != dataLen) { allDataAreNumericArrays = false; break; }
                foreach (var item in arr)
                {
                    if (item is JsonValue jv && jv.GetValueKind() == System.Text.Json.JsonValueKind.Number) continue;
                    allDataAreNumericArrays = false;
                    break;
                }
                if (!allDataAreNumericArrays) break;
            }

            // series 数量 == 每个 data 数组长度时，取 series[i].data[i] 作为 value
            if (allHaveName && allDataAreNumericArrays && dupSeries.Count == dataLen)
            {
                var merged = new JsonArray();
                for (var i = 0; i < dupSeries.Count; i++)
                {
                    if (dupSeries[i] is JsonObject si && si["data"] is JsonArray arr && si["name"] is JsonNode nameNode)
                    {
                        var value = arr[i] is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.Number
                            ? (Double?)v ?? 0
                            : 0;
                        merged.Add(new JsonObject
                        {
                            ["name"] = nameNode.DeepClone(),
                            ["value"] = value,
                        });
                    }
                }

                var mergedResult = new JsonObject();
                foreach (var kvp in obj)
                {
                    if (kvp.Key != "series")
                        mergedResult[kvp.Key] = kvp.Value?.DeepClone();
                }
                mergedResult["series"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = JsonValue.Create(title),
                        ["data"] = merged,
                    }
                };
                return mergedResult;
            }
        }

        // 【适配5.2】LLM 输出 categories + maxValues + series 格式（radar 图常见 LLM 生成格式）
        // 输入：{"categories":["维度1","维度2"],"maxValues":[100,50],"series":[{name:"A",data:[80,70]}]}
        // 输出：添加 indicators（categories→name，maxValues→max），移除 categories/maxValues
        if (type == "radar" && obj["categories"] is JsonArray categoriesArr && obj["indicators"] == null)
        {
            var maxValues = obj["maxValues"] as JsonArray;
            var indFromCat = new JsonArray();
            for (var i = 0; i < categoriesArr.Count; i++)
            {
                var indObj = new JsonObject { ["name"] = categoriesArr[i]?.DeepClone() ?? JsonValue.Create("") };
                if (maxValues != null && i < maxValues.Count)
                {
                    if (maxValues[i] is JsonValue mv && mv.GetValueKind() == System.Text.Json.JsonValueKind.Number)
                        indObj["max"] = mv.DeepClone();
                }
                indFromCat.Add(indObj);
            }
            if (indFromCat.Count > 0)
            {
                obj.Remove("categories");
                obj.Remove("maxValues");
                obj["indicators"] = indFromCat;
            }
        }

        // 【适配6】LLM 输出 dimensions[] + series[].values 简化格式（比 indicators/data 更直觉）
        // 输入：{"dimensions":["速度","力量"],"series":[{"name":"A","values":[80,90]}]}
        // 输出：{"indicators":[{"name":"速度"},{"name":"力量"}],"series":[{"name":"A","data":[80,90]}]}
        if (type == "radar" && obj["dimensions"] is JsonArray dimensionsArr && obj["indicators"] == null)
        {
            var indFromDim = new JsonArray();
            foreach (var dim in dimensionsArr)
            {
                if (dim is JsonValue dimVal && dimVal.GetValueKind() == System.Text.Json.JsonValueKind.String)
                    indFromDim.Add(new JsonObject { ["name"] = JsonValue.Create(dimVal.GetValue<String>()) });
                else if (dim is JsonObject)
                    indFromDim.Add(dim.DeepClone());
            }
            if (indFromDim.Count > 0)
            {
                obj.Remove("dimensions");
                obj["indicators"] = indFromDim;
            }
        }

        // series 已存在时进行后处理，修复常见 LLM 格式错误后返回
        if (obj["series"] is JsonArray existingSeries)
        {
            // 【后处理A】将 series[i].value[] 或 series[i].values[] 重命名为 data（通用修复）
            PostProcessSeriesValueToData(existingSeries);

            if (type == "radar")
            {
                // 【后处理B】indicators 缺失时尝试从 series 对象属性推断维度（属性提取式兜底）
                // 例：series:[{name:"北京", 速度:80, 力量:90}] → indicators + data 数组
                if (obj["indicators"] == null)
                    TryExtractRadarIndicatorsFromAttrs(obj, existingSeries);

                // 【后处理C】indicators/data 长度不匹配时截断或补零，防止 ECharts 崩溃
                if (obj["indicators"] is JsonArray existingInd)
                    AlignRadarDataLength(existingSeries, existingInd.Count);
            }

            return obj;
        }

        if (obj["data"] is JsonArray directData)
        {
            return type switch
            {
                "pie" or "funnel" or "treemap" or "gauge" => WrapSeries(
                    obj["name"]?.GetValue<String>() ?? title,
                    directData,
                    obj,
                    "name",
                    "data"),
                _ => dataNode,
            };
        }

        if (type == "gauge" && obj["value"] != null)
        {
            var item = new JsonObject();
            if (obj["name"] != null) item["name"] = obj["name"]!.DeepClone();
            item["value"] = obj["value"]!.DeepClone();
            var gaugeData = new JsonArray { item };

            return WrapSeries(obj["name"]?.GetValue<String>() ?? title, gaugeData);
        }

        return dataNode;
    }

    /// <summary>
    /// 将 LLM 输出的 datasets + categories 多系列格式转换为标准格式
    /// 
    /// 输入格式（LLM 偏好）：
    /// {
    ///   "datasets": [
    ///     { "name": "北京", "data": [9, 8.5, 7, 1.8], "color": "#E74C3C" },
    ///     { "name": "上海", "data": [8.5, 9, 8, 2.1], "color": "#3498DB" }
    ///   ],
    ///   "categories": ["保值能力", "升值潜力", "性价比", "租金回报"]
    /// }
    /// 
    /// 输出格式（ECharts 标准）：
    /// 雷达图：{ "series": [...], "indicators": [...] }
    /// 其他图表：{ "series": [...], "xAxis": [...], "colors": [...] }
    /// </summary>
    private static JsonNode NormalizeMultiSeriesFormat(String type, JsonObject obj, JsonArray datasets)
    {
        var result = new JsonObject();

        // 复制非 datasets/categories 的其他字段
        foreach (var kvp in obj)
        {
            if (kvp.Key is not ("datasets" or "categories"))
            {
                result[kvp.Key] = kvp.Value?.DeepClone();
            }
        }

        // 转换 datasets → series
        var seriesArray = new JsonArray();
        var colors      = new JsonArray();

        foreach (var ds in datasets.OfType<JsonObject>())
        {
            var seriesItem = new JsonObject();

            if (ds["name"] != null)
                seriesItem["name"] = ds["name"]!.DeepClone();

            if (ds["data"] != null)
                seriesItem["data"] = ds["data"]!.DeepClone();

            // 其他字段直透（如 smooth, area, stack 等）
            foreach (var kvp in ds)
            {
                if (kvp.Key is not ("name" or "data" or "color"))
                {
                    seriesItem[kvp.Key] = kvp.Value?.DeepClone();
                }
            }

            seriesArray.Add(seriesItem);

            // 收集 color 用于统一 colors 数组
            if (ds["color"] != null)
                colors.Add(ds["color"]!.DeepClone());
        }

        result["series"] = seriesArray;

        // 转换 categories → indicators（雷达图）或 xAxis（其他图表）
        if (obj["categories"] is JsonArray categories)
        {
            if (type == "radar")
            {
                // 雷达图维度定义
                var indicators = new JsonArray();
                foreach (var cat in categories)
                {
                    var indicator = new JsonObject();

                    if (cat is JsonObject catObj && catObj["name"] != null)
                    {
                        // 已是 {name, max} 格式
                        indicator = catObj.DeepClone() as JsonObject ?? [];
                    }
                    else
                    {
                        // 纯字符串 → 转为 {name}
                        indicator["name"] = cat?.DeepClone();
                    }

                    indicators.Add(indicator);
                }
                result["indicators"] = indicators;
            }
            else
            {
                // 其他图表的类目轴
                result["xAxis"] = categories.DeepClone();
            }
        }

        // 若存在 color 数据，整合为 colors 数组
        if (colors.Count > 0)
            result["colors"] = colors;

        return result;
    }

    /// <summary>将 series[i].value[] 或 series[i].values[] 统一重命名为 data（通用后处理修复）</summary>
    /// <param name="series">series 数组（原地修改）</param>
    private static void PostProcessSeriesValueToData(JsonArray series)
    {
        foreach (var s in series.OfType<JsonObject>())
        {
            if (s["data"] != null) continue;
            if (s["value"] is JsonArray valArr)
            {
                s["data"] = valArr.DeepClone();
                s.Remove("value");
            }
            else if (s["values"] is JsonArray valsArr)
            {
                s["data"] = valsArr.DeepClone();
                s.Remove("values");
            }
        }
    }

    // 雷达图属性提取时排除的保留字段名
    private static readonly HashSet<String> _seriesReservedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "name", "data", "value", "values", "color", "smooth", "area", "stack",
        "type", "areaStyle", "lineStyle", "label", "symbol", "symbolSize",
        "itemStyle", "emphasis", "selected", "z", "id",
    };

    /// <summary>
    /// 雷达图属性提取式兜底：series 项含有数值属性（非保留字）时，自动推断维度并重组数据
    /// </summary>
    /// <param name="obj">图表数据对象（原地修改，添加 indicators 字段）</param>
    /// <param name="series">series 数组（每项的维度属性将被移入 data 数组）</param>
    private static void TryExtractRadarIndicatorsFromAttrs(JsonObject obj, JsonArray series)
    {
        if (series.Count == 0) return;
        if (series[0] is not JsonObject firstSeries) return;

        // 从第一个 series 项收集维度名（数值类型且非保留字）
        var dimNames = new List<String>();
        foreach (var kvp in firstSeries)
        {
            if (_seriesReservedKeys.Contains(kvp.Key)) continue;
            if (kvp.Value is JsonValue jv && jv.GetValueKind() == System.Text.Json.JsonValueKind.Number)
                dimNames.Add(kvp.Key);
        }
        if (dimNames.Count < 2) return;  // 至少 2 个维度才认定为属性式格式

        // 构建 indicators
        var indicators = new JsonArray();
        foreach (var dim in dimNames)
            indicators.Add(new JsonObject { ["name"] = JsonValue.Create(dim) });
        obj["indicators"] = indicators;

        // 每个 series 项：属性值 → data 数组，并移除属性字段
        foreach (var s in series.OfType<JsonObject>())
        {
            if (s["data"] != null) continue;  // 已有 data，跳过
            var dataArr = new JsonArray();
            foreach (var dim in dimNames)
            {
                dataArr.Add(s[dim] is JsonNode valNode ? valNode.DeepClone() : JsonValue.Create(0));
                s.Remove(dim);
            }
            s["data"] = dataArr;
        }
    }

    /// <summary>雷达图 data 长度对齐：过短补零，过长截断，确保与 indicators 数量一致</summary>
    /// <param name="series">series 数组（原地修改每项的 data）</param>
    /// <param name="expectedLen">期望长度（等于 indicators.Count）</param>
    private static void AlignRadarDataLength(JsonArray series, Int32 expectedLen)
    {
        if (expectedLen <= 0) return;
        foreach (var s in series.OfType<JsonObject>())
        {
            if (s["data"] is not JsonArray data) continue;
            while (data.Count < expectedLen)
                data.Add(JsonValue.Create(0));
            while (data.Count > expectedLen)
                data.RemoveAt(data.Count - 1);
        }
    }

    private static JsonObject WrapSeries(String seriesName, JsonArray data, JsonObject? source = null, params String[] removeKeys)
    {
        var result = source?.DeepClone() as JsonObject ?? [];
        foreach (var item in removeKeys)
        {
            result.Remove(item);
        }

        result["series"] = new JsonArray
        {
            new JsonObject
            {
                ["name"] = JsonValue.Create(seriesName),
                ["data"] = data.DeepClone(),
            }
        };

        return result;
    }

    #endregion
}
