using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Entity;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static NewLife.ChatAI.Entity.ProviderConfig;

namespace NewLife.ChatAI.Areas.ChatAI.Controllers;

/// <summary>提供商配置。AI服务商的连接信息，一个协议类型可以有多个实例</summary>
[Menu(120, true, Icon = "fa-table")]
[ChatAIArea]
public class ProviderConfigController : EntityController<ProviderConfig>
{
    static ProviderConfigController()
    {
        //LogOnChange = true;

        ListFields.RemoveField("Provider");
        ListFields.RemoveCreateField().RemoveRemarkField();

        //{
        //    var df = ListFields.GetField("Code") as ListField;
        //    df.Url = "?code={Code}";
        //    df.Target = "_blank";
        //}
        {
            //var df = ListFields.AddListField("models", null, "Name");
            //df.DisplayName = "模型列表";
            var df = ListFields.GetField("Name") as ListField;
            df.Url = "/ChatAI/ModelConfig?providerId={Id}";
            df.Target = "_frame";
        }
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as ProviderConfig).Kind).ToString("X4");
        //}
        //ListFields.TraceUrl("TraceId");
    }

    //private readonly ITracer _tracer;

    //public ProviderConfigController(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<ProviderConfig> Search(Pager p)
    {
        var code = p["code"];
        var provider = p["provider"];
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return ProviderConfig.Search(code, provider, enable, start, end, p["Q"], p);
    }
}