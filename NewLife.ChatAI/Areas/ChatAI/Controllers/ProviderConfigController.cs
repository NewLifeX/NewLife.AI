using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Entity;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using XCode.Membership;

namespace NewLife.ChatAI.Areas.ChatAI.Controllers;

/// <summary>提供商配置。AI服务商的连接信息，一个协议类型可以有多个实例</summary>
/// <remarks>实例化提供商配置控制器</remarks>
/// <param name="modelService">模型服务</param>
[Menu(120, true, Icon = "fa-table")]
[ChatAIArea]
public class ProviderConfigController(ModelService modelService) : EntityController<ProviderConfig>
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

    /// <summary>批量更新模型列表。对选中的提供商执行模型发现，同步远端模型到本地配置</summary>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public async Task<ActionResult> DiscoverModels()
    {
        var ids = SelectKeys;
        if (ids == null || ids.Length == 0) return JsonRefresh("请先选择提供商！");

        var results = new List<String>();
        foreach (var id in ids)
        {
            var config = ProviderConfig.FindById(id.ToInt());
            if (config == null) continue;

            try
            {
                var msg = await modelService.DiscoverAsync(config).ConfigureAwait(false);
                results.Add(msg);
            }
            catch (Exception ex)
            {
                results.Add($"{config.Name} 发现失败：{ex.Message}");
            }
        }

        return JsonRefresh(results.Count > 0 ? results.Join("；") : "未找到有效提供商");
    }
}