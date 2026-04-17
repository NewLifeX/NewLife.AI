using Microsoft.AspNetCore.Mvc;
using NewLife.Cube.Entity;
using NewLife.ChatAI.Models;
using NewLife.ChatAI.Services;

namespace NewLife.ChatAI.Controllers;

/// <summary>附件控制器</summary>
[Route("api/attachments")]
public class AttachmentsController(ChatSetting chatSetting) : ChatApiControllerBase
{

    /// <summary>上传附件</summary>
    /// <param name="file">文件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    [HttpPost]
    [RequestSizeLimit(500 * 1024 * 1024)]
    public async Task<ActionResult<UploadAttachmentResult>> UploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length <= 0) return BadRequest("无有效文件");

        // 文件大小限制
        var maxBytes = chatSetting.MaxAttachmentSize * 1024L * 1024L;
        if (file.Length > maxBytes)
            return BadRequest($"文件大小超出限制，最大允许 {chatSetting.MaxAttachmentSize}MB");

        // 文件类型限制
        if (!chatSetting.AllowedExtensions.IsNullOrEmpty())
        {
            var ext = Path.GetExtension(file.FileName);
            var allowed = chatSetting.AllowedExtensions.Split(',');
            if (!allowed.Any(e => e.Trim().EqualIgnoreCase(ext)))
                return BadRequest($"不支持的文件类型 {ext}，允许的类型：{chatSetting.AllowedExtensions}");
        }

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

    /// <summary>批量获取附件元信息</summary>
    /// <param name="ids">附件编号列表，逗号分隔</param>
    /// <returns></returns>
    [HttpGet("info")]
    public ActionResult<IEnumerable<AttachmentInfoResult>> GetInfoAsync([FromQuery] String ids)
    {
        if (ids.IsNullOrEmpty()) return Ok(Array.Empty<AttachmentInfoResult>());

        var results = ids.Split(',')
            .Select(s => s.Trim().ToLong())
            .Where(id => id > 0)
            .Select(id => Attachment.FindById(id))
            .Where(att => att != null)
            .Select(att => new AttachmentInfoResult(att!.Id, att.FileName, att.Size, BuildUrl(att), att.ContentType.StartsWithIgnoreCase("image/")))
            .ToList();

        return Ok(results);
    }

    private static String BuildUrl(Attachment att)
    {
        if (!att.ContentType.IsNullOrEmpty() && att.ContentType.StartsWithIgnoreCase("image/"))
            return $"/cube/image?id={att.Id}{att.Extension}";

        return $"/cube/file?id={att.Id}{att.Extension}";
    }
}
