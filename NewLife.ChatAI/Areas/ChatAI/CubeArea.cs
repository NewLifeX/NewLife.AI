using System.ComponentModel;
using NewLife;
using NewLife.Cube;

namespace NewLife.ChatAI.Areas.Cube;

[DisplayName("AI对话")]
public class CubeArea : AreaBase
{
    public CubeArea() : base(nameof(CubeArea).TrimEnd("Area")) { }
}