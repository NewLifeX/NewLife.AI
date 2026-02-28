using System.ComponentModel;
using NewLife;
using NewLife.Cube;

namespace NewLife.ChatAI.Areas.ChatAI;

[DisplayName("AI对话")]
public class ChatAIArea : AreaBase
{
    public ChatAIArea() : base(nameof(ChatAIArea).TrimEnd("Area")) { }
}