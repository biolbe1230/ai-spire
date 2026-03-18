using Godot;
using AISpire.Config;
using MegaCrit.Sts2.Core.AutoSlay.Helpers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Runs;

namespace AISpire.AI;

/// <summary>
/// Handles UI screen interactions (rewards, card selection, map, upgrade).
/// Follows the same patterns as the game's built-in AutoSlayer.
/// </summary>
public static class ScreenHandler
{
    private static Node Root => ((SceneTree)Engine.GetMainLoop()).Root;

    // ════════════════════════════════════════════
    //  Rewards Screen
    // ════════════════════════════════════════════

    /// <summary>
    /// Handle the rewards screen: click all reward buttons, handle card selection, click proceed.
    /// </summary>
    public static async Task HandleRewardsScreen(IRunState runState, Player player)
    {
        // Wait for rewards screen
        NRewardsScreen? screen = null;
        for (int i = 0; i < 20; i++)
        {
            screen = UiHelper.FindFirst<NRewardsScreen>(Root);
            if (screen != null) break;
            await Task.Delay(500);
        }
        if (screen == null)
        {
            Log.Info("[AISpire] Rewards screen not found");
            return;
        }

        Log.Info("[AISpire] Rewards screen found, processing...");
        AIOverlay.AddMessage(Loc.PostCombat, Loc.ProcessingRewards);

        var attemptedButtons = new HashSet<NRewardButton>();

        // Loop clicking reward buttons
        while (true)
        {
            await Task.Delay(300);

            var buttons = UiHelper.FindAll<NRewardButton>(screen);
            var btn = buttons.FirstOrDefault(b => b.IsEnabled && !attemptedButtons.Contains(b));
            if (btn == null) break;

            attemptedButtons.Add(btn);
            var rewardType = btn.Reward?.GetType().Name ?? "unknown";
            Log.Info($"[AISpire] Clicking reward: {rewardType}");
            AIOverlay.AddMessage(Loc.TakeReward, rewardType);

            await UiHelper.Click(btn);
            await Task.Delay(600);

            // Check if any overlay opened (card selection, upgrade screen from relic, etc.)
            var overlay = NOverlayStack.Instance?.Peek();
            if (overlay != null && !ReferenceEquals(overlay, screen))
            {
                await Task.Delay(500);
                await DrainOverlayScreens(runState, player);
                await Task.Delay(500);
            }
        }

        // Click proceed button
        var proceedBtn = UiHelper.FindFirst<NProceedButton>(screen);
        if (proceedBtn != null)
        {
            Log.Info("[AISpire] Clicking proceed");
            AIOverlay.AddMessage(Loc.Proceed, "");
            await UiHelper.Click(proceedBtn);

            // Wait for screen to close or map to open
            for (int i = 0; i < 30; i++)
            {
                if (!GodotObject.IsInstanceValid(screen) || (NMapScreen.Instance?.IsOpen ?? false))
                    break;
                await Task.Delay(300);
            }
        }
    }

    /// <summary>
    /// Check if NCardRewardSelectionScreen is present and handle it.
    /// </summary>
    private static async Task HandleCardRewardIfPresent(IRunState runState, Player player)
    {
        var cardScreen = UiHelper.FindFirst<NCardRewardSelectionScreen>(Root);
        if (cardScreen != null)
        {
            await HandleCardRewardScreen(cardScreen, runState, player);
        }
    }

    // ════════════════════════════════════════════
    //  Card Reward Selection
    // ════════════════════════════════════════════

    /// <summary>
    /// Handle card reward selection: ask LLM which card to pick, then click it.
    /// </summary>
    public static async Task HandleCardRewardScreen(
        NCardRewardSelectionScreen screen, IRunState runState, Player player)
    {
        Log.Info("[AISpire] Card reward selection screen opened");
        await Task.Delay(400);

        var holders = UiHelper.FindAll<NCardHolder>(screen);
        if (holders.Count == 0)
        {
            Log.Info("[AISpire] No card holders found in card reward screen");
            return;
        }

        // Build card info for LLM decision
        var cardChoices = new List<CardInfo>();
        for (int i = 0; i < holders.Count; i++)
        {
            var cm = holders[i].CardModel;
            if (cm == null) continue;
            try
            {
                cardChoices.Add(new CardInfo
                {
                    Index = i,
                    CardId = cm.Id?.Entry ?? "",
                    Name = cm.Title ?? "?",
                    Description = SafeGetDescription(cm),
                    Cost = SafeGetCost(cm),
                    Type = cm.Type.ToString(),
                    IsUpgraded = cm.IsUpgraded,
                });
            }
            catch (Exception e)
            {
                Log.Info($"[AISpire] Error extracting card info: {e.Message}");
                cardChoices.Add(new CardInfo { Index = i, Name = "?" });
            }
        }

        var state = new GameState
        {
            Screen = "card_selection",
            Floor = runState.TotalFloor,
            ActIndex = runState.CurrentActIndex,
            Player = GameStateExtractor.ExtractPlayerInfo(player),
            CardChoices = cardChoices
        };

        AIOverlay.SetStatus(Loc.AnalyzingCards);
        var decision = await AIDecisionEngine.GetDecisionInternal(state, PromptBuilder.BuildCardSelectionPrompt);

        if (decision.Action == "skip_reward")
        {
            Log.Info($"[AISpire] Card reward: skip | {decision.Reasoning}");
            AIOverlay.AddMessage(Loc.CardReward, string.Format(Loc.SkipFmt, decision.Reasoning));
            // Try to find a skip/back button
            var skipBtn = UiHelper.FindFirst<NBackButton>(screen);
            if (skipBtn != null)
                await UiHelper.Click(skipBtn);
        }
        else
        {
            var idx = decision.CardChoiceIndex;
            if (idx < 0 || idx >= holders.Count) idx = 0;

            var cardName = holders[idx].CardModel?.Title ?? "?";
            Log.Info($"[AISpire] Card reward: pick[{idx}] {cardName} | {decision.Reasoning}");
            AIOverlay.AddMessage(Loc.CardReward, string.Format(Loc.ChooseFmt, cardName, decision.Reasoning));

            holders[idx].EmitSignal(NCardHolder.SignalName.Pressed, holders[idx]);
        }

        // Wait for screen to close
        for (int i = 0; i < 30; i++)
        {
            if (!GodotObject.IsInstanceValid(screen) || !screen.IsVisibleInTree())
                break;
            await Task.Delay(300);
        }
    }

    // ════════════════════════════════════════════
    //  Map Screen
    // ════════════════════════════════════════════

    /// <summary>
    /// Wait for map screen to open, then trigger AI map selection.
    /// </summary>
    public static async Task HandleMapScreen(IRunState runState, Player player)
    {
        // Wait for map screen
        for (int i = 0; i < 20; i++)
        {
            if (NMapScreen.Instance?.IsOpen ?? false) break;
            await Task.Delay(500);
        }

        if (!(NMapScreen.Instance?.IsOpen ?? false))
        {
            Log.Info("[AISpire] Map screen not open after rewards");
            return;
        }

        Log.Info("[AISpire] Map screen open, selecting path...");
        AIOverlay.SetStatus(Loc.SelectingPath);
        await Task.Delay(1000);

        // Use the AI decision engine to choose a path
        await AIDecisionEngine.HandleMapChoiceCore(runState, player);
    }

    // ════════════════════════════════════════════
    //  Deck Upgrade Screen (Rest Site Smith)
    // ════════════════════════════════════════════

    /// <summary>
    /// Handle the card upgrade screen at rest site.
    /// Supports multi-card selection (e.g. relics that upgrade 2+ cards).
    /// </summary>
    public static async Task HandleUpgradeScreen(IRunState runState, Player player)
    {
        // Wait for upgrade screen
        NDeckUpgradeSelectScreen? screen = null;
        for (int i = 0; i < 20; i++)
        {
            screen = UiHelper.FindFirst<NDeckUpgradeSelectScreen>(Root);
            if (screen != null && screen.IsVisibleInTree()) break;
            screen = null;
            await Task.Delay(500);
        }
        if (screen == null)
        {
            Log.Info("[AISpire] Upgrade screen not found");
            return;
        }

        Log.Info("[AISpire] Upgrade screen found, selecting card...");
        AIOverlay.SetStatus(Loc.AnalyzingUpgrade);
        await Task.Delay(500);

        // Find all card holders on the grid
        var cards = UiHelper.FindAll<NGridCardHolder>(screen);
        if (cards.Count == 0)
        {
            Log.Info("[AISpire] No upgradeable cards on screen");
            return;
        }

        // Build card info for LLM decision
        var cardChoices = new List<CardInfo>();
        for (int i = 0; i < cards.Count; i++)
        {
            var cm = cards[i].CardModel;
            if (cm == null) continue;
            try
            {
                cardChoices.Add(new CardInfo
                {
                    Index = i,
                    CardId = cm.Id?.Entry ?? "",
                    Name = cm.Title ?? "?",
                    Description = SafeGetDescription(cm),
                    Cost = SafeGetCost(cm),
                    Type = cm.Type.ToString(),
                    IsUpgraded = cm.IsUpgraded,
                });
            }
            catch
            {
                cardChoices.Add(new CardInfo { Index = i, Name = "?" });
            }
        }

        // Ask LLM which card to upgrade (reuse card selection prompt)
        var state = new GameState
        {
            Screen = "card_selection",
            Floor = runState.TotalFloor,
            ActIndex = runState.CurrentActIndex,
            Player = GameStateExtractor.ExtractPlayerInfo(player),
            CardChoices = cardChoices
        };

        var decision = await AIDecisionEngine.GetDecisionInternal(state, PromptBuilder.BuildCardSelectionPrompt);
        var llmIdx = decision.CardChoiceIndex;
        if (llmIdx < 0 || llmIdx >= cards.Count) llmIdx = 0;

        // Select cards one-by-one until preview appears (supports multi-select mode)
        // LLM's pick goes first, then remaining cards sequentially
        var selectOrder = new List<int> { llmIdx };
        for (int i = 0; i < cards.Count; i++)
        {
            if (i != llmIdx) selectOrder.Add(i);
        }

        Control? visiblePreview = null;
        int maxSelections = Math.Min(cards.Count, 5);

        for (int sel = 0; sel < maxSelections; sel++)
        {
            if (!GodotObject.IsInstanceValid(screen) || !screen.IsVisibleInTree()) return;

            // Check if preview already appeared
            var single = screen.GetNodeOrNull<Control>("%UpgradeSinglePreviewContainer");
            var multi = screen.GetNodeOrNull<Control>("%UpgradeMultiPreviewContainer");
            if (single != null && single.Visible) { visiblePreview = single; break; }
            if (multi != null && multi.Visible) { visiblePreview = multi; break; }

            if (sel >= selectOrder.Count) break;
            var pickIdx = selectOrder[sel];
            var picked = cards[pickIdx];
            var cardName = picked.CardModel?.Title ?? "?";
            Log.Info($"[AISpire] Selecting card for upgrade [{sel}]: {cardName}");
            if (sel == 0) AIOverlay.AddMessage(Loc.Upgrade, cardName);

            picked.EmitSignal(NCardHolder.SignalName.Pressed, picked);
            await Task.Delay(400);
        }

        // Wait for preview to appear (extra patience)
        if (visiblePreview == null)
        {
            for (int i = 0; i < 15; i++)
            {
                var single = screen.GetNodeOrNull<Control>("%UpgradeSinglePreviewContainer");
                var multi = screen.GetNodeOrNull<Control>("%UpgradeMultiPreviewContainer");
                if (single != null && single.Visible) { visiblePreview = single; break; }
                if (multi != null && multi.Visible) { visiblePreview = multi; break; }
                if (!GodotObject.IsInstanceValid(screen) || !screen.IsVisibleInTree()) return;
                await Task.Delay(300);
            }
        }

        if (visiblePreview == null)
        {
            Log.Info("[AISpire] Upgrade preview did not appear");
            return;
        }

        // Click confirm button
        var confirmBtn = visiblePreview.GetNodeOrNull<NConfirmButton>("Confirm");
        if (confirmBtn == null)
        {
            Log.Info("[AISpire] Confirm button not found in upgrade preview");
            return;
        }

        // Wait for confirm to be enabled
        for (int i = 0; i < 15; i++)
        {
            if (confirmBtn.IsEnabled) break;
            await Task.Delay(300);
        }

        await UiHelper.Click(confirmBtn);
        Log.Info("[AISpire] Upgrade confirmed");

        // Wait for screen to close
        for (int i = 0; i < 20; i++)
        {
            if (!GodotObject.IsInstanceValid(screen) || !screen.IsVisibleInTree()) break;
            await Task.Delay(300);
        }
    }

    // ════════════════════════════════════════════
    //  Generic Card Grid Screen (remove/transform/enchant/simple)
    // ════════════════════════════════════════════

    /// <summary>
    /// Handle any NCardGridSelectionScreen subclass (card removal, transformation, enchantment, etc.).
    /// Selects a card via LLM, then finds and clicks the Confirm button.
    /// </summary>
    public static async Task HandleCardGridScreen(NCardGridSelectionScreen screen, IRunState runState, Player player)
    {
        var screenType = screen.GetType().Name;
        Log.Info($"[AISpire] Handling card grid screen: {screenType}");
        AIOverlay.SetStatus(Loc.AnalyzingCards);
        await Task.Delay(500);

        var cards = UiHelper.FindAll<NGridCardHolder>(screen);
        if (cards.Count == 0)
        {
            Log.Info($"[AISpire] No cards on {screenType}");
            return;
        }

        // Build card info for LLM decision
        var cardChoices = new List<CardInfo>();
        for (int i = 0; i < cards.Count; i++)
        {
            var cm = cards[i].CardModel;
            if (cm == null) continue;
            try
            {
                cardChoices.Add(new CardInfo
                {
                    Index = i,
                    CardId = cm.Id?.Entry ?? "",
                    Name = cm.Title ?? "?",
                    Description = SafeGetDescription(cm),
                    Cost = SafeGetCost(cm),
                    Type = cm.Type.ToString(),
                    IsUpgraded = cm.IsUpgraded,
                });
            }
            catch
            {
                cardChoices.Add(new CardInfo { Index = i, Name = "?" });
            }
        }

        var state = new GameState
        {
            Screen = "card_selection",
            Floor = runState.TotalFloor,
            ActIndex = runState.CurrentActIndex,
            Player = GameStateExtractor.ExtractPlayerInfo(player),
            CardChoices = cardChoices
        };

        var decision = await AIDecisionEngine.GetDecisionInternal(state, PromptBuilder.BuildCardSelectionPrompt);
        var idx = decision.CardChoiceIndex;
        if (idx < 0 || idx >= cards.Count) idx = 0;

        var cardName = cards[idx].CardModel?.Title ?? "?";
        Log.Info($"[AISpire] {screenType}: selecting [{idx}] {cardName}");
        AIOverlay.AddMessage(screenType, cardName);

        cards[idx].EmitSignal(NCardHolder.SignalName.Pressed, cards[idx]);
        await Task.Delay(500);

        // Find and click any visible+enabled Confirm button on the screen
        for (int i = 0; i < 20; i++)
        {
            if (!GodotObject.IsInstanceValid(screen) || !screen.IsVisibleInTree()) return;

            var confirms = UiHelper.FindAll<NConfirmButton>(screen);
            foreach (var btn in confirms)
            {
                if (btn.IsVisibleInTree() && btn.IsEnabled)
                {
                    await UiHelper.Click(btn);
                    Log.Info($"[AISpire] Confirmed on {screenType}");
                    for (int j = 0; j < 20; j++)
                    {
                        if (!GodotObject.IsInstanceValid(screen) || !screen.IsVisibleInTree()) return;
                        await Task.Delay(300);
                    }
                    return;
                }
            }
            await Task.Delay(300);
        }
        Log.Info($"[AISpire] No confirm button found on {screenType}");
    }

    // ════════════════════════════════════════════
    //  Relic Choice Screen (boss relic / Neow relic)
    // ════════════════════════════════════════════

    /// <summary>
    /// Handle NChooseARelicSelection: find clickable relics and pick one.
    /// </summary>
    public static async Task HandleRelicChoiceScreen(NChooseARelicSelection screen, IRunState runState, Player player)
    {
        Log.Info("[AISpire] Handling relic choice screen");
        await Task.Delay(500);

        var clickables = UiHelper.FindAll<NClickableControl>(screen);
        if (clickables.Count == 0)
        {
            Log.Info("[AISpire] No clickable relics found");
            return;
        }

        Log.Info($"[AISpire] Selecting relic (found {clickables.Count} options)");
        AIOverlay.AddMessage(Loc.TreasureRoom, Loc.PickRelic);
        await UiHelper.Click(clickables[0]);

        for (int i = 0; i < 20; i++)
        {
            if (!GodotObject.IsInstanceValid(screen) || !screen.IsVisibleInTree()) break;
            await Task.Delay(300);
        }
    }

    // ════════════════════════════════════════════
    //  Choose-a-Card Screen (generic card selection overlay)
    // ════════════════════════════════════════════

    /// <summary>
    /// Handle NChooseACardSelectionScreen: find cards, ask LLM, pick one.
    /// </summary>
    public static async Task HandleChooseCardScreen(NChooseACardSelectionScreen screen, IRunState runState, Player player)
    {
        Log.Info("[AISpire] Handling choose-a-card screen");
        await Task.Delay(500);

        var holders = UiHelper.FindAll<NCardHolder>(screen);
        if (holders.Count == 0)
        {
            Log.Info("[AISpire] No cards on choose-a-card screen, trying fallback");
            await TryDismissOverlay(screen);
            return;
        }

        var cardChoices = new List<CardInfo>();
        for (int i = 0; i < holders.Count; i++)
        {
            var cm = holders[i].CardModel;
            if (cm == null) continue;
            try
            {
                cardChoices.Add(new CardInfo
                {
                    Index = i,
                    CardId = cm.Id?.Entry ?? "",
                    Name = cm.Title ?? "?",
                    Description = SafeGetDescription(cm),
                    Cost = SafeGetCost(cm),
                    Type = cm.Type.ToString(),
                    IsUpgraded = cm.IsUpgraded,
                });
            }
            catch
            {
                cardChoices.Add(new CardInfo { Index = i, Name = "?" });
            }
        }

        var state = new GameState
        {
            Screen = "card_selection",
            Floor = runState.TotalFloor,
            ActIndex = runState.CurrentActIndex,
            Player = GameStateExtractor.ExtractPlayerInfo(player),
            CardChoices = cardChoices
        };

        AIOverlay.SetStatus(Loc.AnalyzingCards);
        var decision = await AIDecisionEngine.GetDecisionInternal(state, PromptBuilder.BuildCardSelectionPrompt);

        if (decision.Action == "skip_reward")
        {
            Log.Info($"[AISpire] Choose-a-card: skip | {decision.Reasoning}");
            AIOverlay.AddMessage(Loc.CardReward, string.Format(Loc.SkipFmt, decision.Reasoning));
            var skipBtn = UiHelper.FindFirst<NBackButton>(screen);
            if (skipBtn != null)
                await UiHelper.Click(skipBtn);
        }
        else
        {
            var idx = decision.CardChoiceIndex;
            if (idx < 0 || idx >= holders.Count) idx = 0;

            var cardName = holders[idx].CardModel?.Title ?? "?";
            Log.Info($"[AISpire] Choose-a-card: pick[{idx}] {cardName} | {decision.Reasoning}");
            AIOverlay.AddMessage(Loc.CardReward, string.Format(Loc.ChooseFmt, cardName, decision.Reasoning));

            holders[idx].EmitSignal(NCardHolder.SignalName.Pressed, holders[idx]);
        }

        for (int i = 0; i < 30; i++)
        {
            if (!GodotObject.IsInstanceValid(screen) || !screen.IsVisibleInTree()) break;
            await Task.Delay(300);
        }
    }

    // ════════════════════════════════════════════
    //  Fallback: Try to dismiss any unknown overlay
    // ════════════════════════════════════════════

    /// <summary>
    /// Attempt to dismiss an unknown overlay by finding and clicking
    /// Confirm / Proceed / Skip / Close buttons, in priority order.
    /// </summary>
    private static async Task TryDismissOverlay(Node screen)
    {
        var typeName = screen.GetType().Name;
        Log.Info($"[AISpire] Attempting to dismiss unknown overlay: {typeName}");
        await Task.Delay(500);

        // Priority 1: NConfirmButton
        var confirm = UiHelper.FindFirst<NConfirmButton>(screen);
        if (confirm != null && confirm.IsVisibleInTree())
        {
            for (int i = 0; i < 10; i++) { if (confirm.IsEnabled) break; await Task.Delay(300); }
            if (confirm.IsEnabled) { await UiHelper.Click(confirm); goto waitClose; }
        }

        // Priority 2: NProceedButton
        var proceed = UiHelper.FindFirst<NProceedButton>(screen);
        if (proceed != null && proceed.IsVisibleInTree())
        {
            for (int i = 0; i < 10; i++) { if (proceed.IsEnabled) break; await Task.Delay(300); }
            if (proceed.IsEnabled) { await UiHelper.Click(proceed); goto waitClose; }
        }

        // Priority 3: NBackButton (close/skip)
        var back = UiHelper.FindFirst<NBackButton>(screen);
        if (back != null && back.IsVisibleInTree())
        {
            await UiHelper.Click(back); goto waitClose;
        }

        // Priority 4: Any clickable NClickableControl child
        var anyBtn = UiHelper.FindAll<NClickableControl>(screen)
            .FirstOrDefault(b => b.IsVisibleInTree());
        if (anyBtn != null)
        {
            await UiHelper.Click(anyBtn); goto waitClose;
        }

        Log.Info($"[AISpire] Could not dismiss overlay: {typeName}");
        return;

        waitClose:
        Log.Info($"[AISpire] Clicked button to dismiss: {typeName}");
        for (int i = 0; i < 20; i++)
        {
            var ctrl = screen as Control;
            if (!GodotObject.IsInstanceValid(screen) || (ctrl != null && !ctrl.IsVisibleInTree())) break;
            await Task.Delay(300);
        }
    }

    // ════════════════════════════════════════════
    //  Generic Overlay Drain (like AutoSlayer's DrainOverlayScreensAsync)
    // ════════════════════════════════════════════

    /// <summary>
    /// Drain all overlay screens (upgrade, card selection, removal, transform, relic choice, etc.)
    /// Called after actions that may trigger relic AfterObtained or other overlays.
    /// </summary>
    public static async Task DrainOverlayScreens(IRunState runState, Player player)
    {
        var handledScreens = new HashSet<object>();

        for (int iter = 0; iter < 10; iter++)
        {
            var stack = NOverlayStack.Instance;
            if (stack == null || stack.ScreenCount <= 0) break;

            var current = stack.Peek();
            if (current == null) break;
            if (handledScreens.Contains(current)) break;
            handledScreens.Add(current);

            var node = (Node)current;
            var typeName = node.GetType().Name;
            Log.Info($"[AISpire] Draining overlay: {typeName}");

            // Specific handlers first (order matters: specific before base class)
            if (current is NDeckUpgradeSelectScreen)
                await HandleUpgradeScreen(runState, player);
            else if (current is NCardGridSelectionScreen gridScreen)
                await HandleCardGridScreen(gridScreen, runState, player);
            else if (current is NCardRewardSelectionScreen cardScreen)
                await HandleCardRewardScreen(cardScreen, runState, player);
            else if (current is NChooseACardSelectionScreen chooseCardScreen)
                await HandleChooseCardScreen(chooseCardScreen, runState, player);
            else if (current is NChooseARelicSelection relicScreen)
                await HandleRelicChoiceScreen(relicScreen, runState, player);
            else if (current is NRewardsScreen)
                await HandleRewardsScreen(runState, player);
            // Fallback: try to dismiss any other overlay
            else
                await TryDismissOverlay(node);

            await Task.Delay(300);
        }
    }

    // ════════════════════════════════════════════
    //  Universal Cleanup: Click Proceed Button
    // ════════════════════════════════════════════

    /// <summary>
    /// After all overlays are drained, find and click any remaining ProceedButton in the room.
    /// </summary>
    public static async Task TryClickProceedButton()
    {
        // Don't click proceed if overlays are still present
        var stack = NOverlayStack.Instance;
        if (stack != null && stack.ScreenCount > 0) return;

        var root = ((SceneTree)Engine.GetMainLoop()).Root;

        // Brief wait for button to become available after overlay drain
        for (int attempt = 0; attempt < 6; attempt++)
        {
            var btn = UiHelper.FindAll<NProceedButton>(root)
                .FirstOrDefault(b => b.IsVisibleInTree() && b.IsEnabled);
            if (btn != null)
            {
                Log.Info("[AISpire] Clicking proceed button (cleanup)");
                await UiHelper.Click(btn);
                return;
            }
            await Task.Delay(500);
        }
    }

    // ════════════════════════════════════════════
    //  Helpers
    // ════════════════════════════════════════════

    private static string SafeGetDescription(dynamic cm)
    {
        try { return cm.Description?.GetFormattedText() ?? ""; }
        catch { return ""; }
    }

    private static int SafeGetCost(dynamic cm)
    {
        try
        {
            if ((bool)cm.EnergyCost.CostsX) return -1;
            return (int)cm.EnergyCost.GetWithModifiers(MegaCrit.Sts2.Core.Entities.Cards.CostModifiers.All);
        }
        catch { return 0; }
    }
}
