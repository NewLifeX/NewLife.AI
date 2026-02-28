using Microsoft.AspNetCore.Mvc;
using NewLife.AI.ChatAI.Contracts;

namespace NewLife.ChatAI.Controllers;

/// <summary>附件控制器</summary>
[ApiController]
[Route("api/attachments")]
public class AttachmentsController(IChatApplicationService chatService) : ControllerBase
{
    [HttpPost("upload")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<UploadAttachmentResult>> UploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length <= 0) return BadRequest("无有效文件");

        await using var stream = file.OpenReadStream();
        var result = await chatService.UploadAttachmentAsync(file.FileName, file.Length, stream, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public IActionResult GetAsync([FromRoute] String id)
    {
        return Ok(new { id, url = $"/uploads/{id}", message = "附件预览占位接口" });
    }
}
