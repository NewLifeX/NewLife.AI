using Microsoft.AspNetCore.Mvc;
using NewLife.Cube.Entity;
using NewLife.ChatAI.Services;
using NewLife.ChatAI.Models;

namespace NewLife.ChatAI.Controllers;

/// <summary>附件控制器</summary>
[Route("api/attachments")]
public class AttachmentsController(ChatApplicationService chatService) : ChatApiControllerBase
{
    /// <summary>附件存储根目录</summary>
    private static readonly String _attachmentRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Attachments");

    /// <summary>上传附件</summary>
    /// <param name="file">文件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<UploadAttachmentResult>> UploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length <= 0) return BadRequest("无有效文件");

        await using var stream = file.OpenReadStream();
        var result = await chatService.UploadAttachmentAsync(file.FileName, file.Length, stream, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>下载/预览附件</summary>
    /// <param name="id">附件编号</param>
    /// <returns></returns>
    [HttpGet("{id}")]
    public IActionResult GetAsync([FromRoute] String id)
    {
        var attachId = id.ToLong();
        if (attachId <= 0) return NotFound();

        var entity = Attachment.FindById(attachId);
        if (entity == null) return NotFound();

        var filePath = Path.Combine(_attachmentRoot, entity.FilePath);
        // 安全检查：确保解析后的路径仍在附件目录内，防止路径遍历
        if (!Path.GetFullPath(filePath).StartsWith(Path.GetFullPath(_attachmentRoot))) return BadRequest();
        if (!System.IO.File.Exists(filePath)) return NotFound();

        var contentType = entity.ContentType;
        if (String.IsNullOrWhiteSpace(contentType)) contentType = "application/octet-stream";

        return PhysicalFile(filePath, contentType, entity.FileName);
    }
}
