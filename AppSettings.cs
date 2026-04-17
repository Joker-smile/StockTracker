using Newtonsoft.Json;

namespace StockTracker;

/// <summary>
/// 应用配置模型：存储 Gemini AI、邮件及定时任务设置
/// </summary>
public class AppSettings
{
    [JsonProperty("geminiApiKey")]
    public string GeminiApiKey { get; set; } = "";

    [JsonProperty("tavilyApiKey")]
    public string TavilyApiKey { get; set; } = "";

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

    [JsonProperty("scheduleEnabled")]
    public bool ScheduleEnabled { get; set; } = false;

    [JsonProperty("scheduleTime")]
    public string ScheduleTime { get; set; } = "09:00";
}
