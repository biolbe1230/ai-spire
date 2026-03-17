using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AISpire.AI;

public static class PromptBuilder
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public const string SystemPrompt = @"你是一个杀戮尖塔2(Slay the Spire 2)的专家AI玩家。你需要根据当前游戏状态做出最优决策。

## 游戏基本规则
- 每回合开始时能量恢复为满（默认3），**未使用的能量不会保留到下回合**，必须尽量用完
- 每回合开始抽5张牌（除非有遗物/能力改变抽牌数）
- 打出牌需要消耗对应费用的能量，费用0的牌免费打出
- **格挡(Block)在你的下一回合开始时清零**，不会累积到下回合（除非有特殊能力）
- 敌人头顶显示的意图是**本回合确定要执行的行动**，不会改变
- 敌人的攻击伤害先扣格挡，格挡扣完后才扣HP
- Power牌打出后永久生效直到战斗结束，不进弃牌堆
- Skill牌和Attack牌打出后进入弃牌堆
- 标记为Exhaust的牌打出后被消耗(移出战斗)，不进弃牌堆
- 抽牌堆空时，弃牌堆洗入抽牌堆
- 你每次只能执行一个动作（打一张牌/用一个药水/结束回合），系统会循环询问你直到你结束回合
- **击杀所有敌人即获胜**，不需要结束回合

## 核心策略
- **尽量花光所有能量**，剩余能量是浪费
- 如果能在本回合击杀敌人，全力输出，不需要防御
- 如果不能击杀，根据敌人意图决定攻防比例：高伤害意图优先防御，非攻击意图全力输出
- Power牌优先级最高，越早打出价值越大
- 药水在关键时刻使用(boss战、低血量、精英战)
- 注意多段攻击(如5x3=15总伤害)，格挡需要挡住总伤害

## 能力(Power)联动与多回合规划
你需要像动态规划一样思考，考虑当前决策对未来回合的影响:
- **仔细阅读每个能力(Buff/Debuff)的描述**，它们是决策的关键依据
- 力量(Strength): 增加所有攻击牌伤害，力量越高越应该多出攻击牌
- 敏捷(Dexterity): 增加所有格挡牌的格挡值，高敏捷时防御非常高效
- 易伤(Vulnerable): 受到50%额外伤害，对有易伤的目标优先攻击
- 虚弱(Weak): 造成25%更少伤害，被虚弱时切换防御策略
- 如果你有「受伤获得力量」类能力，短期承伤换取长期输出可能是最优解
- 如果敌人有力量增长类能力，应优先速杀而非长期防守
- 如果你有持续格挡/荆棘/反伤类能力，长期战更有利
- **每回合决策时，先评估：速杀(几回合内击杀) vs 持久战(靠能力滚雪球)哪个更优**
- 考虑抽牌堆循环：关键牌大约每N回合出现一次（N≈牌组大小÷5）

## 地图策略
- HP充足(>70%)时可走精英
- HP较低(<40%)时优先走休息点
- 优先走商店和未知事件
- 避免不必要的战斗

你必须以JSON格式回复，格式如下:
{""action"": ""动作类型"", ""card_index"": -1, ""target_index"": -1, ""reasoning"": ""决策理由""}

动作类型:
- ""play_card"": 打出手牌，需指定card_index，如果牌需要目标还需指定target_index
- ""end_turn"": 结束回合
- ""use_potion"": 使用药水，需指定potion_index，如需目标指定target_index
- ""choose_map"": 选择地图节点，需指定map_node_index
- ""take_reward"": 拿取奖励，需指定reward_index
- ""skip_reward"": 跳过奖励
- ""choose_card"": 选择卡牌奖励，需指定card_choice_index
- ""choose_event"": 选择事件选项，需指定event_option_index
- ""rest"": 休息(恢复HP)
- ""smith"": 升级卡牌

只回复JSON，不要其他文字。";

    public static string BuildCombatPrompt(GameState state)
    {
        var sb = new StringBuilder();
        var combat = state.Combat!;

        sb.AppendLine("## 当前战斗状态");
        sb.AppendLine($"角色: {state.Player.Character} | HP: {combat.Hp}/{combat.MaxHp} | 格挡: {combat.Block} | 能量: {combat.Energy}/{combat.MaxEnergy}");
        sb.AppendLine($"层数: Act{state.ActIndex + 1} - Floor{state.Floor}");
        sb.AppendLine($"牌堆: 抽牌{combat.DrawPileCount} | 弃牌{combat.DiscardPileCount} | 消耗{combat.ExhaustPileCount}");

        // 遗物（全局效果，影响整场战斗）
        if (state.Player.Relics.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## 遗物效果");
            foreach (var relic in state.Player.Relics)
            {
                if (!string.IsNullOrEmpty(relic.Description))
                    sb.AppendLine($"- {relic.Name}: {relic.Description}");
                else
                    sb.AppendLine($"- {relic.Name}");
            }
        }

        if (combat.PlayerPowers.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## 当前能力(Buff/Debuff)");
            foreach (var p in combat.PlayerPowers)
            {
                var tag = p.IsDebuff ? "[负面]" : "";
                var amt = p.Amount != 0 ? $"x{p.Amount}" : "";
                var desc = !string.IsNullOrEmpty(p.Description) ? $" - {p.Description}" : "";
                sb.AppendLine($"- {p.Name}{amt}{tag}{desc}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## 手牌");
        foreach (var card in combat.Hand)
        {
            var playable = card.CanPlay ? "" : " [无法打出]";
            var upgraded = card.IsUpgraded ? "+" : "";
            // 显示精确数值（来自 codex）
            var statsStr = BuildCardStatsStr(card);
            sb.AppendLine($"[{card.Index}] {card.Name}{upgraded} (费用:{card.Cost}, 类型:{card.Type}, 目标:{card.Target}{statsStr}){playable} - {card.Description}");
        }

        sb.AppendLine();
        sb.AppendLine("## 敌人");
        foreach (var enemy in combat.Enemies)
        {
            // 基础信息
            sb.Append($"[{enemy.Index}] {enemy.Name} HP:{enemy.Hp}/{enemy.MaxHp} 格挡:{enemy.Block}");

            // 当前回合确切意图（来自游戏实时数据）
            var moveName = !string.IsNullOrEmpty(enemy.IntentMoveName) ? enemy.IntentMoveName
                         : !string.IsNullOrEmpty(enemy.IntentMoveId) ? enemy.IntentMoveId
                         : "";
            var movePrefix = !string.IsNullOrEmpty(moveName) ? $"[{moveName}]" : "";

            if (enemy.Intent == "Attack")
                sb.Append($" 本回合:{movePrefix}攻击({enemy.IntentDamage}x{enemy.IntentHits})");
            else if (!string.IsNullOrEmpty(enemy.Intent) && enemy.Intent != "Unknown")
                sb.Append($" 本回合:{movePrefix}{enemy.Intent}");
            else
                sb.Append($" 本回合:未知");

            // 当前 buff/debuff
            if (enemy.Powers.Count > 0)
            {
                var powerStrs = enemy.Powers.Select(p =>
                {
                    var desc = !string.IsNullOrEmpty(p.Description) ? $"({p.Description})" : "";
                    return $"{p.Name}x{p.Amount}{desc}";
                });
                sb.Append($" 能力:[{string.Join(", ", powerStrs)}]");
            }
            sb.AppendLine();

            // 后续可能招式（来自 codex，含伤害/格挡，供 AI 长远规划）
            var codexMonster = GameDataLoader.FindMonster(enemy.Name);
            if (codexMonster?.Moves != null && codexMonster.Moves.Count > 0)
            {
                var otherMoves = codexMonster.Moves
                    .Where(m => !string.Equals(m.Id, enemy.IntentMoveId, StringComparison.OrdinalIgnoreCase))
                    .Select(m => GameDataLoader.DescribeMove(codexMonster, m))
                    .ToList();
                if (otherMoves.Count > 0)
                    sb.AppendLine($"   └ 后续可能招式: [{string.Join(" | ", otherMoves)}]");
            }
        }

        // 药水
        if (state.Player.Potions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## 药水");
            foreach (var p in state.Player.Potions)
                sb.AppendLine($"[{p.Index}] {p.Name} - {p.Description}");
        }

        sb.AppendLine();
        sb.AppendLine("请选择一个动作（play_card/use_potion/end_turn），以JSON格式回复。");

        return sb.ToString();
    }

    /// <summary>
    /// 构建卡牌精确数值字符串
    /// </summary>
    private static string BuildCardStatsStr(CardInfo card)
    {
        var parts = new List<string>();
        if (card.Damage > 0)
        {
            var dmgStr = card.HitCount > 1 ? $"伤害:{card.Damage}x{card.HitCount}" : $"伤害:{card.Damage}";
            parts.Add(dmgStr);
        }
        if (card.Block > 0)
            parts.Add($"格挡:{card.Block}");
        return parts.Count > 0 ? ", " + string.Join(", ", parts) : "";
    }

    public static string BuildMapPrompt(GameState state)
    {
        var sb = new StringBuilder();
        var map = state.Map!;

        sb.AppendLine("## 地图选择");
        sb.AppendLine($"角色: {state.Player.Character} | HP: {state.Player.Hp}/{state.Player.MaxHp} | 金币: {state.Player.Gold}");
        sb.AppendLine($"层数: Act{state.ActIndex + 1} - Floor{state.Floor}");
        sb.AppendLine($"当前位置: {map.CurrentCoord}");
        sb.AppendLine();
        sb.AppendLine("## 可选节点");
        foreach (var node in map.AvailableNodes)
            sb.AppendLine($"[{node.Index}] {node.Type} (位置:{node.Coord})");

        sb.AppendLine();
        sb.AppendLine("请选择要前往的节点（choose_map），以JSON格式回复。");
        return sb.ToString();
    }

    public static string BuildRewardPrompt(GameState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 奖励选择");
        sb.AppendLine($"角色: {state.Player.Character} | HP: {state.Player.Hp}/{state.Player.MaxHp} | 金币: {state.Player.Gold} | 牌组大小: {state.Player.DeckSize}张");

        if (state.Rewards != null)
        {
            foreach (var r in state.Rewards)
                sb.AppendLine($"[{r.Index}] {r.Type}: {r.Description}");
        }

        sb.AppendLine();
        sb.AppendLine("请选择（take_reward/skip_reward），以JSON格式回复。");
        return sb.ToString();
    }

    public static string BuildCardSelectionPrompt(GameState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 卡牌选择");
        sb.AppendLine($"角色: {state.Player.Character} | 牌组大小: {state.Player.DeckSize}张");

        if (state.CardChoices != null)
        {
            foreach (var c in state.CardChoices)
            {
                var upgraded = c.IsUpgraded ? "+" : "";
                sb.AppendLine($"[{c.Index}] {c.Name}{upgraded} (费用:{c.Cost}, 类型:{c.Type}) - {c.Description}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("请选择一张卡牌加入牌组（choose_card），或跳过（skip_reward），以JSON格式回复。");
        return sb.ToString();
    }

    public static string BuildEventPrompt(GameState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 事件选项");
        sb.AppendLine($"角色: {state.Player.Character} | HP: {state.Player.Hp}/{state.Player.MaxHp} | 金币: {state.Player.Gold}");

        if (state.EventOptions != null)
        {
            foreach (var opt in state.EventOptions)
            {
                var locked = opt.IsLocked ? " [锁定]" : "";
                sb.AppendLine($"[{opt.Index}] {opt.Title}{locked} - {opt.Description}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("请选择一个事件选项（choose_event），以JSON格式回复。");
        return sb.ToString();
    }

    public static string BuildRestSitePrompt(GameState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 休息点");
        sb.AppendLine($"角色: {state.Player.Character} | HP: {state.Player.Hp}/{state.Player.MaxHp}");
        sb.AppendLine($"牌组大小: {state.Player.DeckSize}张");
        sb.AppendLine();
        sb.AppendLine("选项: rest(恢复HP), smith(升级一张牌)");
        sb.AppendLine("请选择（rest/smith），以JSON格式回复。");
        return sb.ToString();
    }
}
