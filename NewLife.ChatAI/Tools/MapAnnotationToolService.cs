using System.ComponentModel;
using System.Globalization;
using System.Text;
using NewLife.AI.Tools;
using NewLife.Log;
using NewLife.Serialization;

namespace NewLife.ChatAI.Tools;

/// <summary>中国地图标注工具服务。基于内置 SVG 底图在省份高亮或坐标点标注后渲染到对话气泡</summary>
/// <remarks>
/// <para>底图：程序集嵌入资源 china-map.svg（来源：mapsvg.com geo-calibrated，147KB）</para>
/// <para>坐标系：Web Mercator 投影，mapsvg:geoViewBox 属性存储地理边界（西北东南四至）</para>
/// <para>省份 ID 格式：ISO 3166-2 数字码，如 CN-11（北京）、CN-44（广东）</para>
/// <para>返回格式与 show_widget 兼容，前端 WidgetBlock 直接渲染，无需新增 SSE 事件类型</para>
/// </remarks>
/// <param name="log">日志</param>
public class MapAnnotationToolService(ILog log)
{
    #region 常量 / 静态数据

    /// <summary>省份中文名称到 SVG path id 的映射（ISO 3166-2 数字码格式）</summary>
    private static readonly Dictionary<String, String> ProvinceIdMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // 直辖市
        { "北京",  "CN-11" }, { "北京市",  "CN-11" },
        { "天津",  "CN-12" }, { "天津市",  "CN-12" },
        { "上海",  "CN-31" }, { "上海市",  "CN-31" },
        { "重庆",  "CN-50" }, { "重庆市",  "CN-50" },
        // 华北
        { "河北",  "CN-13" }, { "河北省",  "CN-13" },
        { "山西",  "CN-14" }, { "山西省",  "CN-14" },
        { "内蒙古","CN-15" }, { "内蒙古自治区","CN-15" },
        // 东北
        { "辽宁",  "CN-21" }, { "辽宁省",  "CN-21" },
        { "吉林",  "CN-22" }, { "吉林省",  "CN-22" },
        { "黑龙江","CN-23" }, { "黑龙江省","CN-23" },
        // 华东
        { "江苏",  "CN-32" }, { "江苏省",  "CN-32" },
        { "浙江",  "CN-33" }, { "浙江省",  "CN-33" },
        { "安徽",  "CN-34" }, { "安徽省",  "CN-34" },
        { "福建",  "CN-35" }, { "福建省",  "CN-35" },
        { "江西",  "CN-36" }, { "江西省",  "CN-36" },
        { "山东",  "CN-37" }, { "山东省",  "CN-37" },
        // 华中
        { "河南",  "CN-41" }, { "河南省",  "CN-41" },
        { "湖北",  "CN-42" }, { "湖北省",  "CN-42" },
        { "湖南",  "CN-43" }, { "湖南省",  "CN-43" },
        // 华南
        { "广东",  "CN-44" }, { "广东省",  "CN-44" },
        { "广西",  "CN-45" }, { "广西壮族自治区","CN-45" }, { "广西自治区","CN-45" },
        { "海南",  "CN-46" }, { "海南省",  "CN-46" },
        // 西南
        { "四川",  "CN-51" }, { "四川省",  "CN-51" },
        { "贵州",  "CN-52" }, { "贵州省",  "CN-52" },
        { "云南",  "CN-53" }, { "云南省",  "CN-53" },
        { "西藏",  "CN-54" }, { "西藏自治区","CN-54" },
        // 西北
        { "陕西",  "CN-61" }, { "陕西省",  "CN-61" },
        { "甘肃",  "CN-62" }, { "甘肃省",  "CN-62" },
        { "青海",  "CN-63" }, { "青海省",  "CN-63" },
        { "宁夏",  "CN-64" }, { "宁夏回族自治区","CN-64" }, { "宁夏自治区","CN-64" },
        { "新疆",  "CN-65" }, { "新疆维吾尔自治区","CN-65" }, { "新疆自治区","CN-65" },
        // 特别行政区 / 台湾
        { "台湾",  "CN-71" }, { "台湾省",  "CN-71" },
        { "香港",  "CN-91" }, { "香港特别行政区","CN-91" }, { "香港特区","CN-91" },
        { "澳门",  "CN-92" }, { "澳门特别行政区","CN-92" }, { "澳门特区","CN-92" },
    };

    /// <summary>默认标注点颜色序列（多点时循环使用）</summary>
    private static readonly String[] DefaultMarkerColors =
    [
        "#E74C3C", "#3498DB", "#2ECC71", "#F39C12", "#9B59B6",
        "#1ABC9C", "#E67E22", "#34495E", "#C0392B", "#2980B9",
    ];

    /// <summary>配色方案预设：方案名 → (省份底色, 省份悬停色, 标注点默认色组, 高亮默认色, 边界色)</summary>
    private static readonly Dictionary<String, (String Fill, String Hover, String[] Markers, String Highlight, String Border)> ColorSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["blue"]   = ("#D9E8F5", "#B0CFEA", new[] { "#3498DB", "#2980B9", "#1ABC9C", "#2ECC71", "#5470c6" }, "#87CEEB", "#FFFFFF"),
        ["warm"]   = ("#FFF0E0", "#FFD4B8", new[] { "#F39C12", "#E67E22", "#E74C3C", "#FC8452", "#FAC858" }, "#FFB347", "#FFFFFF"),
        ["green"]  = ("#E8F5E9", "#C8E6C9", new[] { "#2ECC71", "#27AE60", "#1ABC9C", "#3BA272", "#91CC75" }, "#90EE90", "#FFFFFF"),
        ["purple"] = ("#F3E5F5", "#E1BEE7", new[] { "#9B59B6", "#8E44AD", "#E74C3C", "#3498DB", "#EA7CCC" }, "#DDA0DD", "#FFFFFF"),
        ["red"]    = ("#FFEBEE", "#FFCDD2", new[] { "#E74C3C", "#C0392B", "#F39C12", "#E67E22", "#EE6666" }, "#FF6B6B", "#FFFFFF"),
    };

    /// <summary>解析配色方案，返回生效的颜色配置。若 colorScheme 为空则使用默认蓝色系</summary>
    private static (String Fill, String Hover, String[] Markers, String Highlight, String Border) ResolveColorScheme(String? colorScheme)
    {
        if (!colorScheme.IsNullOrEmpty() && ColorSchemes.TryGetValue(colorScheme, out var scheme))
            return scheme;
        return ColorSchemes["blue"];
    }

    /// <summary>底图 SVG 嵌入资源名称（以此后缀在程序集资源列表中匹配）</summary>
    private const String MapResourceSuffix = "china-map.svg";

    /// <summary>底图 SVG 内容缓存（首次读取后常驻内存，避免重复解压）</summary>
    private static String? _cachedBaseSvg;

    #endregion

    #region 工具方法

    /// <summary>在中国地图底图上标注坐标点或高亮省份，生成可渲染的 SVG 并显示在对话气泡中</summary>
    /// <param name="title">地图标题（≤ 30 字），显示在卡片头部</param>
    /// <param name="markers">
    /// 坐标点标注，JSON 数组，可为空。
    /// 每项格式：<c>{"name":"北京","lat":39.9,"lng":116.4,"label":"北京\n43760亿","color":"#E74C3C","size":8}</c>。
    /// <c>lat</c>/<c>lng</c> 为 WGS84 十进制度；<c>label</c> 支持 \n 换行；<c>color</c> 和 <c>size</c> 可省略。
    /// </param>
    /// <param name="highlightProvinces">
    /// 省份高亮，JSON 数组，可为空。
    /// 每项格式：<c>{"name":"广东省","color":"#FFB347","label":"GDP第一"}</c>。
    /// <c>name</c> 支持中文省份全称或简称（如"广东"或"广东省"）；<c>label</c> 可省略。
    /// </param>
    /// <param name="legendTitle">图例标题（可选），如"GDP规模"、"销售分布"</param>
    /// <param name="colorScheme">配色方案（可选），支持 blue/orange/green/purple/red</param>
    /// <param name="markerStyle">标注点样式（可选），如 circle/pin/square</param>
    /// <param name="context">工具调用上下文（框架自动注入）</param>
    /// <returns>ToolResult（ForUser=完整Map JSON + ForLlm=简短确认）</returns>
    [ToolDescription("show_china_map", IsSystem = false,
        Triggers = "地图,标注,城市分布,数据地图,各省,全国分布,地理分布",
        AssistantTriggers = "地图,标注,省份,城市分布,地理分布,数据地图,热力,分布图,定位,各省,全国分布",
        ReadOnly = true)]
    [DisplayName("中国地图标注")]
    [Description("在中国地图上标注坐标点（城市/POI）或高亮省份区域，生成带标注的 SVG 地图并渲染到对话气泡。支持：①自定义颜色的省份填充高亮；②WGS84 经纬度坐标标注点（含标签文字）；③右下角自动图例。典型用途：各省数据分布、城市位置展示、区域销售热力图。")]
    public ToolResult AnnotateChinaMap(
        [Description("地图标题（≤ 30 字），如「2024年各省GDP分布」")] String title,
        [Description(@"坐标点标注 JSON 数组（可为空数组 []）。格式：[{""name"":""北京"",""lat"":39.9,""lng"":116.4,""label"":""北京\n43760亿"",""color"":""#E74C3C"",""size"":8}]。lat/lng 为 WGS84 十进制度，color/size 可省略")] String markers,
        [Description(@"省份高亮 JSON 数组（可为空数组 []）。格式：[{""name"":""广东省"",""color"":""#FFB347"",""label"":""GDP第一""}]。name 支持中文省份全称或简称，label 可省略")] String? highlightProvinces = null,
        [Description("图例标题（可选），如「GDP规模」")] String? legendTitle = null,
        [Description("配色方案（可选），支持 blue/orange/green/purple/red，默认 blue")] String? colorScheme = null,
        [Description("标注点样式（可选），如 circle/pin/square，默认 circle")] String? markerStyle = null,
        ToolCallContext? context = null)
    {
        // 加载底图（首次从程序集嵌入资源读取，之后使用静态缓存）
        var baseSvg = _cachedBaseSvg ??= LoadBaseSvg();

        // 解析地理校准参数（mapsvg:geoViewBox="minLng maxLat maxLng minLat"）
        var calibration = ParseGeoViewBox(baseSvg);

        // 解析入参
        var schemeColors = ResolveColorScheme(colorScheme);
        List<MarkerItem> markerList;
        List<HighlightItem> highlightList;
        try
        {
            markerList = ParseMarkers(markers, schemeColors.Markers);
            highlightList = ParseHighlights(highlightProvinces, schemeColors.Highlight);
        }
        catch (ArgumentException ex)
        {
            throw new ToolException($"参数错误：{ex.Message}", "请检查 JSON 格式后重试，或直接回复用户说明无法生成地图标注。", ex);
        }

        // 组装带标注的 SVG
        var annotatedSvg = BuildAnnotatedSvg(baseSvg, calibration, markerList, highlightList, legendTitle, schemeColors, markerStyle);

        var widgetId = context?.ToolCallId;
        if (widgetId.IsNullOrEmpty()) widgetId = $"map_{Guid.NewGuid():N}";

        log.Info("[Map] 生成地图标注：{0}，{1} 个标注点，{2} 个高亮省份，{3} 字节",
            title, markerList.Count, highlightList.Count,
            Encoding.UTF8.GetByteCount(annotatedSvg));

        var resultJson = new { widgetId, kind = "svg", title, code = annotatedSvg }.ToJson();
        return new ToolResult(
            ToolContent.ForUser(resultJson),
            ToolContent.ForLlm($"[已渲染中国地图到客户端：{title}]"));
    }

    #endregion

    #region 辅助：加载底图

    /// <summary>从程序集嵌入资源中读取底图 SVG 内容</summary>
    private static String LoadBaseSvg()
    {
        var assembly = typeof(MapAnnotationToolService).Assembly;
        var resourceName = Array.Find(
            assembly.GetManifestResourceNames(),
            n => n.EndsWith(MapResourceSuffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
            throw new InvalidOperationException(
                "嵌入资源 china-map.svg 未找到，请确认 csproj 中包含 <EmbeddedResource Include=\"Resources\\china-map.svg\" />");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    #endregion

    #region 辅助：解析参数

    private static GeoCalibration ParseGeoViewBox(String svgContent)
    {
        // 匹配 mapsvg:geoViewBox="73.554302 53.561780 134.775703 18.155060"
        var m = System.Text.RegularExpressions.Regex.Match(svgContent,
            @"geoViewBox=""([0-9.\-]+)\s+([0-9.\-]+)\s+([0-9.\-]+)\s+([0-9.\-]+)""");
        if (!m.Success) throw new InvalidOperationException("底图缺少 geoViewBox 校准属性");

        var minLng = Double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var maxLat = Double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        var maxLng = Double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        var minLat = Double.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);

        // 解析 SVG 像素尺寸
        var wm = System.Text.RegularExpressions.Regex.Match(svgContent, @"width=""([0-9.]+)""");
        var hm = System.Text.RegularExpressions.Regex.Match(svgContent, @"height=""([0-9.]+)""");
        var svgW = wm.Success ? Double.Parse(wm.Groups[1].Value, CultureInfo.InvariantCulture) : 774.0;
        var svgH = hm.Success ? Double.Parse(hm.Groups[1].Value, CultureInfo.InvariantCulture) : 570.0;

        return new GeoCalibration(minLng, maxLng, minLat, maxLat, svgW, svgH);
    }

    private static List<MarkerItem> ParseMarkers(String json, String[]? schemeMarkerColors = null)
    {
        if (json.IsNullOrWhiteSpace() || json.Trim() == "[]") return [];

        var list = new List<MarkerItem>();
        var fallbackColors = schemeMarkerColors ?? DefaultMarkerColors;
        try
        {
            var arr = json.ToJsonEntity<List<Dictionary<String, Object>>>();
            if (arr == null) return list;

            var colorIdx = 0;
            foreach (var item in arr)
            {
                var lat = GetDouble(item, "lat");
                var lng = GetDouble(item, "lng");
                if (lat == 0 && lng == 0) continue;   // 无效坐标跳过

                list.Add(new MarkerItem(
                    Name:   GetStr(item, "name"),
                    Lat:    lat,
                    Lng:    lng,
                    Label:  GetStr(item, "label"),
                    Color:  (String.IsNullOrEmpty(GetStr(item, "color")) ? fallbackColors[colorIdx % fallbackColors.Length] : GetStr(item, "color")),
                    Size:   (Int32)GetDouble(item, "size", 7)
                ));
                colorIdx++;
            }
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"markers 参数 JSON 解析失败：{ex.Message}", nameof(json));
        }
        return list;
    }

    private static List<HighlightItem> ParseHighlights(String? json, String defaultColor = "#87CEEB")
    {
        if (json.IsNullOrWhiteSpace() || json!.Trim() == "[]") return [];

        var list = new List<HighlightItem>();
        try
        {
            var arr = json.ToJsonEntity<List<Dictionary<String, Object>>>();
            if (arr == null) return list;

            foreach (var item in arr)
            {
                var name = GetStr(item, "name");
                if (name.IsNullOrEmpty()) continue;

                // 查找省份 SVG path id
                if (!ProvinceIdMap.TryGetValue(name, out var pathId)) continue;

                list.Add(new HighlightItem(
                    ProvinceName: name,
                    PathId:       pathId,
                    Color:        (String.IsNullOrEmpty(GetStr(item, "color")) ? defaultColor : GetStr(item, "color")),
                    Label:        GetStr(item, "label")
                ));
            }
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"highlightProvinces 参数 JSON 解析失败：{ex.Message}", nameof(json));
        }
        return list;
    }

    private static String GetStr(Dictionary<String, Object> d, String key)
        => d.TryGetValue(key, out var v) ? v?.ToString() ?? "" : "";

    private static Double GetDouble(Dictionary<String, Object> d, String key, Double def = 0)
    {
        if (!d.TryGetValue(key, out var v)) return def;
        return v switch
        {
            Double dbl  => dbl,
            Int64  lng  => (Double)lng,
            String s    => Double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : def,
            System.Text.Json.JsonElement je => je.ValueKind == System.Text.Json.JsonValueKind.Number
                ? je.GetDouble() : def,
            _ => def,
        };
    }

    #endregion

    #region 辅助：SVG 组装

    private static String BuildAnnotatedSvg(
        String baseSvg,
        GeoCalibration cal,
        List<MarkerItem> markers,
        List<HighlightItem> highlights,
        String? legendTitle,
        (String Fill, String Hover, String[] Markers, String Highlight, String Border) schemeColors,
        String? markerStyle = null)
    {
        // 查找 SVG 根标签结束位置（">"），在其后插入 <style>
        var svgTagEnd = baseSvg.IndexOf('>');
        if (svgTagEnd < 0) throw new InvalidOperationException("SVG 根标签解析失败");

        var sb = new StringBuilder(baseSvg.Length + 8192);
        sb.Append(baseSvg, 0, svgTagEnd + 1);

        // ── 注入样式 ─────────────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine("<style>");
        // 省份默认样式（使用配色方案的填充色和边界色）
        sb.AppendLine($"  path {{ fill: {schemeColors.Fill}; stroke: {schemeColors.Border}; stroke-width: 0.8; transition: fill 0.2s; }}");
        sb.AppendLine($"  path:hover {{ fill: {schemeColors.Hover}; }}");
        // 高亮省份（按 path id 覆盖）
        foreach (var h in highlights)
            sb.AppendLine($"  path#{h.PathId} {{ fill: {h.Color}; }}");
        // 标注点样式
        sb.AppendLine("  .map-marker circle, .map-marker path, .map-marker polygon, .map-marker rect { stroke: #FFFFFF; stroke-width: 1.5; opacity: 0.9; }");
        sb.AppendLine("  .map-marker text  { font-family: 'PingFang SC','Microsoft YaHei',sans-serif; font-size: 11px; fill: #2C3E50; paint-order: stroke; stroke: #FFFFFF; stroke-width: 3; }");
        sb.AppendLine("  .map-label-main   { font-size: 11px; font-weight: bold; }");
        sb.AppendLine("  .map-label-sub    { font-size: 10px; fill: #555; }");
        sb.AppendLine("  .map-legend rect  { fill: rgba(255,255,255,0.88); rx: 6; ry: 6; stroke: #CCC; stroke-width: 0.8; }");
        sb.AppendLine("  .map-legend text  { font-family: 'PingFang SC','Microsoft YaHei',sans-serif; font-size: 11px; fill: #333; }");
        sb.AppendLine("</style>");

        // SVG 原始内容（省份路径部分）
        sb.Append(baseSvg, svgTagEnd + 1, baseSvg.Length - svgTagEnd - 1 - "</svg>".Length);

        // ── 注入标注点 ────────────────────────────────────────────
        if (markers.Count > 0)
        {
            sb.AppendLine("<g id=\"map-markers\">");
            foreach (var mk in markers)
            {
                var (px, py) = cal.ToPixel(mk.Lat, mk.Lng);
                sb.AppendLine($"  <g class=\"map-marker\" transform=\"translate({px:F1},{py:F1})\">");
                sb.AppendLine(GenerateMarkerShape(mk.Size, mk.Color, markerStyle));

                // 拆分 label 为多行
                var lines = (String.IsNullOrEmpty(mk.Label) ? mk.Name : mk.Label).Split('\n');
                var lineH = 13;
                var offsetY = mk.Size + 4 + lineH;
                sb.AppendLine("    <text text-anchor=\"middle\">");
                foreach (var (line, i) in lines.Select((l, i) => (l, i)))
                {
                    var cls = i == 0 ? "map-label-main" : "map-label-sub";
                    sb.AppendLine($"      <tspan class=\"{cls}\" x=\"0\" dy=\"{(i == 0 ? offsetY : lineH)}\">{EscapeXml(line)}</tspan>");
                }
                sb.AppendLine("    </text>");
                sb.AppendLine("  </g>");
            }
            sb.AppendLine("</g>");
        }

        // ── 注入图例 ──────────────────────────────────────────────
        var legendItems = BuildLegendItems(markers, highlights);
        if (legendItems.Count > 0)
        {
            var itemH = 18;
            var paddingX = 10;
            var paddingY = 8;
            var legendW = 140;
            var legendH = paddingY * 2 + (legendTitle.IsNullOrEmpty() ? 0 : itemH) + legendItems.Count * itemH;
            var lx = cal.SvgWidth - legendW - 10;
            var ly = cal.SvgHeight - legendH - 10;

            sb.AppendLine($"<g class=\"map-legend\" transform=\"translate({lx:F0},{ly:F0})\">");
            sb.AppendLine($"  <rect x=\"0\" y=\"0\" width=\"{legendW}\" height=\"{legendH}\" rx=\"6\" ry=\"6\" fill=\"rgba(255,255,255,0.88)\" stroke=\"#CCC\" stroke-width=\"0.8\"/>");
            var tyOffset = paddingY;
            if (!legendTitle.IsNullOrEmpty())
            {
                sb.AppendLine($"  <text x=\"{paddingX}\" y=\"{tyOffset + 12}\" font-weight=\"bold\" font-size=\"12\" font-family=\"'PingFang SC','Microsoft YaHei',sans-serif\" fill=\"#333\">{EscapeXml(legendTitle)}</text>");
                tyOffset += itemH;
            }
            foreach (var (color, label) in legendItems)
            {
                sb.AppendLine($"  <rect x=\"{paddingX}\" y=\"{tyOffset + 3}\" width=\"12\" height=\"12\" rx=\"2\" fill=\"{color}\"/>");
                sb.AppendLine($"  <text x=\"{paddingX + 17}\" y=\"{tyOffset + 13}\" font-size=\"11\" font-family=\"'PingFang SC','Microsoft YaHei',sans-serif\" fill=\"#555\">{EscapeXml(label)}</text>");
                tyOffset += itemH;
            }
            sb.AppendLine("</g>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>根据 markerStyle 生成对应的 SVG 形状元素</summary>
    /// <param name="size">标注点基础尺寸（半径/半边长）</param>
    /// <param name="color">填充颜色</param>
    /// <param name="style">样式：circle（默认）/ pin（图钉）/ diamond（菱形）/ square（方点）</param>
    private static String GenerateMarkerShape(Int32 size, String color, String? style)
    {
        var r = size;
        var d = r * 2;
        return style?.ToLower() switch
        {
            "pin"    => $"    <path d=\"M0,{-r * 2} C{-r},{-r * 2} {-r * 2},{-r} {-r * 2},0 C{-r * 2},{r} 0,{r * 2} 0,{r * 2} C0,{r * 2} {r * 2},{r} {r * 2},0 C{r * 2},{-r} {r},{-r * 2} 0,{-r * 2}Z\" fill=\"{color}\"/>",
            "diamond" => $"    <polygon points=\"0,{-r} {r},0 0,{r} {-r},0\" fill=\"{color}\"/>",
            "square"  => $"    <rect x=\"{-r}\" y=\"{-r}\" width=\"{d}\" height=\"{d}\" rx=\"1\" fill=\"{color}\"/>",
            _         => $"    <circle r=\"{r}\" fill=\"{color}\"/>",
        };
    }

    private static List<(String Color, String Label)> BuildLegendItems(
        List<MarkerItem> markers,
        List<HighlightItem> highlights)
    {
        var items = new List<(String, String)>();
        // 已收录的颜色，避免重复图例条目
        var seen = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

        foreach (var mk in markers)
        {
            if (seen.Add(mk.Color))
                items.Add((mk.Color, (String.IsNullOrEmpty(mk.Label) ? mk.Name : mk.Label).Split('\n')[0]));
        }
        foreach (var h in highlights)
        {
            if (seen.Add(h.Color))
                items.Add((h.Color, String.IsNullOrEmpty(h.Label) ? h.ProvinceName : h.Label));
        }
        return items;
    }

    private static String EscapeXml(String text)
        => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    #endregion

    #region 内部数据结构

    /// <summary>地理校准参数（Web Mercator 投影）</summary>
    private sealed record GeoCalibration(
        Double MinLng, Double MaxLng,
        Double MinLat, Double MaxLat,
        Double SvgWidth, Double SvgHeight)
    {
        // 预计算 Mercator Y 边界（提升性能，避免重复对数运算）
        private readonly Double _mercMinY = MercatorY(MinLat);
        private readonly Double _mercMaxY = MercatorY(MaxLat);

        /// <summary>Web Mercator Y 值：ln(tan(π/4 + lat×π/360))</summary>
        private static Double MercatorY(Double lat)
            => Math.Log(Math.Tan(Math.PI / 4.0 + lat * Math.PI / 360.0));

        /// <summary>将 WGS84 经纬度转换为 SVG 像素坐标</summary>
        public (Double X, Double Y) ToPixel(Double lat, Double lng)
        {
            var x = (lng - MinLng) / (MaxLng - MinLng) * SvgWidth;
            var y = (_mercMaxY - MercatorY(lat)) / (_mercMaxY - _mercMinY) * SvgHeight;
            return (x, y);
        }
    }

    private sealed record MarkerItem(String Name, Double Lat, Double Lng, String Label, String Color, Int32 Size);

    private sealed record HighlightItem(String ProvinceName, String PathId, String Color, String Label);

    #endregion

    #region 日志

    #endregion
}
