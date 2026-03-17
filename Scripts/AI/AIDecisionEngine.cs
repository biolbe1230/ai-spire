using AISpire.Config;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;

namespace AISpire.AI;

/// <summary>
/// AI 决策引擎主控 - 协调状态提取、LLM 调用、规则引擎和动作执行
/// 支持多轮对话：每场战斗维护完整消息历史，让 LLM 记住之前的决策
/// </summary>
public static class AIDecisionEngine
{
    private static bool _isProcessing;
    private static DateTime _lastActionTime = DateTime.MinValue;

    // ── 多轮对话状态 ──
    private static readonly List<ChatMessage> _history = new();
    private static string _currentCombatEnemies = "";  // 用于检测新战斗
    private static int _turnNumber;
    private static int _actionInTurn;

    /// <summary>
    /// 处理战斗回合开始 - 循环做决策直到需要结束回合
    /// </summary>
    public static async Task HandleCombatTurn(CombatState combatState, Player player)
    {
        if (!AIConfig.Enabled || _isProcessing) return;
        _isProcessing = true;

        try
        {
            // 提取初始状态检测是否为新战斗
            var initialState = GameStateExtractor.ExtractCombatState(combatState, player);
            var enemySignature = string.Join("|", initialState.Combat!.Enemies.Select(e => e.Name));

            if (enemySignature != _currentCombatEnemies)
            {
                // 新战斗：重置对话历史
                _currentCombatEnemies = enemySignature;
                _history.Clear();
                _history.Add(new ChatMessage("system", PromptBuilder.SystemPrompt));
                _turnNumber = 0;
                Log.Debug("[AISpire] === 新战斗开始，对话历史已重置 ===");
                AIOverlay.AddMessage("新战斗", $"敌人: {enemySignature.Replace("|", ", ")}");
            }

            _turnNumber++;
            _actionInTurn = 0;
            Log.Debug($"[AISpire] === 战斗回合 {_turnNumber} 开始 ===");

            // 循环出牌直到结束回合
            while (true)
            {
                await WaitForActionDelay();

                // 提取最新状态
                var state = GameStateExtractor.ExtractCombatState(combatState, player);

                // 检查是否无牌可打或无能量
                if (state.Combat!.Energy <= 0 ||
                    !state.Combat.Hand.Any(c => c.CanPlay))
                {
                    Log.Debug("[AISpire] 无能量或无可打牌，结束回合");
                    AIOverlay.AddMessage("结束回合", "无能量或无可打牌");
                    ActionExecutor.ExecuteCombatAction(
                        new AIDecision { Action = "end_turn" }, combatState, player);
                    break;
                }

                _actionInTurn++;

                // 获取决策（多轮对话）
                var decision = await GetCombatDecisionMultiTurn(state);
                Log.Debug($"[AISpire] 决策: {decision.Action} | 理由: {decision.Reasoning}");

                // 更新 Overlay
                var actionDesc = decision.Action switch
                {
                    "play_card" => $"出牌[{decision.CardIndex}]",
                    "use_potion" => $"药水[{decision.PotionIndex}]",
                    "end_turn" => "结束回合",
                    _ => decision.Action
                };
                AIOverlay.AddMessage(actionDesc, decision.Reasoning);

                // 执行决策
                if (decision.Action == "end_turn")
                {
                    ActionExecutor.ExecuteCombatAction(decision, combatState, player);
                    break;
                }

                bool success = ActionExecutor.ExecuteCombatAction(decision, combatState, player);
                if (!success)
                {
                    Log.Debug("[AISpire] 决策执行失败，结束回合");
                    AIOverlay.AddMessage("执行失败", "自动结束回合");
                    ActionExecutor.ExecuteCombatAction(
                        new AIDecision { Action = "end_turn" }, combatState, player);
                    break;
                }

                _lastActionTime = DateTime.UtcNow;
                await Task.Delay(300);
            }
        }
        catch (Exception e)
        {
            Log.Debug($"[AISpire] Error in combat turn: {e.Message}\n{e.StackTrace}");
            try
            {
                ActionExecutor.ExecuteCombatAction(
                    new AIDecision { Action = "end_turn" }, combatState, player);
            }
            catch { }
        }
        finally
        {
            _isProcessing = false;
        }
    }

    /// <summary>
    /// 多轮对话式战斗决策
    /// </summary>
    private static async Task<AIDecision> GetCombatDecisionMultiTurn(GameState state)
    {
        // 构建当前状态的 user 消息
        var prompt = PromptBuilder.BuildCombatPrompt(state);
        var prefix = $"[回合{_turnNumber} 第{_actionInTurn}次行动]\n";
        var userMessage = prefix + prompt;

        // 添加 user 消息到历史
        _history.Add(new ChatMessage("user", userMessage));
        TrimHistory();

        // 尝试 LLM
        try
        {
            AIOverlay.SetStatus("正在思考...");
            var (decision, rawText) = await LLMClient.GetDecisionWithHistoryAsync(_history);

            if (decision != null)
            {
                // 将 assistant 回复加入历史
                _history.Add(new ChatMessage("assistant", rawText ?? "{}"));
                Log.Debug("[AISpire] LLM 返回决策（多轮对话）");
                return decision;
            }
        }
        catch (Exception e)
        {
            Log.Debug($"[AISpire] LLM 调用失败: {e.Message}");
        }

        // 兜底：规则引擎
        Log.Debug("[AISpire] 使用规则引擎兜底");
        var fallback = RuleEngine.DecideCombat(state);
        // 将规则引擎决策也加入历史，保持上下文一致
        var fallbackJson = $"{{\"action\":\"{fallback.Action}\",\"card_index\":{fallback.CardIndex},\"target_index\":{fallback.TargetIndex},\"reasoning\":\"{fallback.Reasoning}\"}}";
        _history.Add(new ChatMessage("assistant", fallbackJson));
        return fallback;
    }

    /// <summary>
    /// 处理地图选择
    /// </summary>
    public static async Task HandleMapChoice(IRunState runState, Player player)
    {
        if (!AIConfig.Enabled || _isProcessing) return;
        _isProcessing = true;

        try
        {
            await WaitForActionDelay();

            var state = GameStateExtractor.ExtractMapState(runState, player);
            if (state.Map?.AvailableNodes.Count == 0)
            {
                Log.Debug("[AISpire] 无可用地图节点");
                return;
            }

            var decision = await GetDecision(state, PromptBuilder.BuildMapPrompt);
            Log.Debug($"[AISpire] 地图决策: 节点{decision.MapNodeIndex} | 理由: {decision.Reasoning}");
            AIOverlay.AddMessage("选路", decision.Reasoning);

            ActionExecutor.ExecuteMapMove(decision, runState, player);
            _lastActionTime = DateTime.UtcNow;
        }
        catch (Exception e)
        {
            Log.Debug($"[AISpire] Error in map choice: {e.Message}");
        }
        finally
        {
            _isProcessing = false;
        }
    }

    /// <summary>
    /// 非战斗场景：单次 LLM 调用（无多轮历史）
    /// </summary>
    private static async Task<AIDecision> GetDecision(GameState state, Func<GameState, string> promptBuilder)
    {
        try
        {
            var prompt = promptBuilder(state);
            var decision = await LLMClient.GetDecisionAsync(PromptBuilder.SystemPrompt, prompt);
            if (decision != null)
            {
                Log.Debug("[AISpire] LLM 返回决策");
                return decision;
            }
        }
        catch (Exception e)
        {
            Log.Debug($"[AISpire] LLM 调用失败: {e.Message}");
        }

        Log.Debug("[AISpire] 使用规则引擎兜底");
        return state.Screen switch
        {
            "combat" => RuleEngine.DecideCombat(state),
            "map" => RuleEngine.DecideMap(state),
            "rest" => RuleEngine.DecideRestSite(state),
            "card_selection" => RuleEngine.DecideCardSelection(state),
            "reward" => RuleEngine.DecideReward(state),
            "event" => RuleEngine.DecideEvent(state),
            _ => new AIDecision { Action = "end_turn", Reasoning = "未知场景，兜底结束" }
        };
    }

    /// <summary>
    /// 修剪历史消息，避免超出 token 限制
    /// </summary>
    private static void TrimHistory()
    {
        // 保留 system 消息 + 最近 N 条 user/assistant 消息
        if (_history.Count <= AIConfig.MaxHistoryMessages + 1) return;

        // 保留第一条 system 消息
        var system = _history[0];
        var recent = _history.Skip(_history.Count - AIConfig.MaxHistoryMessages).ToList();
        _history.Clear();
        _history.Add(system);
        _history.AddRange(recent);
        Log.Debug($"[AISpire] 历史消息已修剪至 {_history.Count} 条");
    }

    private static async Task WaitForActionDelay()
    {
        var elapsed = (DateTime.UtcNow - _lastActionTime).TotalMilliseconds;
        if (elapsed < AIConfig.ActionDelayMs)
        {
            await Task.Delay(AIConfig.ActionDelayMs - (int)elapsed);
        }
    }
}
