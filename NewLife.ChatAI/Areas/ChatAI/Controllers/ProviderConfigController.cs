using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Entity;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;

namespace NewLife.ChatAI.Areas.ChatAI.Controllers;

/// <summary>提供商配置。AI服务商的连接信息</summary>
[Menu(19, true, Icon = "fa-server")]
[ChatAIArea]
public class ProviderConfigController : EntityController<ProviderConfig>
{
    static ProviderConfigController()
    {
        //LogOnChange = true;

        //ListFields.RemoveField("Id", "Creator");
        ListFields.RemoveCreateField().RemoveRemarkField();
        ListFields.AddListField("Remark", "UpdateUserId");

        // ApiKey 字段脱敏显示
        {
            var df = ListFields.GetField("ApiKey") as ListField;
            if (df != null)
            {
                df.GetValue = e =>
                {
                    var apiKey = (e as ProviderConfig)?.ApiKey;
                    if (apiKey.IsNullOrEmpty()) return "";
                    if (apiKey.Length <= 8) return "***";
                    return apiKey[..4] + "***" + apiKey[^4..];
                };
            }
        }
    }

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
