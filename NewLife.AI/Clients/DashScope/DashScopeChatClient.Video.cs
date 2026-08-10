using NewLife.AI.Clients.OpenAI;
using NewLife.Serialization;

namespace NewLife.AI.Clients.DashScope;

public partial class DashScopeChatClient
{
    #region 文生视频
    /// <summary>提交视频生成任务。使用 DashScope 原生异步任务接口</summary>
    /// <param name="request">视频生成请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务提交响应，含 TaskId</returns>
    public override async Task<VideoTaskSubmitResponse> SubmitVideoGenerationAsync(VideoGenerationRequest request, CancellationToken cancellationToken = default)
    {
        var url = GetNativeBaseUrl() + VideoSynthesisPath;

        var body = new Dictionary<String, Object?>
        {
            ["model"] = request.Model ?? _options.Model,
            ["input"] = BuildVideoInput(request),
            ["parameters"] = BuildVideoParameters(request),
        };

        var json = await PostAsync(url, body, null, _options, cancellationToken).ConfigureAwait(false);
        return ParseDashScopeVideoSubmitResponse(json);
    }

    /// <summary>查询视频生成任务状态。使用 DashScope 的 /tasks/{task_id} 接口</summary>
    /// <param name="taskId">任务编号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务状态响应</returns>
    public override async Task<VideoTaskStatusResponse> GetVideoTaskAsync(String taskId, CancellationToken cancellationToken = default)
    {
        var url = GetNativeBaseUrl() + $"/tasks/{taskId}";

        var json = await GetAsync(url, null, _options, cancellationToken).ConfigureAwait(false);
        return ParseDashScopeVideoStatusResponse(json);
    }

    /// <summary>构建 DashScope 视频生成 input 字段</summary>
    private static Dictionary<String, Object?> BuildVideoInput(VideoGenerationRequest request)
    {
        var input = new Dictionary<String, Object?> { ["prompt"] = request.Prompt };
        if (!String.IsNullOrEmpty(request.ImageUrl))
        {
            // wan2.7-i2v 新版协议优先使用 media 数组；旧版 i2v 继续兼容 img_url
            if (IsWan27I2vModel(request.Model))
            {
                input["media"] = new[]
                {
                    new Dictionary<String, Object?>
                    {
                        ["type"] = "first_frame",
                        ["url"] = request.ImageUrl,
                    },
                };
            }
            else
            {
                input["img_url"] = request.ImageUrl;
            }
        }

        if (!String.IsNullOrEmpty(request.NegativePrompt))
            input["negative_prompt"] = request.NegativePrompt;
        return input;
    }

    /// <summary>是否为万相 2.7 图生视频模型</summary>
    private static Boolean IsWan27I2vModel(String? model)
    {
        if (model.IsNullOrEmpty()) return false;
        return model.StartsWith("wan2.7-i2v", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>构建 DashScope 视频生成 parameters 字段</summary>
    private static Dictionary<String, Object?>? BuildVideoParameters(VideoGenerationRequest request)
    {
        var param = new Dictionary<String, Object?>();
        if (!String.IsNullOrEmpty(request.Size))
            param["size"] = request.Size;
        if (request.Duration > 0)
            param["duration"] = request.Duration;
        if (request.Fps > 0)
            param["fps"] = request.Fps;
        if (request.Seed.HasValue)
            param["seed"] = request.Seed.Value;
        return param.Count > 0 ? param : null;
    }

    /// <summary>解析 DashScope 视频任务提交响应</summary>
    private VideoTaskSubmitResponse ParseDashScopeVideoSubmitResponse(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) return new VideoTaskSubmitResponse();

        var output = dic["output"] as IDictionary<String, Object>;
        return new VideoTaskSubmitResponse
        {
            TaskId = output?["task_id"] as String,
            RequestId = dic["request_id"] as String,
            Status = output?["task_status"] as String,
        };
    }

    /// <summary>解析 DashScope 视频任务状态响应</summary>
    private VideoTaskStatusResponse ParseDashScopeVideoStatusResponse(String json)
    {
        var dic = JsonParser.Decode(json);
        if (dic == null) return new VideoTaskStatusResponse();

        var output = dic["output"] as IDictionary<String, Object>;
        var resp = new VideoTaskStatusResponse
        {
            TaskId = output?["task_id"] as String,
            RequestId = dic["request_id"] as String,
            Status = output?["task_status"] as String,
        };

        // 视频URL在 output.video_url 或 output.results[].url
        if (output?["video_url"] is String videoUrl)
        {
            resp.VideoUrls = [videoUrl];
        }
        else if (output?["results"] is IList<Object> results)
        {
            resp.VideoUrls = results
                .OfType<IDictionary<String, Object>>()
                .Select(r => r["url"] as String ?? "")
                .Where(u => u.Length > 0)
                .ToArray();
        }

        resp.ErrorCode = output?["code"] as String ?? dic["code"] as String;
        resp.ErrorMessage = output?["message"] as String ?? dic["message"] as String;

        return resp;
    }
    #endregion
}
