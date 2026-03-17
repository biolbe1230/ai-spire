namespace AISpire.Config;

public static class AIConfig
{
    // DeepSeek API
    public const string ApiKey = "sk-876589a840af4e288ca6b116be0dcd9a";
    public const string ApiEndpoint = "https://api.deepseek.com/v1/chat/completions";
    public const string Model = "deepseek-chat";

    // 超时与重试
    public const int ApiTimeoutMs = 15000;
    public const int MaxRetries = 1;

    // AI 开关
    public static bool Enabled { get; set; } = true;

    // 决策间隔（毫秒），防止过快操作
    public const int ActionDelayMs = 500;

    // 日志
    public const bool VerboseLogging = true;

    // spire-codex 数据目录（运行时自动定位到 mods/AISpire/data/）
    public static string DataPath =>
        Path.Combine(Path.GetDirectoryName(typeof(AIConfig).Assembly.Location) ?? ".", "data");

    // 多轮对话最大历史消息数（system 不计入）
    public const int MaxHistoryMessages = 40;
}
