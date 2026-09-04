using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Tools;
using Xunit;

namespace XUnitTest.Tools;

/// <summary>WeatherNmcService 单请求超时测试。省份扫描被个别慢端点拖死时按超时跳过该省，不阻塞整轮扫描
/// （回归：星尘调用链中 get_weather 首次扫描被单省 7.1s 拖死）</summary>
[DisplayName("WeatherNmcService请求超时测试")]
public class WeatherNmcServiceTimeoutTests
{
    /// <summary>可编程 stub：路径含 SLOW 的端点一直挂起直到请求级超时取消；其余端点返回城市列表</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.Contains("SLOW"))
            {
                // 慢端点：等待远超测试超时阈值，直到 WeatherNmcService 的请求级超时触发取消
                await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
            }

            var json = """[{"Code":"58362","Province":"上海","City":"上海"},{"Code":"58367","Province":"北京","City":"北京"}]""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }
    }

    [Fact]
    [DisplayName("FetchCitiesAsync_慢端点超时返回空且不挂起")]
    public async Task FetchCities_SlowEndpoint_TimeoutReturnsEmpty()
    {
        var svc = new WeatherNmcService(new HttpClient(new StubHandler()), TimeSpan.FromMilliseconds(300));

        var sw = Stopwatch.StartNew();
        var cities = await svc.FetchCitiesAsync("A-SLOW", CancellationToken.None);
        sw.Stop();

        Assert.Empty(cities);
        Assert.True(sw.ElapsedMilliseconds < 5000, $"超时应快速跳过，实际耗时 {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    [DisplayName("FetchCitiesAsync_正常端点解析城市列表")]
    public async Task FetchCities_Normal_ReturnsCities()
    {
        var svc = new WeatherNmcService(new HttpClient(new StubHandler()), TimeSpan.FromSeconds(5));

        var cities = await svc.FetchCitiesAsync("ASH", CancellationToken.None);

        Assert.NotEmpty(cities);
        Assert.Contains(cities, c => c.City == "上海");
    }
}
