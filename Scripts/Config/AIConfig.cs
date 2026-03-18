using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace AISpire.Config;

public static class AIConfig
{
    // ── 运行时从 config.json 加载的字段 ──
    public static string ApiKey { get; private set; } = "";
    public static string ApiEndpoint { get; private set; } = "https://api.deepseek.com/v1/chat/completions";
    public static string Model { get; private set; } = "deepseek-chat";
    public static int ApiTimeoutMs { get; private set; } = 15000;
    public static int MaxRetries { get; private set; } = 1;
    public static bool Enabled { get; set; } = true;
    public static int ActionDelayMs { get; private set; } = 500;
    public static bool VerboseLogging { get; private set; } = true;
    public static int MaxHistoryMessages { get; private set; } = 40;

    // 语言: "zhs"(中文) 或 "en"(英文)，auto 表示跟随游戏
    public static string Language { get; private set; } = "auto";

    private static string _modDir = "";

    // spire-codex 数据目录（根据语言选择 data/en/ 或 data/zhs/）
    public static string DataPath
    {
        get
        {
            var lang = Language == "en" ? "en" : "zhs";
            var langPath = Path.Combine(_modDir, "data", lang);
            if (Directory.Exists(langPath)) return langPath;
            // fallback: 无子目录时尝试旧的 data/ 目录
            return Path.Combine(_modDir, "data");
        }
    }

    /// <summary>
    /// 从 config.json 加载配置，找不到则使用默认值
    /// </summary>
    public static void Load()
    {
        _modDir = Path.GetDirectoryName(typeof(AIConfig).Assembly.Location) ?? ".";
        var configPath = Path.Combine(_modDir, "config.json");

        if (!File.Exists(configPath))
        {
            Log.Info($"[AISpire] config.json not found at {configPath}, using defaults");
            return;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var cfg = JsonSerializer.Deserialize<ConfigJson>(json);
            if (cfg == null) return;

            if (!string.IsNullOrEmpty(cfg.ApiKey)) ApiKey = cfg.ApiKey;
            if (!string.IsNullOrEmpty(cfg.ApiEndpoint)) ApiEndpoint = cfg.ApiEndpoint;
            if (!string.IsNullOrEmpty(cfg.Model)) Model = cfg.Model;
            if (cfg.ApiTimeoutMs.HasValue) ApiTimeoutMs = cfg.ApiTimeoutMs.Value;
            if (cfg.MaxRetries.HasValue) MaxRetries = cfg.MaxRetries.Value;
            if (cfg.Enabled.HasValue) Enabled = cfg.Enabled.Value;
            if (cfg.ActionDelayMs.HasValue) ActionDelayMs = cfg.ActionDelayMs.Value;
            if (cfg.VerboseLogging.HasValue) VerboseLogging = cfg.VerboseLogging.Value;
            if (cfg.MaxHistoryMessages.HasValue) MaxHistoryMessages = cfg.MaxHistoryMessages.Value;
            if (!string.IsNullOrEmpty(cfg.Language)) Language = cfg.Language;

            // auto-detect: 从 Godot TranslationServer 获取当前游戏语言
            if (Language == "auto")
            {
                try
                {
                    var locale = TranslationServer.GetLocale(); // e.g. "zh_CN", "en"
                    Language = locale.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zhs" : "en";
                }
                catch
                {
                    Language = "zhs"; // 获取失败默认中文
                }
            }

            Log.Info($"[AISpire] Config loaded: model={Model}, endpoint={ApiEndpoint}, language={Language}");
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error loading config.json: {e.Message}");
        }
    }

    private class ConfigJson
    {
        [JsonPropertyName("api_key")] public string? ApiKey { get; set; }
        [JsonPropertyName("api_endpoint")] public string? ApiEndpoint { get; set; }
        [JsonPropertyName("model")] public string? Model { get; set; }
        [JsonPropertyName("api_timeout_ms")] public int? ApiTimeoutMs { get; set; }
        [JsonPropertyName("max_retries")] public int? MaxRetries { get; set; }
        [JsonPropertyName("enabled")] public bool? Enabled { get; set; }
        [JsonPropertyName("action_delay_ms")] public int? ActionDelayMs { get; set; }
        [JsonPropertyName("verbose_logging")] public bool? VerboseLogging { get; set; }
        [JsonPropertyName("max_history_messages")] public int? MaxHistoryMessages { get; set; }
        [JsonPropertyName("language")] public string? Language { get; set; }
    }
}
