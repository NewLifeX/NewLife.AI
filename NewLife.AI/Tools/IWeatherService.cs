namespace NewLife.AI.Tools;

/// <summary>天气查询服务接口。支持多实现链式降级（中央气象台 → wttr.in → 远程兜底）</summary>
public interface IWeatherService
{
    /// <summary>获取指定城市的实时天气信息</summary>
    /// <param name="city">城市名称，支持中英文</param>
    /// <param name="unit">温度单位：C（摄氏度）或 F（华氏度）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功返回天气信息；失败或不可用返回 null</returns>
    Task<WeatherModel?> GetWeatherAsync(String city, String unit = "C", CancellationToken cancellationToken = default);
}

/// <summary>天气查询结果</summary>
public class WeatherModel
{
    /// <summary>城市名称</summary>
    public String? City { get; set; }

    /// <summary>省份</summary>
    public String? Province { get; set; }

    /// <summary>国家</summary>
    public String? Country { get; set; }

    /// <summary>天气描述，如"晴"、"多云"</summary>
    public String? Description { get; set; }

    /// <summary>温度（含单位），如"25°C"</summary>
    public String? Temp { get; set; }

    /// <summary>体感温度（含单位）</summary>
    public String? FeelsLike { get; set; }

    /// <summary>湿度，如"65%"</summary>
    public String? Humidity { get; set; }

    /// <summary>风力信息，如"东南风 3级（5.2 m/s）"</summary>
    public String? Wind { get; set; }

    /// <summary>降雨量，如"0 mm"</summary>
    public String? Rain { get; set; }

    /// <summary>能见度，如"10 km"</summary>
    public String? Visibility { get; set; }

    /// <summary>紫外线指数</summary>
    public String? UvIndex { get; set; }

    /// <summary>数据发布/观测时间</summary>
    public String? PublishTime { get; set; }

    /// <summary>气象预警信息，无预警时为 null</summary>
    public String? Warning { get; set; }
}
