using Microsoft.AspNetCore.Mvc;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.ChatAI.Services;
using AiChatMessage = NewLife.AI.Models.ChatMessage;

namespace NewLife.ChatAI.Controllers;

/// <summary>图像编辑控制器。面向前端 Web UI，使用 Cookie 认证，无需 AppKey</summary>
[Route("api/images")]
public class ImageEditController(ModelService modelService) : ChatApiControllerBase
{
    /// <summary>图像编辑。解析 multipart/form-data，路由到对应图像编辑服务商</summary>
    [HttpPost("edits")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> EditAsync(CancellationToken cancellationToken)
    {
        var form = await Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        var modelCode = form["model"].FirstOrDefault();
        var prompt = form["prompt"].FirstOrDefault();
        var size = form["size"].FirstOrDefault() ?? ChatSetting.Current.DefaultImageSize;
        var imageFile = form.Files.GetFile("image");

        if (String.IsNullOrWhiteSpace(prompt))
            return BadRequest(new { code = "INVALID_REQUEST", message = "prompt 不能为空" });

        if (imageFile == null || imageFile.Length == 0)
            return BadRequest(new { code = "INVALID_REQUEST", message = "image 文件不能为空" });

        var config = modelService.ResolveModelByCode(modelCode);
        if (config == null)
            return NotFound(new { code = "MODEL_NOT_FOUND", message = $"未找到模型 '{modelCode}'" });

        if (!modelService.IsAvailable(config))
            return StatusCode(503, new { code = "MODEL_UNAVAILABLE", message = $"未找到服务商 '{config.GetEffectiveProvider()}'" });

        try
        {
            using var ms = new MemoryStream();
            await imageFile.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            var imageBase64 = Convert.ToBase64String(ms.ToArray());
            var mimeType = imageFile.ContentType ?? "image/png";
            var dataUri = $"data:{mimeType};base64,{imageBase64}";

            var maskFile = form.Files.GetFile("mask");
            String? maskInfo = null;
            if (maskFile != null && maskFile.Length > 0)
            {
                using var maskMs = new MemoryStream();
                await maskFile.CopyToAsync(maskMs, cancellationToken).ConfigureAwait(false);
                maskInfo = $"data:{maskFile.ContentType ?? "image/png"};base64,{Convert.ToBase64String(maskMs.ToArray())}";
            }

            var contentParts = new List<Object>
            {
                new { type = "text", text = $"Edit this image: {prompt}. Size: {size}" },
                new { type = "image_url", image_url = new { url = dataUri } },
            };
            if (maskInfo != null)
                contentParts.Add(new { type = "image_url", image_url = new { url = maskInfo } });

            using var editClient = modelService.CreateClient(config)!;
            var response = await editClient.GetResponseAsync(
                [new AiChatMessage { Role = "user", Content = contentParts }],
                new ChatOptions { Model = config.Code },
                cancellationToken).ConfigureAwait(false);

            return Ok(new
            {
                created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                data = new[]
                {
                    new
                    {
                        revised_prompt = prompt,
                        content = response.Messages?.FirstOrDefault()?.Message?.Content,
                    }
                }
            });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { code = "IMAGE_EDIT_FAILED", message = ex.Message });
        }
    }
}
