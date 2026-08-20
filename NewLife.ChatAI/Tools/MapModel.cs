using System.ComponentModel;

namespace NewLife.ChatAI.Tools;

/// <summary>地图标注点输入（LLM 提供）。供 <see cref="MapAnnotationToolService"/> 的类型化参数使用，避免 LLM 手工转义 JSON 字符串</summary>
public class MapMarker
{
    /// <summary>城市/点位名称（必填），如 "北京"</summary>
    [Description("城市/点位名称（必填），如 \"北京\"")]
    public String Name { get; set; } = String.Empty;

    /// <summary>纬度（必填），WGS84 十进制度</summary>
    [Description("纬度（必填），WGS84 十进制度")]
    public Double Lat { get; set; }

    /// <summary>经度（必填），WGS84 十进制度</summary>
    [Description("经度（必填），WGS84 十进制度")]
    public Double Lng { get; set; }

    /// <summary>标注标签文字（可选），支持 \n 换行</summary>
    [Description("标注标签文字（可选），支持 \\n 换行")]
    public String? Label { get; set; }

    /// <summary>标注颜色（可选），十六进制如 "#E74C3C"；不填自动按配色方案轮转</summary>
    [Description("标注颜色（可选），十六进制如 \"#E74C3C\"；不填自动按配色方案轮转")]
    public String? Color { get; set; }

    /// <summary>标注尺寸（可选），默认 7</summary>
    [Description("标注尺寸（可选），默认 7")]
    public Int32? Size { get; set; }
}

/// <summary>地图省份高亮输入（LLM 提供）</summary>
public class MapHighlight
{
    /// <summary>省份名称（必填），支持全称或简称，如 "广东省" 或 "广东"</summary>
    [Description("省份名称（必填），支持全称或简称，如 \"广东省\" 或 \"广东\"")]
    public String Name { get; set; } = String.Empty;

    /// <summary>高亮颜色（可选），十六进制如 "#FFB347"；不填使用默认色</summary>
    [Description("高亮颜色（可选），十六进制如 \"#FFB347\"；不填使用默认色")]
    public String? Color { get; set; }

    /// <summary>高亮标签（可选），如 "GDP第一"</summary>
    [Description("高亮标签（可选），如 \"GDP第一\"")]
    public String? Label { get; set; }
}
