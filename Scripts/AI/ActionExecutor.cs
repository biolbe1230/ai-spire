using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Entities.Merchant;

namespace AISpire.AI;

public static class ActionExecutor
{
    /// <summary>
    /// 执行战斗中的 AI 决策（async：等待卡牌效果完全结算后再返回）
    /// </summary>
    public static async Task<bool> ExecuteCombatAction(AIDecision decision, CombatState combatState, Player player)
    {
        try
        {
            switch (decision.Action)
            {
                case "play_card":
                    return await ExecutePlayCard(decision, combatState, player);
                case "end_turn":
                    return ExecuteEndTurn(combatState, player);
                case "use_potion":
                    return ExecuteUsePotion(decision, combatState, player);
                default:
                    Log.Info($"[AISpire] Unknown combat action: {decision.Action}");
                    return false;
            }
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error executing action {decision.Action}: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 执行地图移动
    /// </summary>
    public static bool ExecuteMapMove(AIDecision decision, IRunState runState, Player player)
    {
        try
        {
            if (decision.Action != "choose_map") return false;

            var currentPoint = runState.CurrentMapPoint;
            if (currentPoint == null) return false;

            var children = currentPoint.Children.ToList();
            if (decision.MapNodeIndex < 0 || decision.MapNodeIndex >= children.Count)
            {
                Log.Info($"[AISpire] Invalid map node index: {decision.MapNodeIndex}, available: {children.Count}");
                return false;
            }

            var target = children[decision.MapNodeIndex];
            var action = new MoveToMapCoordAction(player, target.coord);
            RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(action);

            Log.Info($"[AISpire] Moving to {target.PointType} at {target.coord}");
            return true;
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error executing map move: {e.Message}");
            return false;
        }
    }

    private static async Task<bool> ExecutePlayCard(AIDecision decision, CombatState combatState, Player player)
    {
        var hand = player.PlayerCombatState.Hand.Cards;
        if (decision.CardIndex < 0 || decision.CardIndex >= hand.Count)
        {
            Log.Info($"[AISpire] Invalid card index: {decision.CardIndex}, hand size: {hand.Count}");
            return false;
        }

        var card = hand[decision.CardIndex];
        if (!card.CanPlay())
        {
            Log.Info($"[AISpire] Card {card.Title} cannot be played");
            return false;
        }

        // 获取目标
        Creature? target = null;
        if (card.TargetType == TargetType.AnyEnemy)
        {
            var enemies = combatState.HittableEnemies;
            if (decision.TargetIndex >= 0 && decision.TargetIndex < enemies.Count)
            {
                target = enemies[decision.TargetIndex];
            }
            else if (enemies.Count > 0)
            {
                target = enemies[0]; // 默认打第一个敌人
            }
            else
            {
                Log.Info("[AISpire] No hittable enemies for targeted card");
                return false;
            }
        }
        else if (card.TargetType == TargetType.AnyAlly)
        {
            var allies = combatState.Allies;
            if (decision.TargetIndex >= 0 && decision.TargetIndex < allies.Count)
            {
                target = allies[decision.TargetIndex];
            }
            else if (allies.Count > 0)
            {
                target = allies[0];
            }
        }

        Log.Info($"[AISpire] Playing card: {card.Title} -> {target?.Name ?? "no target"}");
        await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(), card, target);
        return true;
    }

    private static bool ExecuteEndTurn(CombatState combatState, Player player)
    {
        if (!CombatManager.Instance.IsInProgress || !CombatManager.Instance.IsPlayPhase)
        {
            Log.Info("[AISpire] Cannot end turn: combat not in play phase");
            return false;
        }
        PlayerCmd.EndTurn(player, canBackOut: false);
        Log.Info("[AISpire] Ending turn");
        return true;
    }

    private static bool ExecuteUsePotion(AIDecision decision, CombatState combatState, Player player)
    {
        var potionSlots = player.PotionSlots;
        if (decision.PotionIndex < 0 || decision.PotionIndex >= potionSlots.Count)
        {
            Log.Info($"[AISpire] Invalid potion index: {decision.PotionIndex}");
            return false;
        }

        var potion = potionSlots[decision.PotionIndex];
        if (potion == null)
        {
            Log.Info($"[AISpire] No potion at slot {decision.PotionIndex}");
            return false;
        }

        // 获取目标
        Creature? target = null;
        if (decision.TargetIndex >= 0)
        {
            var enemies = combatState.HittableEnemies;
            if (decision.TargetIndex < enemies.Count)
                target = enemies[decision.TargetIndex];
        }

        var action = new UsePotionAction(potion, target, CombatManager.Instance.IsInProgress);
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(action);

        Log.Info($"[AISpire] Using potion at slot {decision.PotionIndex} -> {target?.Name ?? "no target"}");
        return true;
    }

    // ─── 事件选项 ───

    public static bool ExecuteEventOption(int optionIndex)
    {
        try
        {
            RunManager.Instance.EventSynchronizer.ChooseLocalOption(optionIndex);
            Log.Info($"[AISpire] Chose event option {optionIndex}");
            return true;
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error choosing event option: {e.Message}");
            return false;
        }
    }

    // ─── 营地选项 ───

    public static async Task<bool> ExecuteRestSiteOption(int optionIndex)
    {
        try
        {
            var result = await RunManager.Instance.RestSiteSynchronizer.ChooseLocalOption(optionIndex);
            Log.Info($"[AISpire] Chose rest site option {optionIndex}, result: {result}");
            return result;
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error choosing rest site option: {e.Message}");
            return false;
        }
    }

    // ─── 宝物房遗物选择 ───

    public static bool ExecuteRelicPick(int relicIndex)
    {
        try
        {
            RunManager.Instance.TreasureRoomRelicSynchronizer.PickRelicLocally(relicIndex);
            Log.Info($"[AISpire] Picked relic {relicIndex}");
            return true;
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error picking relic: {e.Message}");
            return false;
        }
    }

    // ─── 商店购买 ───

    public static async Task<bool> ExecuteShopPurchase(int shopItemIndex, MerchantRoom shopRoom)
    {
        try
        {
            var entryObj = GameStateExtractor.GetShopEntry(shopItemIndex);
            if (entryObj == null)
            {
                Log.Info($"[AISpire] Shop entry not found at index {shopItemIndex}");
                return false;
            }

            var inventory = shopRoom.Inventory;

            // MerchantCardRemovalEntry 有独立的 OnTryPurchaseWrapper 重载
            if (entryObj is MerchantCardRemovalEntry removalEntry)
            {
                var result = await removalEntry.OnTryPurchaseWrapper(inventory, ignoreCost: false, cancelable: false);
                Log.Info($"[AISpire] Card removal purchase: {result}");
                return result;
            }

            // 其他类型统一调用基类的 OnTryPurchaseWrapper
            if (entryObj is MerchantEntry merchantEntry)
            {
                var result = await merchantEntry.OnTryPurchaseWrapper(inventory);
                Log.Info($"[AISpire] Shop purchase: {result}");
                return result;
            }

            Log.Info($"[AISpire] Unknown shop entry type: {entryObj.GetType().Name}");
            return false;
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error executing shop purchase: {e.Message}");
            return false;
        }
    }
}
