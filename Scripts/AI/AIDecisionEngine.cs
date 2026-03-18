using Godot;
using AISpire.Config;
using MegaCrit.Sts2.Core.AutoSlay.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.GameInfo.Objects;

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
                Log.Info("[AISpire] === 新战斗开始，对话历史已重置 ===");
                AIOverlay.AddMessage("新战斗", $"敌人: {enemySignature.Replace("|", ", ")}");
            }

            _turnNumber++;
            _actionInTurn = 0;
            Log.Info($"[AISpire] === 战斗回合 {_turnNumber} 开始 ===");

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
                    Log.Info("[AISpire] 无能量或无可打牌，结束回合");
                    AIOverlay.AddMessage("结束回合", "无能量或无可打牌");
                    await ActionExecutor.ExecuteCombatAction(
                        new AIDecision { Action = "end_turn" }, combatState, player);
                    break;
                }

                _actionInTurn++;

                // 获取决策（多轮对话）
                var decision = await GetCombatDecisionMultiTurn(state);
                Log.Info($"[AISpire] 决策: {decision.Action} | 理由: {decision.Reasoning}");

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
                    await ActionExecutor.ExecuteCombatAction(decision, combatState, player);
                    break;
                }

                bool success = await ActionExecutor.ExecuteCombatAction(decision, combatState, player);
                if (!success)
                {
                    Log.Info("[AISpire] 决策执行失败，结束回合");
                    AIOverlay.AddMessage("执行失败", "自动结束回合");
                    await ActionExecutor.ExecuteCombatAction(
                        new AIDecision { Action = "end_turn" }, combatState, player);
                    break;
                }

                // 检查战斗是否已结束或不在出牌阶段
                if (!CombatManager.Instance.IsInProgress || !CombatManager.Instance.IsPlayPhase)
                {
                    Log.Info("[AISpire] 战斗已结束或不在出牌阶段，停止出牌");
                    break;
                }

                _lastActionTime = DateTime.UtcNow;
                await Task.Delay(300);
            }
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error in combat turn: {e.Message}\n{e.StackTrace}");
            try
            {
                await ActionExecutor.ExecuteCombatAction(
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
                Log.Info("[AISpire] LLM 返回决策（多轮对话）");
                return decision;
            }
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] LLM 调用失败: {e.Message}");
        }

        // 兜底：规则引擎
        Log.Info("[AISpire] 使用规则引擎兜底");
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
            await HandleMapChoiceCore(runState, player);
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error in map choice: {e.Message}");
        }
        finally
        {
            _isProcessing = false;
        }
    }

    /// <summary>
    /// 地图选择核心逻辑 (no _isProcessing guard, called from HandlePostCombat)
    /// </summary>
    public static async Task HandleMapChoiceCore(IRunState runState, Player player)
    {
        var state = GameStateExtractor.ExtractMapState(runState, player);
        if (state.Map?.AvailableNodes.Count == 0)
        {
            Log.Info("[AISpire] No available map nodes");
            return;
        }

        var decision = await GetDecision(state, PromptBuilder.BuildMapPrompt);
        Log.Info($"[AISpire] Map decision: node{decision.MapNodeIndex} | reason: {decision.Reasoning}");
        AIOverlay.AddMessage(Loc.PathChoice, decision.Reasoning);

        var mapScreen = NMapScreen.Instance;
        if (mapScreen == null || !mapScreen.IsOpen)
        {
            var root = ((SceneTree)Engine.GetMainLoop()).Root;
            mapScreen = root.GetNodeOrNull<NMapScreen>("/root/Game/RootSceneContainer/Run/MapScreen");
        }

        if (mapScreen == null || !mapScreen.IsOpen)
        {
            Log.Info("[AISpire] Map screen not open, falling back to action");
            ActionExecutor.ExecuteMapMove(decision, runState, player);
            _lastActionTime = DateTime.UtcNow;
            return;
        }

        var points = UiHelper.FindAll<NMapPoint>(mapScreen);
        var available = state.Map!.AvailableNodes;
        var idx = decision.MapNodeIndex;
        if (idx < 0 || idx >= available.Count) idx = 0;

        var targetCoord = available[idx].Coord;
        var targetPoint = points.FirstOrDefault(p => p.Point.coord.ToString() == targetCoord);

        if (targetPoint != null)
        {
            Log.Info($"[AISpire] Clicking map point at {targetCoord}");
            await UiHelper.Click(targetPoint);
        }
        else
        {
            Log.Info($"[AISpire] NMapPoint for {targetCoord} not found, falling back to action");
            ActionExecutor.ExecuteMapMove(decision, runState, player);
        }
        _lastActionTime = DateTime.UtcNow;
    }

    /// <summary>
    /// 非战斗场景：单次 LLM 调用（无多轮历史）
    /// </summary>
    public static async Task<AIDecision> GetDecision(GameState state, Func<GameState, string> promptBuilder)
    {
        try
        {
            var prompt = promptBuilder(state);
            var decision = await LLMClient.GetDecisionAsync(PromptBuilder.SystemPrompt, prompt);
            if (decision != null)
            {
                Log.Info("[AISpire] LLM 返回决策");
                return decision;
            }
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] LLM 调用失败: {e.Message}");
        }

        Log.Info("[AISpire] 使用规则引擎兜底");
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

    // ─── 战后处理 ───

    public static async Task HandlePostCombat(IRunState runState, Player player)
    {
        if (!AIConfig.Enabled || _isProcessing) return;
        _isProcessing = true;

        try
        {
            // 1) 处理奖励画面
            await ScreenHandler.HandleRewardsScreen(runState, player);

            // 1.5) Drain any remaining overlays (e.g. relic triggered upgrade screen)
            await ScreenHandler.DrainOverlayScreens(runState, player);

            // 2) 等待并处理地图选路
            await Task.Delay(1000);
            await ScreenHandler.HandleMapScreen(runState, player);
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] HandlePostCombat error: {e.Message}\n{e.StackTrace}");
        }
        finally
        {
            _isProcessing = false;
        }
    }

    // ─── 房间入口分发 ───

    public static async Task HandleRoomEntered(AbstractRoom room, IRunState runState, Player player)
    {
        if (!AIConfig.Enabled || _isProcessing) return;

        try
        {
            // 等待场景初始化完成
            await Task.Delay(1500);

            switch (room)
            {
                case MapRoom:
                    await HandleMapChoice(runState, player);
                    break;
                case EventRoom eventRoom:
                    await HandleEvent(eventRoom, runState, player);
                    break;
                case RestSiteRoom restRoom:
                    await HandleRestSite(restRoom, runState, player);
                    break;
                case MerchantRoom shopRoom:
                    await HandleShop(shopRoom, runState, player);
                    break;
                case TreasureRoom:
                    await HandleTreasure(runState, player);
                    break;
                default:
                    Log.Info($"[AISpire] Unhandled room: {room.GetType().Name}");
                    break;
            }

            // ── Universal cleanup after every room handler ──
            await Task.Delay(500);
            await ScreenHandler.DrainOverlayScreens(runState, player);

            // 先尝试事件的 IsProceed 按钮（NEventOptionButton）
            await ScreenHandler.ClickEventProceedIfNeeded();

            // 再尝试通用的 NProceedButton
            await ScreenHandler.TryClickProceedButton();

            await Task.Delay(500);
            if (NMapScreen.Instance?.IsOpen ?? false)
            {
                await ScreenHandler.HandleMapScreen(runState, player);
            }
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] HandleRoomEntered error: {e.Message}\n{e.StackTrace}");
        }
    }

    // ─── 事件 ───

    public static async Task HandleEvent(EventRoom eventRoom, IRunState runState, Player player)
    {
        if (_isProcessing) return;
        _isProcessing = true;

        try
        {
            var root = ((SceneTree)Engine.GetMainLoop()).Root;
            var eventNode = root.GetNodeOrNull("/root/Game/RootSceneContainer/Run/RoomContainer/EventRoom");
            if (eventNode == null)
            {
                Log.Info("[AISpire] Event room node not found");
                return;
            }

            for (int round = 0; round < 10; round++)
            {
                await Task.Delay(500);

                // 检查地图是否已打开（说明事件已完成）
                if (NMapScreen.Instance?.IsOpen ?? false)
                {
                    Log.Info("[AISpire] Map already open, event complete");
                    break;
                }

                var buttons = UiHelper.FindAll<NEventOptionButton>(eventNode)
                    .Where(b => b.IsVisibleInTree() && b.IsEnabled).ToList();

                if (buttons.Count == 0)
                {
                    var proceedBtn = UiHelper.FindFirst<NProceedButton>(eventNode);
                    if (proceedBtn != null && proceedBtn.IsEnabled)
                    {
                        Log.Info("[AISpire] Clicking event proceed (NProceedButton)");
                        await UiHelper.Click(proceedBtn);
                        await Task.Delay(500);
                        break;
                    }

                    Log.Info("[AISpire] No event options available, exiting");
                    break;
                }

                // 优先检查 IsProceed 按钮（事件结束的"离开"按钮），直接点击不需要问 LLM
                var proceedEventBtn = buttons.FirstOrDefault(b => !b.Option.IsLocked && b.Option.IsProceed);
                if (proceedEventBtn != null)
                {
                    Log.Info("[AISpire] Clicking event proceed option (IsProceed)");
                    await UiHelper.Click(proceedEventBtn);
                    await Task.Delay(500);
                    break;
                }

                var state = GameStateExtractor.ExtractEventState(eventRoom, runState, player);
                if (state.EventOptions == null || state.EventOptions.Count == 0)
                {
                    Log.Info("[AISpire] Event has no extractable options");
                    break;
                }

                AIOverlay.SetStatus(Loc.AnalyzingEvent);
                var decision = await GetDecision(state, PromptBuilder.BuildEventPrompt);

                var idx = decision.EventOptionIndex;
                if (idx < 0 || idx >= buttons.Count) idx = 0;

                var title = buttons[idx].GetNodeOrNull<Label>("Label")?.Text ?? $"Option {idx}";
                Log.Info($"[AISpire] Event decision: option{idx} '{title}' | reason: {decision.Reasoning}");
                AIOverlay.AddMessage(Loc.EventChoice, $"{title}: {decision.Reasoning}");

                await UiHelper.Click(buttons[idx]);
                await Task.Delay(1000);

                // 处理事件选项触发的覆盖屏幕（升级、卡牌选择等）
                var overlayCheck = NOverlayStack.Instance;
                if (overlayCheck != null && overlayCheck.ScreenCount > 0)
                {
                    Log.Info("[AISpire] Overlay opened during event, draining screens");
                    await ScreenHandler.DrainOverlayScreens(runState, player);
                    // continue 而不是 break，让循环继续找到事件的"离开"按钮
                    continue;
                }
            }
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] HandleEvent error: {e.Message}");
        }
        finally
        {
            _isProcessing = false;
        }
    }

    // ─── 营地 ───

    public static async Task HandleRestSite(RestSiteRoom restRoom, IRunState runState, Player player)
    {
        if (_isProcessing) return;
        _isProcessing = true;

        try
        {
            var root = ((SceneTree)Engine.GetMainLoop()).Root;
            var restNode = root.GetNodeOrNull("/root/Game/RootSceneContainer/Run/RoomContainer/RestSiteRoom");
            if (restNode == null)
            {
                Log.Info("[AISpire] NRestSiteRoom not found");
                return;
            }

            var restBtns = UiHelper.FindAll<NRestSiteButton>(restNode)
                .Where(b => b.IsVisibleInTree() && b.IsEnabled).ToList();
            if (restBtns.Count == 0)
            {
                Log.Info("[AISpire] No enabled rest site buttons");
                return;
            }

            var state = GameStateExtractor.ExtractRestSiteState(restRoom, runState, player);
            AIOverlay.SetStatus(Loc.AnalyzingCamp);
            var decision = await GetDecision(state, PromptBuilder.BuildRestSitePrompt);

            var restIdx = decision.EventOptionIndex;
            if (restIdx < 0 || restIdx >= restBtns.Count) restIdx = 0;

            var optName = restBtns[restIdx].Name;
            Log.Info($"[AISpire] Camp decision: {optName} (idx:{restIdx}) | reason: {decision.Reasoning}");
            AIOverlay.AddMessage(Loc.Camp, $"{optName}: {decision.Reasoning}");

            await UiHelper.Click(restBtns[restIdx]);
            await Task.Delay(1500);

            // If overlay opened (upgrade screen from Smith), handle it
            var overlayCheck = NOverlayStack.Instance;
            if (overlayCheck != null && overlayCheck.ScreenCount > 0)
            {
                Log.Info("[AISpire] Overlay opened at rest site, draining screens");
                await ScreenHandler.DrainOverlayScreens(runState, player);
                var proceedBtn = UiHelper.FindFirst<NProceedButton>(restNode);
                for (int i = 0; i < 20; i++)
                {
                    if (proceedBtn != null && proceedBtn.IsEnabled) break;
                    await Task.Delay(500);
                }
            }

            // Click proceed button
            var proceed = UiHelper.FindFirst<NProceedButton>(restNode);
            if (proceed != null)
            {
                for (int i = 0; i < 20; i++)
                {
                    if (proceed.IsEnabled) break;
                    await Task.Delay(500);
                }
                if (proceed.IsEnabled)
                {
                    Log.Info("[AISpire] Clicking rest site proceed");
                    await UiHelper.Click(proceed);
                }
            }

            _lastActionTime = DateTime.UtcNow;

            // Handle map
            await Task.Delay(1000);
            if (NMapScreen.Instance?.IsOpen ?? false)
            {
                await HandleMapChoiceCore(runState, player);
            }
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] HandleRestSite error: {e.Message}");
        }
        finally
        {
            _isProcessing = false;
        }
    }

    // ─── 商店 ───

    public static async Task HandleShop(MerchantRoom shopRoom, IRunState runState, Player player)
    {
        if (_isProcessing) return;
        _isProcessing = true;

        try
        {
            var root = ((SceneTree)Engine.GetMainLoop()).Root;
            var shopNode = root.GetNodeOrNull("/root/Game/RootSceneContainer/Run/RoomContainer/MerchantRoom");
            if (shopNode == null)
            {
                Log.Info("[AISpire] NMerchantRoom not found");
                return;
            }

            for (int shopRound = 0; shopRound < 10; shopRound++)
            {
                await Task.Delay(500);

                var state = GameStateExtractor.ExtractShopState(shopRoom, runState, player);
                if (state.ShopItems == null || state.ShopItems.Count == 0)
                {
                    Log.Info("[AISpire] Shop empty");
                    break;
                }

                var gold = state.Player?.Gold ?? 0;
                if (!state.ShopItems!.Any(i => i.Price <= gold))
                {
                    Log.Info("[AISpire] No affordable items");
                    AIOverlay.AddMessage(Loc.Shop, Loc.NoAffordable);
                    break;
                }

                AIOverlay.SetStatus(Loc.AnalyzingShop);
                var decision = await GetDecision(state, PromptBuilder.BuildShopPrompt);

                if (decision.Action == "leave_shop")
                {
                    Log.Info($"[AISpire] Shop decision: leave | reason: {decision.Reasoning}");
                    AIOverlay.AddMessage(Loc.Shop, string.Format(Loc.LeaveFmt, decision.Reasoning));
                    break;
                }

                var itemIdx = decision.ShopItemIndex;
                if (itemIdx < 0 || itemIdx >= state.ShopItems!.Count)
                {
                    Log.Info($"[AISpire] Shop decision: invalid index {itemIdx}");
                    break;
                }

                var item = state.ShopItems[itemIdx];
                if (item.Price > gold)
                {
                    Log.Info($"[AISpire] Shop: insufficient gold for {item.Name}");
                    AIOverlay.AddMessage(Loc.Shop, string.Format(Loc.InsufficientGoldFmt, item.Name));
                    break;
                }
                Log.Info($"[AISpire] Shop decision: buy[{itemIdx}] {item.Name} ({item.Price}g) | reason: {decision.Reasoning}");
                AIOverlay.AddMessage(Loc.Shop, string.Format(Loc.BuyFmt, item.Name, item.Price, decision.Reasoning));

                var purchaseOk = await ActionExecutor.ExecuteShopPurchase(itemIdx, shopRoom);
                if (!purchaseOk)
                {
                    Log.Info("[AISpire] Shop purchase failed");
                    break;
                }
                _lastActionTime = DateTime.UtcNow;
                await Task.Delay(500);

                // 购买后可能弹出覆盖屏幕（如移除卡牌、升级卡牌等），需要处理
                var overlayAfterPurchase = NOverlayStack.Instance;
                if (overlayAfterPurchase != null && overlayAfterPurchase.ScreenCount > 0)
                {
                    Log.Info("[AISpire] Overlay opened after shop purchase, draining screens");
                    await ScreenHandler.DrainOverlayScreens(runState, player);
                    await Task.Delay(500);
                }
            }

            // Click back button to leave shop
            var backBtn = UiHelper.FindFirst<NBackButton>(shopNode);
            if (backBtn != null && backBtn.IsEnabled)
            {
                Log.Info("[AISpire] Clicking shop back button");
                await UiHelper.Click(backBtn);
                await Task.Delay(500);
            }

            // Wait for map or proceed
            for (int i = 0; i < 10; i++)
            {
                if (NMapScreen.Instance?.IsOpen ?? false) break;
                var proceed = UiHelper.FindFirst<NProceedButton>(shopNode);
                if (proceed != null && proceed.IsEnabled)
                {
                    Log.Info("[AISpire] Clicking shop proceed");
                    await UiHelper.Click(proceed);
                    await Task.Delay(500);
                    break;
                }
                await Task.Delay(500);
            }

            _lastActionTime = DateTime.UtcNow;

            await Task.Delay(1000);
            if (NMapScreen.Instance?.IsOpen ?? false)
            {
                await HandleMapChoiceCore(runState, player);
            }
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] HandleShop error: {e.Message}");
        }
        finally
        {
            _isProcessing = false;
        }
    }

    // ─── 宝物房 ───

    public static async Task HandleTreasure(IRunState runState, Player player)
    {
        if (_isProcessing) return;
        _isProcessing = true;

        try
        {
            var root = ((SceneTree)Engine.GetMainLoop()).Root;

            NTreasureRoom? roomNode = null;
            for (int i = 0; i < 20; i++)
            {
                roomNode = UiHelper.FindFirst<NTreasureRoom>(root);
                if (roomNode != null) break;
                await Task.Delay(500);
            }
            if (roomNode == null)
            {
                Log.Info("[AISpire] NTreasureRoom not found");
                return;
            }

            // 点击宝箱打开
            var chestBtn = roomNode.GetNodeOrNull<NButton>("%Chest");
            if (chestBtn != null)
            {
                Log.Info("[AISpire] Clicking treasure chest");
                AIOverlay.AddMessage(Loc.TreasureRoom, Loc.PickRelic);
                await UiHelper.Click(chestBtn);
            }

            // 等待遗物出现并点击
            NTreasureRoomRelicHolder? relicHolder = null;
            for (int i = 0; i < 30; i++)
            {
                relicHolder = UiHelper.FindFirst<NTreasureRoomRelicHolder>(roomNode);
                if (relicHolder != null && relicHolder.IsVisibleInTree()) break;
                relicHolder = null;
                await Task.Delay(500);
            }

            if (relicHolder != null)
            {
                await Task.Delay(500);
                Log.Info("[AISpire] Clicking relic holder");
                await UiHelper.Click(relicHolder);
                await Task.Delay(500);
            }
            else
            {
                Log.Info("[AISpire] No relic holder found (empty chest?)");
            }

            // Drain overlay screens (e.g. relic AfterObtained triggers upgrade screen)
            await ScreenHandler.DrainOverlayScreens(runState, player);

            // 等待 ProceedButton 启用并点击
            var proceedBtn = roomNode.ProceedButton;
            if (proceedBtn != null)
            {
                for (int i = 0; i < 30; i++)
                {
                    if (proceedBtn.IsEnabled) break;
                    await Task.Delay(500);
                }

                if (proceedBtn.IsEnabled)
                {
                    Log.Info("[AISpire] Clicking treasure proceed");
                    AIOverlay.AddMessage(Loc.TreasureRoom, Loc.Proceed);
                    await UiHelper.Click(proceedBtn);
                }
            }

            _lastActionTime = DateTime.UtcNow;

            await Task.Delay(1000);
            if (NMapScreen.Instance?.IsOpen ?? false)
            {
                await HandleMapChoiceCore(runState, player);
            }
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] HandleTreasure error: {e.Message}");
        }
        finally
        {
            _isProcessing = false;
        }
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
        Log.Info($"[AISpire] 历史消息已修剪至 {_history.Count} 条");
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
