using Microsoft.AspNetCore.Mvc;
using NewLife.ChatAI.Entity;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static NewLife.ChatAI.Entity.ModelConfig;

namespace NewLife.ChatAI.Areas.ChatAI.Controllers;

/// <summary>模型配置。后端接入的大语言模型，关联到具体的提供商实例</summary>
[Menu(110, false, Icon = "fa-table", LastUpdate = "2026-4-6")]
[ChatAIArea]
public class ModelConfigController : EntityController<ModelConfig>
{
    static ModelConfigController()
    {
        //LogOnChange = true;

        //ListFields.RemoveField("Id", "Creator");
        ListFields.RemoveCreateField().RemoveRemarkField();

        {
            var df = AddFormFields.AddDataField("RoleIds", "RoleNames");
            df.DataSource = entity => Role.FindAllWithCache().Where(x => x.Enable).OrderByDescending(e => e.Sort).ToDictionary(e => e.ID, e => e.Name);
            AddFormFields.RemoveField("RoleNames");
        }
        {
            var df = EditFormFields.AddDataField("RoleIds", "RoleNames");
            df.DataSource = entity => Role.FindAllWithCache().Where(x => x.Enable).OrderByDescending(e => e.Sort).ToDictionary(e => e.ID, e => e.Name);
            EditFormFields.RemoveField("RoleNames");
        }
        {
            var df = AddFormFields.AddDataField("DepartmentIds", "DepartmentNames");
            df.DataSource = entity => Department.FindAllWithCache().Where(x => x.Enable).OrderByDescending(e => e.Sort).ToDictionary(e => e.ID, e => e.Name);
            AddFormFields.RemoveField("DepartmentNames");
        }
        {
            var df = EditFormFields.AddDataField("DepartmentIds", "DepartmentNames");
            df.DataSource = entity => Department.FindAllWithCache().Where(x => x.Enable).OrderByDescending(e => e.Sort).ToDictionary(e => e.ID, e => e.Name);
            EditFormFields.RemoveField("DepartmentNames");
        }
    }

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<ModelConfig> Search(Pager p)
    {
        var providerId = p["providerId"].ToInt(-1);
        var code = p["code"];
        var supportThinking = p["supportThinking"]?.ToBoolean();
        var supportVision = p["supportVision"]?.ToBoolean();
        var supportImageGeneration = p["supportImageGeneration"]?.ToBoolean();
        var supportFunctionCalling = p["supportFunctionCalling"]?.ToBoolean();
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return ModelConfig.Search(providerId, code, supportThinking, supportVision, supportImageGeneration, supportFunctionCalling, enable, start, end, p["Q"], p);
    }
}