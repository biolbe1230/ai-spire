namespace AISpire.AI;

/// <summary>
/// 规则引擎 - 当 LLM 不可用或超时时的兜底策略
/// </summary>
public static class RuleEngine
{
    public static AIDecision DecideCombat(GameState state)
    {
        var combat = state.Combat!;
        var hand = combat.Hand;
        var enemies = combat.Enemies;

        if (hand.Count == 0 || combat.Energy <= 0)
            return EndTurn();

        // 找出可打出的牌
        var playable = hand.Where(c => c.CanPlay).ToList();
        if (playable.Count == 0)
            return EndTurn();

        // 计算总受到的伤害
        int incomingDamage = enemies.Sum(e =>
            e.Intent == "Attack" ? e.IntentDamage * Math.Max(1, e.IntentHits) : 0);
        int effectiveIncoming = Math.Max(0, incomingDamage - combat.Block);

        // 策略1: 如果能击杀唯一敌人，优先输出
        if (enemies.Count == 1)
        {
            var target = enemies[0];
            int targetEffectiveHp = target.Hp + target.Block;
            var attacks = playable.Where(c => c.Type == "Attack").OrderByDescending(c => EstimateDamage(c)).ToList();

            // 看看能否一回合击杀
            int totalAttackDamage = 0;
            var killPlan = new List<CardInfo>();
            int energyRemaining = combat.Energy;
            foreach (var atk in attacks)
            {
                if (energyRemaining >= atk.Cost)
                {
                    killPlan.Add(atk);
                    totalAttackDamage += EstimateDamage(atk);
                    energyRemaining -= atk.Cost;
                    if (totalAttackDamage >= targetEffectiveHp)
                        break;
                }
            }

            if (totalAttackDamage >= targetEffectiveHp && killPlan.Count > 0)
            {
                var card = killPlan[0];
                return PlayCard(card.Index, card.Target == "AnyEnemy" ? 0 : -1,
                    $"击杀 {target.Name}");
            }
        }

        // 策略2: 受到高伤害时优先防御
        if (effectiveIncoming > combat.MaxHp * 0.2)
        {
            var defenseCards = playable
                .Where(c => c.Type == "Skill" && (c.Target == "None" || c.Target == "Self"))
                .OrderByDescending(c => EstimateBlock(c))
                .ToList();

            if (defenseCards.Count > 0)
            {
                var card = defenseCards[0];
                return PlayCard(card.Index, -1, $"防御，预计受到 {effectiveIncoming} 伤害");
            }
        }

        // 策略3: 优先出 Power 牌
        var powers = playable.Where(c => c.Type == "Power").ToList();
        if (powers.Count > 0)
        {
            var card = powers[0];
            return PlayCard(card.Index, card.Target == "AnyEnemy" ? 0 : -1, "打出Power牌");
        }

        // 策略4: 出伤害最高的攻击牌
        var bestAttack = playable
            .Where(c => c.Type == "Attack")
            .OrderByDescending(c => EstimateDamage(c))
            .FirstOrDefault();

        if (bestAttack != null)
        {
            int targetIdx = bestAttack.Target == "AnyEnemy" ? FindBestTarget(enemies) : -1;
            return PlayCard(bestAttack.Index, targetIdx, "输出攻击");
        }

        // 策略5: 出任何可打的牌
        var anyCard = playable.FirstOrDefault();
        if (anyCard != null)
        {
            int targetIdx = anyCard.Target == "AnyEnemy" ? FindBestTarget(enemies) : -1;
            return PlayCard(anyCard.Index, targetIdx, "打出可用牌");
        }

        return EndTurn();
    }

    public static AIDecision DecideMap(GameState state)
    {
        var map = state.Map!;
        var nodes = map.AvailableNodes;
        if (nodes.Count == 0)
            return new AIDecision { Action = "choose_map", MapNodeIndex = 0, Reasoning = "无可用节点" };

        float hpPercent = (float)state.Player.Hp / state.Player.MaxHp;

        // 按优先级排序
        var preferred = nodes.OrderByDescending(n => GetMapNodePriority(n.Type, hpPercent)).First();
        return new AIDecision
        {
            Action = "choose_map",
            MapNodeIndex = preferred.Index,
            Reasoning = $"选择 {preferred.Type} (HP:{hpPercent:P0})"
        };
    }

    public static AIDecision DecideRestSite(GameState state)
    {
        float hpPercent = (float)state.Player.Hp / state.Player.MaxHp;
        if (hpPercent < 0.6)
            return new AIDecision { Action = "rest", Reasoning = $"HP较低({hpPercent:P0})，休息恢复" };
        return new AIDecision { Action = "smith", Reasoning = $"HP充足({hpPercent:P0})，升级卡牌" };
    }

    public static AIDecision DecideCardSelection(GameState state)
    {
        if (state.CardChoices == null || state.CardChoices.Count == 0)
            return new AIDecision { Action = "skip_reward", Reasoning = "无可选卡牌" };

        // 牌组太大时跳过
        if (state.Player.DeckSize > 25)
            return new AIDecision { Action = "skip_reward", Reasoning = "牌组过大，跳过" };

        // 优先选 Power 和 Rare
        var best = state.CardChoices
            .OrderByDescending(c => c.Type == "Power" ? 10 : 0)
            .ThenByDescending(c => c.IsUpgraded ? 2 : 0)
            .First();

        return new AIDecision
        {
            Action = "choose_card",
            CardChoiceIndex = best.Index,
            Reasoning = $"选择 {best.Name}"
        };
    }

    public static AIDecision DecideReward(GameState state)
    {
        if (state.Rewards == null || state.Rewards.Count == 0)
            return new AIDecision { Action = "skip_reward", Reasoning = "无奖励可选" };

        // 优先拿金币 > 药水 > 遗物 > 卡牌
        var reward = state.Rewards
            .OrderByDescending(r => r.Type switch
            {
                "gold" => 4,
                "relic" => 3,
                "potion" => 2,
                "card" => 1,
                _ => 0
            })
            .First();

        return new AIDecision
        {
            Action = "take_reward",
            RewardIndex = reward.Index,
            Reasoning = $"拿取 {reward.Type}: {reward.Description}"
        };
    }

    public static AIDecision DecideEvent(GameState state)
    {
        if (state.EventOptions == null || state.EventOptions.Count == 0)
            return new AIDecision { Action = "choose_event", EventOptionIndex = 0, Reasoning = "无选项" };

        // 选第一个未锁定选项
        var option = state.EventOptions.FirstOrDefault(o => !o.IsLocked) ?? state.EventOptions[0];
        return new AIDecision
        {
            Action = "choose_event",
            EventOptionIndex = option.Index,
            Reasoning = $"选择: {option.Title}"
        };
    }

    // --- 辅助方法 ---

    private static int EstimateDamage(CardInfo card)
    {
        // 使用 codex 的精确伤害值
        if (card.Damage > 0)
            return card.Damage * Math.Max(1, card.HitCount);
        // 兆底：用费用粗略估算
        return card.Cost > 0 ? card.Cost * 6 : 4;
    }

    private static int EstimateBlock(CardInfo card)
    {
        // 使用 codex 的精确格挡值
        if (card.Block > 0)
            return card.Block;
        return card.Cost > 0 ? card.Cost * 5 : 5;
    }

    private static int FindBestTarget(List<EnemyInfo> enemies)
    {
        // 优先攻击血量最低的敌人
        return enemies.OrderBy(e => e.Hp).First().Index;
    }

    private static int GetMapNodePriority(string type, float hpPercent)
    {
        return type switch
        {
            "RestSite" => hpPercent < 0.5 ? 100 : 20,
            "Shop" => 60,
            "Treasure" => 70,
            "Event" => 50,
            "Unknown" => 50,
            "Monster" => hpPercent > 0.5 ? 40 : 10,
            "Elite" => hpPercent > 0.75 ? 55 : 5,
            "Boss" => 30,
            "Ancient" => hpPercent > 0.75 ? 65 : 5,
            _ => 25
        };
    }

    private static AIDecision PlayCard(int cardIndex, int targetIndex, string reasoning)
    {
        return new AIDecision
        {
            Action = "play_card",
            CardIndex = cardIndex,
            TargetIndex = targetIndex,
            Reasoning = reasoning
        };
    }

    private static AIDecision EndTurn()
    {
        return new AIDecision { Action = "end_turn", Reasoning = "结束回合" };
    }
}
