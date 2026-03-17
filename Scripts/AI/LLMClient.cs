using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AISpire.Config;
using MegaCrit.Sts2.Core.Logging;

namespace AISpire.AI;

public static class LLMClient
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromMilliseconds(AIConfig.ApiTimeoutMs)
    };

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// 多轮对话版本：传入完整消息历史
    /// </summary>
    public static async Task<(AIDecision? Decision, string? RawText)> GetDecisionWithHistoryAsync(
        List<ChatMessage> messages)
    {
        for (int attempt = 0; attempt <= AIConfig.MaxRetries; attempt++)
        {
            try
            {
                var msgArray = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray();
                var requestBody = new
                {
                    model = AIConfig.Model,
                    messages = msgArray,
                    temperature = 0.3,
                    max_tokens = 512
                };

                var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, AIConfig.ApiEndpoint);
                request.Headers.Add("Authorization", $"Bearer {AIConfig.ApiKey}");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Log.Debug($"[AISpire] LLM API error (attempt {attempt}): {response.StatusCode} - {responseJson}");
                    continue;
                }

                return ParseResponseWithRaw(responseJson);
            }
            catch (TaskCanceledException)
            {
                Log.Debug($"[AISpire] LLM API timeout (attempt {attempt})");
            }
            catch (Exception e)
            {
                Log.Debug($"[AISpire] LLM API error (attempt {attempt}): {e.Message}");
            }
        }

        return (null, null);
    }

    /// <summary>
    /// 单次调用版本（兼容旧接口）
    /// </summary>
    public static async Task<AIDecision?> GetDecisionAsync(string systemPrompt, string userPrompt)
    {
        var messages = new List<ChatMessage>
        {
            new("system", systemPrompt),
            new("user", userPrompt)
        };
        var (decision, _) = await GetDecisionWithHistoryAsync(messages);
        return decision;
    }

    private static (AIDecision? Decision, string? RawText) ParseResponseWithRaw(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var choices = doc.RootElement.GetProperty("choices");
            var message = choices[0].GetProperty("message");
            var text = message.GetProperty("content").GetString() ?? "";

            if (AIConfig.VerboseLogging)
                Log.Debug($"[AISpire] LLM raw response: {text}");

            // 从响应中提取 JSON
            var jsonStart = text.IndexOf('{');
            var jsonEnd = text.LastIndexOf('}');
            if (jsonStart < 0 || jsonEnd < 0 || jsonEnd <= jsonStart)
            {
                Log.Debug("[AISpire] No JSON found in LLM response");
                return (null, text);
            }

            var decisionJson = text.Substring(jsonStart, jsonEnd - jsonStart + 1);
            var decision = JsonSerializer.Deserialize<AIDecision>(decisionJson, _jsonOptions);
            return (decision, text);
        }
        catch (Exception e)
        {
            Log.Debug($"[AISpire] Failed to parse LLM response: {e.Message}");
            return (null, null);
        }
    }
}
