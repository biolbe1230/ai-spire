using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Map;

namespace AISpire.AI;

public static class ActionExecutor
{
    /// <summary>
    /// 执行战斗中的 AI 决策
    /// </summary>
    public static bool ExecuteCombatAction(AIDecision decision, CombatState combatState, Player player)
    {
        try
        {
            switch (decision.Action)
            {
                case "play_card":
                    return ExecutePlayCard(decision, combatState, player);
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

    private static bool ExecutePlayCard(AIDecision decision, CombatState combatState, Player player)
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

        var action = new PlayCardAction(card, target);
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(action);

        Log.Info($"[AISpire] Playing card: {card.Title} -> {target?.Name ?? "no target"}");
        return true;
    }

    private static bool ExecuteEndTurn(CombatState combatState, Player player)
    {
        var action = new EndPlayerTurnAction(player, combatState.RoundNumber);
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(action);

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

    /// <summary>
    /// 执行商店购买
    /// </summary>
    public static bool ExecuteShopPurchase(int itemIndex, IRunState runState, Player player)
    {
        try
        {
            var currentRoom = runState.CurrentRoom;
            if (currentRoom is not MerchantRoom shopRoom)
            {
                Log.Info("[AISpire] Not in shop room");
                return false;
            }

            var entries = shopRoom.Inventory.AllEntries.ToList();
            // Filter to in-stock only
            var stocked = entries.Where(e => e.IsStocked).ToList();
            if (itemIndex < 0 || itemIndex >= stocked.Count)
            {
                Log.Info($"[AISpire] Invalid shop item index: {itemIndex}, stocked: {stocked.Count}");
                return false;
            }

            var entry = stocked[itemIndex];
            if (!entry.EnoughGold)
            {
                Log.Info($"[AISpire] Not enough gold for item at index {itemIndex}");
                return false;
            }

            entry.OnTryPurchaseWrapper(shopRoom.Inventory).GetAwaiter().GetResult();
            Log.Info($"[AISpire] Purchased shop item at index {itemIndex}");
            return true;
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error executing shop purchase: {e.Message}");
            return false;
        }
    }
}
