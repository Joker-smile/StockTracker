using Newtonsoft.Json;
using System;
using System.Linq;

namespace StockTracker;

/// <summary>
/// 应用配置模型：AI、搜索、邮件及定时任务
/// </summary>
public class AppSettings
{
    // ── AI 统一配置 ───────────────────────────────────────────────────────────
    [JsonProperty("aiPlatform")]
    public string AiPlatform { get; set; } = "Gemini";

    // ── 分平台 API Key ──────────────────────────────────────────────
    [JsonProperty("geminiApiKey")]
    public string GeminiApiKey { get; set; } = "";
    [JsonProperty("deepSeekApiKey")]
    public string DeepSeekApiKey { get; set; } = "";
    [JsonProperty("qwenApiKey")]
    public string QwenApiKey { get; set; } = "";
    [JsonProperty("glmApiKey")]
    public string GlmApiKey { get; set; } = "";
    [JsonProperty("customApiKey")]
    public string CustomApiKey { get; set; } = "";

    [JsonProperty("aiBaseUrl")]
    public string AiBaseUrl { get; set; } = "";

    [JsonProperty("aiModel")]
    public string AiModel { get; set; } = "";

    // ── 搜索配置 ──────────────────────────────────────────────────────────────
    [JsonProperty("tavilyApiKey")]
    public string TavilyApiKey { get; set; } = "";

    // ── 邮件配置 ──────────────────────────────────────────────────────────────
    [JsonProperty("emailSmtpHost")]
    public string EmailSmtpHost { get; set; } = "";
    [JsonProperty("emailSmtpPort")]
    public int EmailSmtpPort { get; set; } = 465;
    [JsonProperty("emailSmtpSsl")]
    public bool EmailSmtpSsl { get; set; } = true;
    [JsonProperty("emailUser")]
    public string EmailUser { get; set; } = "";
    [JsonProperty("emailPassword")]
    public string EmailPassword { get; set; } = "";
    [JsonProperty("emailTo")]
    public string EmailTo { get; set; } = "";

    // ── 功能开关 ──────────────────────────────────────────────────────────────
    [JsonProperty("marketReviewEnabled")]
    public bool MarketReviewEnabled { get; set; } = true;

    // ── 定时任务配置 ──────────────────────────────────────────────────────────
    [JsonProperty("scheduleEnabled")]
    public bool ScheduleEnabled { get; set; } = false;
    [JsonProperty("scheduleTime")]
    public string ScheduleTime { get; set; } = "09:00";

    // ── 运行时只读属性 ────────────────────────────────────────────────────────
    [JsonIgnore]
    public bool IsGeminiProtocol => string.IsNullOrWhiteSpace(AiBaseUrl) || AiBaseUrl.Contains("generativelanguage.googleapis.com");

    [JsonIgnore]
    public string ResolvedBaseUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(AiBaseUrl))
                return "https://generativelanguage.googleapis.com/v1beta";

            string url = AiBaseUrl.Trim().TrimEnd('/');
            if (url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                url = url.Substring(0, url.Length - "/chat/completions".Length).TrimEnd('/');
            }
            return url;
        }
    }

    [JsonIgnore]
    public string[] ResolvedModels
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(AiModel))
                return AiModel.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries).Select(m => m.Trim()).ToArray();

            return AiPlatform switch
            {
                "DeepSeek" => new[] { "deepseek-chat", "deepseek-reasoner" },
                "阿里云百炼 (Qwen)" => new[] { "qwen3-max", "qwen-max-latest", "qwen-plus-latest", "qwen-turbo-latest" },
                var p when p.Contains("Coding Plan") || p == "智谱 (GLM)" => new[] { "glm-5.3", "glm-5.3-flash", "glm-5-turbo", "glm-4-flash" },
                var p when p.Contains("智谱") => new[] { "glm-4-flash", "glm-4-plus", "glm-4-air" },
                "Gemini" => new[] { "gemini-2.5-flash", "gemini-3.8-flash", "gemini-2.5-pro" },
                _ => IsGeminiProtocol
                    ? new[] { "gemini-2.5-flash", "gemini-3.8-flash", "gemini-2.5-pro" }
                    : Array.Empty<string>()
            };
        }
    }

    [JsonIgnore]
    public string ApiKey => AiPlatform switch
    {
        "Gemini" => GeminiApiKey ?? "",
        "DeepSeek" => DeepSeekApiKey ?? "",
        "阿里云百炼 (Qwen)" => QwenApiKey ?? "",
        var p when p.Contains("智谱") || p.Contains("GLM") => GlmApiKey ?? "",
        _ => CustomApiKey ?? ""
    };
}
