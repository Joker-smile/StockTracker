using Newtonsoft.Json;

namespace StockTracker;

/// <summary>
/// 应用配置模型：AI、搜索、邮件及定时任务
/// </summary>
public class AppSettings
{
    // ── AI 统一配置 ───────────────────────────────────────────────────────────
    // 只需填写下面三项，系统根据 BaseUrl 自动识别协议类型（Gemini / OpenAI 兼容）。
    //
    //  常用预设：
    //  ┌──────────────────────────────────────────────────────────────────────┐
    //  │  Gemini（默认）  BaseUrl 留空  Model 留空（自动 gemini-2.5-flash）   │
    //  │  DeepSeek        https://api.deepseek.com/v1          deepseek-chat  │
    //  │  千问(Qwen)      https://dashscope.aliyuncs.com/compatible-mode/v1   │
    //  │                                                         qwen-plus    │
    //  │  GLM             https://open.bigmodel.cn/api/paas/v4  glm-4-flash  │
    //  │  自定义中转       https://your-proxy.example.com/v1    <模型名>      │
    //  └──────────────────────────────────────────────────────────────────────┘

    /// <summary>当前选择的快捷平台（仅用于 UI 状态）</summary>
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

    /// <summary>
    /// API Base URL（留空 = Gemini 官方地址）
    /// </summary>
    [JsonProperty("aiBaseUrl")]
    public string AiBaseUrl { get; set; } = "";

    /// <summary>
    /// 模型名称列表（支持逗号分隔多个，系统将依次尝试 fallback）
    /// </summary>
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

    // ── 定时任务配置 ──────────────────────────────────────────────────────────

    [JsonProperty("scheduleEnabled")]
    public bool ScheduleEnabled { get; set; } = false;

    [JsonProperty("scheduleTime")]
    public string ScheduleTime { get; set; } = "09:00";

    // ── 运行时只读属性（不序列化） ────────────────────────────────────────────

    /// <summary>
    /// 是否使用 Gemini 原生协议。
    /// 判断规则：Base URL 为空，或包含 "generativelanguage.googleapis.com"。
    /// 其余一律走 OpenAI Chat Completions 兼容协议。
    /// </summary>
    [JsonIgnore]
    public bool IsGeminiProtocol
    {
        get
        {
            if (string.IsNullOrWhiteSpace(AiBaseUrl)) return true;
            return AiBaseUrl.Contains("generativelanguage.googleapis.com");
        }
    }

    /// <summary>实际使用的 Base URL（已去除末尾斜杠）</summary>
    [JsonIgnore]
    public string ResolvedBaseUrl => string.IsNullOrWhiteSpace(AiBaseUrl)
        ? "https://generativelanguage.googleapis.com/v1beta"
        : AiBaseUrl.TrimEnd('/');

    /// <summary>
    /// 实际使用的模型列表（首项为首选，后续为 fallback）。
    /// - 用户已填模型：按逗号分割解析。
    /// - 用户未填 + Gemini：自动使用内置 fallback 链。
    /// - 用户未填 + OpenAI 兼容：返回空数组拦截。
    /// </summary>
    [JsonIgnore]
    public string[] ResolvedModels
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(AiModel))
            {
                return AiModel
                    .Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(m => m.Trim())
                    .ToArray();
            }

            if (IsGeminiProtocol)
                return new[] { "gemini-2.5-flash", "gemini-3-flash-preview", "gemini-2.0-flash", "gemini-1.5-flash" };

            // OpenAI 兼容但未指定模型，无法 fallback
            return Array.Empty<string>();
        }
    }

    /// <summary>实际使用的 API Key（根据当前选中的平台自动获取）</summary>
    [JsonIgnore]
    public string ApiKey => AiPlatform switch
    {
        "Gemini" => GeminiApiKey ?? "",
        "DeepSeek" => DeepSeekApiKey ?? "",
        "阿里云百炼 (Qwen)" => QwenApiKey ?? "",
        "智谱 (GLM)" => GlmApiKey ?? "",
        _ => CustomApiKey ?? ""
    };
}
