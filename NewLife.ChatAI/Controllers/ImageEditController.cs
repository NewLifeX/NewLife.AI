using Microsoft.AspNetCore.Mvc;
using NewLife.AI.Clients;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using NewLife.ChatAI.Services;

namespace NewLife.ChatAI.Controllers;

/// <summary>图像编辑控制器。面向前端 Web UI，使用 Cookie 认证，无需 AppKey</summary>
[Route("api/images")]
public class ImageEditController(ModelService modelService, ChatSetting chatSetting) : ChatApiControllerBase
{
    /// <summary>图像编辑。解析 multipart/form-data，路由到对应图像编辑服务商</summary>
    [HttpPost("edits")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> EditAsync(CancellationToken cancellationToken)
    {
        var form = await Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        var modelCode = form["model"].FirstOrDefault();
        var prompt = form["prompt"].FirstOrDefault();
        var size = form["size"].FirstOrDefault() ?? chatSetting.DefaultImageSize;
        var imageFile = form.Files.GetFile("image");
        var maskFile = form.Files.GetFile("mask");

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
            using var editClient = modelService.CreateClient(config)!;
            if (editClient is not IImageClient imageClient)
                return BadRequest(new { code = "MODEL_UNSUPPORTED", message = $"模型 '{config.Code}' 不支持图像编辑" });

            using var imageStream = imageFile.OpenReadStream();
            using var maskStream = maskFile != null && maskFile.Length > 0 ? maskFile.OpenReadStream() : null;

            var response = await imageClient.EditImageAsync(new ImageEditsRequest
            {
                Model = config.Code,
                Prompt = prompt!,
                Size = size,
                ImageStream = imageStream,
                ImageFileName = String.IsNullOrWhiteSpace(imageFile.FileName) ? "image.png" : imageFile.FileName,
                MaskStream = maskStream,
                MaskFileName = maskFile != null && !String.IsNullOrWhiteSpace(maskFile.FileName) ? maskFile.FileName : "mask.png",
            }, cancellationToken).ConfigureAwait(false);

            return Ok(NormalizeImageEditResponse(response, prompt!));
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(new { code = "MODEL_UNSUPPORTED", message = ex.Message });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return StatusCode(502, new { code = "IMAGE_EDIT_FAILED", message = ex.Message });
        }
    }

    private static Object NormalizeImageEditResponse(ImageGenerationResponse? response, String prompt)
    {
        var created = response?.Created > DateTime.MinValue
            ? new DateTimeOffset(response.Created.ToUniversalTime()).ToUnixTimeSeconds()
            : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var data = response?.Data?.Select(item => new
        {
            revised_prompt = item.RevisedPrompt ?? prompt,
            content = GetImageContent(item),
            url = item.Url,
            b64_json = item.B64Json,
        }).ToArray();

        return new
        {
            created,
            data = data is { Length: > 0 }
                ? data
                : new[]
                {
                    new
                    {
                        revised_prompt = prompt,
                        content = (String?)null,
                        url = (String?)null,
                        b64_json = (String?)null,
                    }
                }
        };
    }

    private static String? GetImageContent(ImageData item)
    {
        if (!String.IsNullOrWhiteSpace(item.Content)) return item.Content;
        if (!String.IsNullOrWhiteSpace(item.Url)) return item.Url;
        if (!String.IsNullOrWhiteSpace(item.B64Json)) return $"data:image/png;base64,{item.B64Json}";

        return null;
    }
}
