using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AISpire.Config;

namespace AISpire.AI;

public static class PromptBuilder
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static string SystemPrompt => Loc.IsEnglish ? SystemPromptEN : SystemPromptZH;

    private const string SystemPromptZH = @"你是一个杀戮尖塔2(Slay the Spire 2)的专家AI玩家。你需要根据当前游戏状态做出最优决策。

## 游戏基本规则
- 每回合开始时能量恢复为满（默认3），**未使用的能量不会保留到下回合**，必须尽量用完
- 每回合开始抽5张牌（除非有遗物/能力改变抽牌数）
- 打出牌需要消耗对应费用的能量，费用0的牌免费打出
- **X费卡牌**（显示费用:X）打出时消耗你所有剩余能量，能量越多效果越强。如果有其它需要费用的牌，应于X费卡牌之前打出。
- **格挡(Block)在你的下一回合开始时清零**，不会累积到下回合（除非有特殊能力）
- 敌人头顶显示的意图是**本回合确定要执行的行动**，不会改变
- 敌人的攻击伤害先扣格挡，格挡扣完后才扣HP
- Power牌打出后永久生效直到战斗结束，不进弃牌堆
- Skill牌和Attack牌打出后进入弃牌堆
- 标记为Exhaust的牌打出后被消耗(移出战斗)，不进弃牌堆
- 抽牌堆空时，弃牌堆洗入抽牌堆
- 你每次只能执行一个动作（打一张牌/用一个药水/结束回合），系统会循环询问你直到你结束回合
- **击杀所有敌人即获胜**，不需要结束回合

## ★ 每回合必须遵循的思考流程（按顺序）

### 第1步：阅读敌人的能力和Buff
逐个检查每个敌人的能力(Powers)列表，特别关注：
- **伤害限制类**：如「滑溜」(每次只受1点伤害)、「荆棘」(攻击它会反伤)
- **力量增长类**：如每回合+力量，说明必须速杀
- **多段攻击类**：对手意图5x3=15总伤害，格挡需要够总量
- **免疫/抗性类**：如不受debuff影响、扣血上限
→ 这些能力直接决定你的策略方向。**如果敌人有滑溜/伤害上限，易伤、力量加成全部无意义，应改用多次命中的卡牌**

### 第2步：阅读自己的能力和Buff
检查你身上所有Powers，理解当前增益：
- 力量xN → 每张攻击牌额外+N伤害（但如果敌人有伤害限制，此加成无意义）
- 敏捷xN → 每张格挡牌额外+N格挡
- 覆甲xN → 回合结束自动获得N格挡
- 其他特殊能力 → 仔细阅读描述

### 第3步：评估局势 — 速杀 vs 持久战
- 计算：你本回合最大输出 vs 敌人剩余HP
- 如果能击杀 → 全力输出，忽略防御
- 如果不能击杀 → 根据敌人意图决定攻防比例
- 如果敌人有伤害上限/滑溜等限制 → 不要上易伤/力量，改为叠多段攻击或格挡消耗

### 第4步：确定出牌优先级
按以下顺序选择要打的牌：
1. **Power牌** → 永久效果，越早打价值越大（但注意Power的效果对当前敌人是否有意义）
2. **0费牌** → 免费收益，无脑打出
3. **核心策略牌** → 根据第3步的策略方向选择攻击/防御
4. **X费卡** → 放在最后打，确保吃到所有剩余能量

### 第5步：执行并花光能量
- 检查是否还有能量和可用牌
- 能量不要浪费，尽量全部花完
- 格挡足够时不要过度防御，多余能量用于输出

## 核心策略
- **尽量花光所有能量**，剩余能量是浪费
- 如果能在本回合击杀敌人，全力输出，不需要防御
- 如果不能击杀，根据敌人意图决定攻防比例：高伤害意图优先防御，非攻击意图全力输出
- Power牌优先级最高，越早打出价值越大
- 药水在关键时刻使用(boss战、低血量、精英战)
- 注意多段攻击(如5x3=15总伤害)，格挡需要挡住总伤害
- **对有伤害上限的敌人（如滑溜），易伤(Vulnerable)和力量加成毫无意义，不要浪费行动上debuff，改用多次命中的牌**

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
- **务必注意敌人的特殊能力对你策略的否定效果**：如果敌人限制受到的伤害，增加攻击力的Power就不值得打

## 地图策略
- HP充足(>70%)时可走精英
- HP较低(<40%)时优先走休息点
- 优先走商店和未知事件
- 避免不必要的战斗

你必须以JSON格式回复，格式如下:
{""action"": ""动作类型"", ""card_index"": -1, ""target_index"": -1, ""reasoning"": ""决策理由(请在reasoning中体现你的思考过程：先说敌人能力分析，再说策略选择，最后说具体行动)""}

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
- ""choose_rest_option"": 选择营地选项，需指定event_option_index
- ""buy_shop_item"": 购买商店物品，需指定shop_item_index
- ""leave_shop"": 离开商店
- ""choose_relic"": 选择遗物，需指定relic_choice_index

只回复JSON，不要其他文字。";

    private const string SystemPromptEN = @"You are an expert AI player of Slay the Spire 2. Make optimal decisions based on the current game state.

## Basic Rules
- Energy refills each turn (default 3). **Unused energy does NOT carry over** — spend it all.
- Draw 5 cards per turn (unless modified by relics/powers).
- Playing a card costs its energy cost; 0-cost cards are free.
- **X-cost cards** (shown as Cost:X) consume ALL your remaining energy when played. The more energy you have, the stronger the effect. Play X-cost cards LAST in a turn to maximize their value, or play them when you have lots of energy.
- **Block resets to 0 at the start of YOUR next turn** (unless a special power says otherwise).
- Enemy intents shown above their heads are **confirmed actions this turn** and will not change.
- Enemy attacks reduce Block first, then HP.
- Power cards have permanent effects for the rest of combat; they don't go to discard pile.
- Skill and Attack cards go to discard pile after being played.
- Exhaust cards are removed from combat when played.
- When draw pile is empty, discard pile is shuffled into draw pile.
- You may only perform one action at a time (play a card / use a potion / end turn). The system will keep asking until you end your turn.
- **Killing all enemies wins the fight** — no need to end turn after.

## ★ MANDATORY THINKING FRAMEWORK (follow this order every turn)

### Step 1: Read Enemy Powers/Buffs
Check EVERY enemy's powers list carefully. Watch for:
- **Damage cap abilities**: e.g. ""Slippery"" (only takes 1 damage per hit) or similar — means Vulnerable, Strength buffs are USELESS. Use multi-hit cards instead.
- **Thorns/retaliate**: attacking them hurts you — favor skills or indirect damage.
- **Strength scaling**: enemy gains Strength each turn — must kill quickly.
- **Immunity/resistance**: immune to debuffs, damage caps, etc.
→ These abilities OVERRIDE your default strategy. **If an enemy has damage caps, do NOT waste actions on Vulnerable/Strength buffs. Use multi-hit cards.**

### Step 2: Read Your Own Powers/Buffs
Check all your active powers and understand your current bonuses:
- Strength xN → each Attack deals +N extra damage (but useless if enemy has damage cap)
- Dexterity xN → each Block card gives +N extra Block
- Plating xN → gain N Block at end of turn automatically
- Other powers → read descriptions carefully

### Step 3: Assess Situation — Quick Kill vs. Long Fight
- Calculate: your max damage this turn vs. enemy remaining HP
- If you can kill → go all-out offense, skip defense
- If you can't kill → balance offense/defense based on enemy intent
- If enemy has damage cap → don't buff your attack, focus on multi-hit or defense/attrition

### Step 4: Determine Card Priority
Play cards in this order:
1. **Power cards** → permanent effects, play early (but only if useful against this enemy)
2. **0-cost cards** → free value, always play
3. **Core strategy cards** → offense or defense based on Step 3
4. **X-cost cards** → play LAST to consume all remaining energy

### Step 5: Spend All Energy
- Check if you still have energy and playable cards
- Don't waste energy — spend it all
- If Block is sufficient, don't over-defend; use remaining energy for offense

## Core Strategy
- **Spend all energy** — leftover energy is wasted.
- If you can kill the enemy this turn, go all-out offense, no need for defense.
- If you can't kill, balance offense/defense based on enemy intent: high damage → prioritize defense; non-attack → go offense.
- Power cards have highest priority — play them early for maximum value.
- Use potions at critical moments (boss fights, low HP, elite fights).
- Watch for multi-hit attacks (e.g. 5×3 = 15 total damage); Block must cover total damage.
- **Against enemies with damage caps (e.g. Slippery), Vulnerable and Strength buffs are worthless — use multi-hit cards instead.**

## Power Synergy & Multi-Turn Planning
Think like dynamic programming — consider how current decisions affect future turns:
- **Read each power (Buff/Debuff) description carefully** — they are key to decisions.
- Strength: increases all Attack damage. Higher Strength → play more Attacks.
- Dexterity: increases all Block gained. High Dexterity → defense is very efficient.
- Vulnerable: take 50% more damage. Prioritize attacking Vulnerable targets.
- Weak: deal 25% less damage. When Weakened, switch to defense.
- If you have ""gain Strength when damaged"" powers, taking short-term damage for long-term offense may be optimal.
- If the enemy gains Strength over time, prioritize quick kills over long fights.
- If you have ongoing Block/Thorns/retaliate powers, longer fights are favorable.
- **Each turn: evaluate quick kill (within few turns) vs. sustained fight (snowball with powers).**
- Consider draw pile cycling: key cards appear roughly every N turns (N ≈ deck size ÷ 5).
- **Always check if enemy powers negate your strategy**: damage caps make attack buffs useless.

## Map Strategy
- HP above 70%: can take Elite fights.
- HP below 40%: prioritize Rest Sites.
- Prefer Shops and Unknown events.
- Avoid unnecessary fights.

You MUST reply in JSON format:
{""action"": ""action_type"", ""card_index"": -1, ""target_index"": -1, ""reasoning"": ""your reasoning (show your thinking: first analyze enemy powers, then strategy choice, then specific action)""}

Action types:
- ""play_card"": play a hand card, specify card_index; if the card needs a target, also specify target_index
- ""end_turn"": end your turn
- ""use_potion"": use a potion, specify potion_index; if it needs a target, specify target_index
- ""choose_map"": choose a map node, specify map_node_index
- ""take_reward"": take a reward, specify reward_index
- ""skip_reward"": skip rewards
- ""choose_card"": choose a card reward, specify card_choice_index
- ""choose_event"": choose an event option, specify event_option_index
- ""rest"": rest (heal HP)
- ""smith"": upgrade a card
- ""choose_rest_option"": choose a camp option, specify event_option_index
- ""buy_shop_item"": buy a shop item, specify shop_item_index
- ""leave_shop"": leave the shop
- ""choose_relic"": choose a relic, specify relic_choice_index

Reply with JSON only, no other text.";

    public static string BuildCombatPrompt(GameState state)
    {
        var sb = new StringBuilder();
        var combat = state.Combat!;

        sb.AppendLine(Loc.CombatHeader);
        sb.AppendLine($"{Loc.CharLabel}: {state.Player.Character} | HP: {combat.Hp}/{combat.MaxHp} | {Loc.BlockLabel}: {combat.Block} | {Loc.EnergyLabel}: {combat.Energy}/{combat.MaxEnergy}");
        sb.AppendLine($"{Loc.FloorLabel}: Act{state.ActIndex + 1} - Floor{state.Floor}");
        sb.AppendLine($"{Loc.DeckPile}: {Loc.DrawPile}{combat.DrawPileCount} | {Loc.DiscardPile}{combat.DiscardPileCount} | {Loc.ExhaustPile}{combat.ExhaustPileCount}");

        // ★ 敌人信息放在最前面，让 AI 先分析敌人能力（思考流程第1步）
        sb.AppendLine();
        sb.AppendLine(Loc.EnemiesHeader);
        sb.AppendLine(Loc.EnemyAnalysisHint);
        foreach (var enemy in combat.Enemies)
        {
            sb.Append($"[{enemy.Index}] {enemy.Name} HP:{enemy.Hp}/{enemy.MaxHp} {Loc.BlockLabel}:{enemy.Block}");

            var moveName = !string.IsNullOrEmpty(enemy.IntentMoveName) ? enemy.IntentMoveName
                         : !string.IsNullOrEmpty(enemy.IntentMoveId) ? enemy.IntentMoveId
                         : "";
            var movePrefix = !string.IsNullOrEmpty(moveName) ? $"[{moveName}]" : "";

            if (enemy.Intent == "Attack")
                sb.Append($" {Loc.ThisTurn}:{movePrefix}{Loc.AttackStr}({enemy.IntentDamage}x{enemy.IntentHits})");
            else if (!string.IsNullOrEmpty(enemy.Intent) && enemy.Intent != "Unknown")
                sb.Append($" {Loc.ThisTurn}:{movePrefix}{enemy.Intent}");
            else
                sb.Append($" {Loc.ThisTurn}:{Loc.UnknownIntent}");

            if (enemy.Powers.Count > 0)
            {
                sb.AppendLine();
                sb.Append($"   └ {Loc.Abilities}: ");
                var powerStrs = enemy.Powers.Select(p =>
                {
                    var desc = !string.IsNullOrEmpty(p.Description) ? $"({p.Description})" : "";
                    return $"**{p.Name}x{p.Amount}**{desc}";
                });
                sb.Append(string.Join(", ", powerStrs));
            }
            sb.AppendLine();

            var codexMonster = GameDataLoader.FindMonster(enemy.Name);
            if (codexMonster?.Moves != null && codexMonster.Moves.Count > 0)
            {
                var otherMoves = codexMonster.Moves
                    .Where(m => !string.Equals(m.Id, enemy.IntentMoveId, StringComparison.OrdinalIgnoreCase))
                    .Select(m => GameDataLoader.DescribeMove(codexMonster, m))
                    .ToList();
                if (otherMoves.Count > 0)
                    sb.AppendLine($"   └ {Loc.FutureMoves}: [{string.Join(" | ", otherMoves)}]");
            }
        }

        // ★ 自己的能力（思考流程第2步）
        // 遗物
        if (state.Player.Relics.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(Loc.RelicsHeader);
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
            sb.AppendLine(Loc.PowersHeader);
            foreach (var p in combat.PlayerPowers)
            {
                var tag = p.IsDebuff ? Loc.DebuffTag : "";
                var amt = p.Amount != 0 ? $" x{p.Amount}" : "";
                var desc = !string.IsNullOrEmpty(p.Description) ? $" - {p.Description}" : "";
                sb.AppendLine($"- {p.Name}{amt}{tag}{desc}");
            }
            sb.AppendLine(Loc.PowersNote);
        }

        // ★ 手牌（思考流程第3-5步：根据分析结果选牌）
        sb.AppendLine();
        sb.AppendLine(Loc.HandHeader);
        foreach (var card in combat.Hand)
        {
            var playable = card.CanPlay ? "" : Loc.Unplayable;
            var upgraded = card.IsUpgraded ? "+" : "";
            var statsStr = BuildCardStatsStr(card);
            var costStr = card.Cost < 0 ? "X" : card.Cost.ToString();
            sb.AppendLine($"[{card.Index}] {card.Name}{upgraded} ({Loc.CostLabel}:{costStr}, {Loc.TypeLabel}:{card.Type}, {Loc.TargetLabel}:{card.Target}{statsStr}){playable} - {card.Description}");
        }

        // 药水
        if (state.Player.Potions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(Loc.PotionsHeader);
            foreach (var p in state.Player.Potions)
                sb.AppendLine($"[{p.Index}] {p.Name} - {p.Description}");
        }

        sb.AppendLine();
        sb.AppendLine(Loc.CombatActionHint);

        return sb.ToString();
    }

    private static string BuildCardStatsStr(CardInfo card)
    {
        var parts = new List<string>();
        if (card.Damage > 0)
        {
            var dmgStr = card.HitCount > 1 ? $"{Loc.DamageLabel}:{card.Damage}x{card.HitCount}" : $"{Loc.DamageLabel}:{card.Damage}";
            parts.Add(dmgStr);
        }
        if (card.Block > 0)
            parts.Add($"{Loc.BlockLabel}:{card.Block}");
        return parts.Count > 0 ? ", " + string.Join(", ", parts) : "";
    }

    public static string BuildMapPrompt(GameState state)
    {
        var sb = new StringBuilder();
        var map = state.Map!;

        sb.AppendLine(Loc.MapHeader);
        sb.AppendLine($"{Loc.CharLabel}: {state.Player.Character} | HP: {state.Player.Hp}/{state.Player.MaxHp} | {Loc.GoldLabel}: {state.Player.Gold}");
        sb.AppendLine($"{Loc.FloorLabel}: Act{state.ActIndex + 1} - Floor{state.Floor}");
        sb.AppendLine($"{Loc.CurrentPos}: {map.CurrentCoord}");
        sb.AppendLine();
        sb.AppendLine(Loc.AvailableNodes);
        foreach (var node in map.AvailableNodes)
            sb.AppendLine($"[{node.Index}] {node.Type} ({Loc.PositionLabel}:{node.Coord})");

        sb.AppendLine();
        sb.AppendLine(Loc.MapActionHint);
        return sb.ToString();
    }

    public static string BuildRewardPrompt(GameState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Loc.RewardHeader);
        sb.AppendLine($"{Loc.CharLabel}: {state.Player.Character} | HP: {state.Player.Hp}/{state.Player.MaxHp} | {Loc.GoldLabel}: {state.Player.Gold} | {Loc.DeckSizeLabel}: {state.Player.DeckSize}{Loc.DeckUnit}");

        if (state.Rewards != null)
        {
            foreach (var r in state.Rewards)
                sb.AppendLine($"[{r.Index}] {r.Type}: {r.Description}");
        }

        sb.AppendLine();
        sb.AppendLine(Loc.RewardActionHint);
        return sb.ToString();
    }

    public static string BuildCardSelectionPrompt(GameState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Loc.CardSelectionHeader);
        sb.AppendLine($"{Loc.CharLabel}: {state.Player.Character} | {Loc.DeckSizeLabel}: {state.Player.DeckSize}{Loc.DeckUnit}");

        if (state.CardChoices != null)
        {
            foreach (var c in state.CardChoices)
            {
                var upgraded = c.IsUpgraded ? "+" : "";
                var costStr = c.Cost < 0 ? "X" : c.Cost.ToString();
                sb.AppendLine($"[{c.Index}] {c.Name}{upgraded} ({Loc.CostLabel}:{costStr}, {Loc.TypeLabel}:{c.Type}) - {c.Description}");
            }
        }

        sb.AppendLine();
        sb.AppendLine(Loc.CardSelectionHint);
        return sb.ToString();
    }

    public static string BuildEventPrompt(GameState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Loc.EventHeader);
        sb.AppendLine($"{Loc.CharLabel}: {state.Player.Character} | HP: {state.Player.Hp}/{state.Player.MaxHp} | {Loc.GoldLabel}: {state.Player.Gold}");
        if (!string.IsNullOrEmpty(state.EventTitle))
            sb.AppendLine($"{Loc.EventLabel}: {state.EventTitle}");
        if (!string.IsNullOrEmpty(state.EventDescription))
            sb.AppendLine($"{Loc.DescLabel}: {state.EventDescription}");
        sb.AppendLine();
        sb.AppendLine(Loc.OptionsHeader);

        if (state.EventOptions != null)
        {
            foreach (var opt in state.EventOptions)
            {
                var locked = opt.IsLocked ? Loc.Locked : "";
                sb.AppendLine($"[{opt.Index}] {opt.Title}{locked} - {opt.Description}");
            }
        }

        sb.AppendLine();
        sb.AppendLine(Loc.EventActionHint);
        return sb.ToString();
    }

    public static string BuildRestSitePrompt(GameState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Loc.RestHeader);
        sb.AppendLine($"{Loc.CharLabel}: {state.Player.Character} | HP: {state.Player.Hp}/{state.Player.MaxHp}");
        sb.AppendLine($"{Loc.DeckSizeLabel}: {state.Player.DeckSize}{Loc.DeckUnit}");
        sb.AppendLine();

        if (state.RestSiteOptions != null && state.RestSiteOptions.Count > 0)
        {
            sb.AppendLine(Loc.AvailableOps);
            foreach (var opt in state.RestSiteOptions)
            {
                var enabled = opt.IsEnabled ? "" : Loc.Unavailable;
                sb.AppendLine($"[{opt.Index}] {opt.Name}{enabled} - {opt.Description}");
            }
        }
        else
        {
            sb.AppendLine(Loc.RestDefault);
        }

        sb.AppendLine();
        sb.AppendLine(Loc.RestActionHint);
        return sb.ToString();
    }

    public static string BuildShopPrompt(GameState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Loc.ShopHeader);
        sb.AppendLine($"{Loc.CharLabel}: {state.Player.Character} | HP: {state.Player.Hp}/{state.Player.MaxHp} | {Loc.GoldLabel}: {state.Player.Gold} | {Loc.DeckSizeLabel}: {state.Player.DeckSize}{Loc.DeckUnit}");
        sb.AppendLine();

        if (state.ShopItems != null && state.ShopItems.Count > 0)
        {
            sb.AppendLine(Loc.ItemList);
            foreach (var item in state.ShopItems)
            {
                var afford = item.CanAfford ? "" : Loc.Insufficient;
                sb.AppendLine($"[{item.Index}] [{item.Type}] {item.Name} - {Loc.PriceLabel}:{item.Price}g{afford} - {item.Description}");
            }
        }
        else
        {
            sb.AppendLine(Loc.EmptyShop);
        }

        sb.AppendLine();
        sb.AppendLine(Loc.ShopActionHint);
        return sb.ToString();
    }

    public static string BuildTreasurePrompt(GameState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Loc.TreasureHeader);
        sb.AppendLine($"{Loc.CharLabel}: {state.Player.Character} | HP: {state.Player.Hp}/{state.Player.MaxHp}");
        sb.AppendLine();

        if (state.RelicChoices != null && state.RelicChoices.Count > 0)
        {
            sb.AppendLine(Loc.AvailableRelics);
            foreach (var relic in state.RelicChoices)
            {
                sb.AppendLine($"[{relic.Index}] {relic.Name} - {relic.Description}");
            }
        }

        // 当前已有遗物
        if (state.Player.Relics.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(Loc.CurrentRelics);
            foreach (var r in state.Player.Relics)
                sb.AppendLine($"- {r.Name}: {r.Description}");
        }

        sb.AppendLine();
        sb.AppendLine(Loc.TreasureActionHint);
        return sb.ToString();
    }
}
