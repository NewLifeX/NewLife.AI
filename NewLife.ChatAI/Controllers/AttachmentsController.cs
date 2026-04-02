using Microsoft.AspNetCore.Mvc;
using NewLife.Cube.Entity;
using NewLife.ChatAI.Models;

namespace NewLife.ChatAI.Controllers;

/// <summary>附件控制器</summary>
[Route("api/attachments")]
public class AttachmentsController : ChatApiControllerBase
{
    /// <summary>上传附件</summary>
    /// <param name="file">文件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<UploadAttachmentResult>> UploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length <= 0) return BadRequest("无有效文件");

        var att = new Attachment
        {
            Category = "ChatAI",
            ContentType = file.ContentType,
            Size = file.Length,
            Enable = true,
            UploadTime = DateTime.Now,
        };

        var ok = await att.SaveFile(file.OpenReadStream(), null, file.FileName);
        if (!ok) return StatusCode(500, "附件保存失败");

        return Ok(new UploadAttachmentResult(att.Id, att.FileName, BuildUrl(att), att.Size));
    }

    /// <summary>重定向到 Cube 文件路由（兼容旧 URL）</summary>
    /// <param name="id">附件编号</param>
    /// <returns></returns>
    [HttpGet("{id}")]
    public IActionResult GetAsync([FromRoute] Int64 id)
    {
        var entity = Attachment.FindById(id);
        if (entity == null) return NotFound();

        return Redirect(BuildUrl(entity));
    }

    /// <summary>批量获取附件元信息</summary>
    /// <param name="ids">附件编号列表，逗号分隔</param>
    /// <returns></returns>
    [HttpGet("info")]
    public ActionResult<AttachmentInfoDto[]> GetInfos([FromQuery] String ids)
    {
        if (ids.IsNullOrEmpty()) return Ok(Array.Empty<AttachmentInfoDto>());

        var idParts = ids.Split(',');
        if (idParts.Length > 100) return BadRequest("一次最多查询100个附件");

        var list = idParts
            .Select(s => s.Trim().ToLong())
            .Where(id => id > 0)
            .Select(id => Attachment.FindById(id))
            .Where(att => att != null)
            .Select(att => new AttachmentInfoDto(att!.Id, att.FileName, att.Size, BuildUrl(att), att.ContentType.StartsWithIgnoreCase("image/")))
            .ToArray();

        return Ok(list);
    }

    private static String BuildUrl(Attachment att)
    {
        if (!att.ContentType.IsNullOrEmpty() && att.ContentType.StartsWithIgnoreCase("image/"))
            return $"/cube/image?id={att.Id}{att.Extension}";

        return $"/cube/file?id={att.Id}{att.Extension}";
    }
}

