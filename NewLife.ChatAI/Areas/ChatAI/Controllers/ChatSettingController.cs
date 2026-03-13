using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.AI.ChatAI;
using NewLife.ChatAI.Services;
using NewLife.Cube;
using NewLife.Cube.Areas.Admin;

namespace NewLife.ChatAI.Areas.ChatAI.Controllers;

/// <summary>对话设置控制器</summary>
[DisplayName("对话设置")]
[ChatAIArea]
[Menu(30, true, Icon = "fa-wrench")]
public class ChatSettingController : ConfigController<ChatSetting>
{

}
