namespace AISpire.Config;

/// <summary>
/// 简易双语本地化：根据 AIConfig.Language 返回中/英文字符串
/// </summary>
public static class Loc
{
    public static bool IsEnglish => AIConfig.Language == "en";

    // ── Overlay / UI ──
    public static string OverlayTitle => IsEnglish ? "🤖 AI Decision Log" : "🤖 AI 决策日志";

    // ── AIDecisionEngine 日志标签 ──
    public static string NewBattle => IsEnglish ? "New Battle" : "新战斗";
    public static string Enemy => IsEnglish ? "Enemy" : "敌人";
    public static string EndTurn => IsEnglish ? "End Turn" : "结束回合";
    public static string NoEnergyOrCards => IsEnglish ? "No energy or playable cards" : "无能量或无可打牌";
    public static string PlayCardFmt => IsEnglish ? "Play[{0}]" : "出牌[{0}]";
    public static string PotionFmt => IsEnglish ? "Potion[{0}]" : "药水[{0}]";
    public static string ExecFailed => IsEnglish ? "Execution failed" : "执行失败";
    public static string AutoEndTurn => IsEnglish ? "Auto end turn" : "自动结束回合";
    public static string Thinking => IsEnglish ? "Thinking..." : "正在思考...";
    public static string AnalyzingEvent => IsEnglish ? "Analyzing event..." : "分析事件...";
    public static string AnalyzingCamp => IsEnglish ? "Analyzing camp..." : "分析营地选项...";
    public static string AnalyzingShop => IsEnglish ? "Analyzing shop..." : "分析商店...";
    public static string AnalyzingCards => IsEnglish ? "Analyzing card reward..." : "分析卡牌奖励...";
    public static string NoMapNodes => IsEnglish ? "No available map nodes" : "无可用地图节点";
    public static string PathChoice => IsEnglish ? "Path" : "选路";
    public static string NoEventOptions => IsEnglish ? "No event options" : "事件无选项";
    public static string EventChoice => IsEnglish ? "Event" : "事件选择";
    public static string Camp => IsEnglish ? "Camp" : "营地";
    public static string Shop => IsEnglish ? "Shop" : "商店";
    public static string LeaveFmt => IsEnglish ? "Leave: {0}" : "离开: {0}";
    public static string InvalidIndex => IsEnglish ? "Invalid index" : "无效索引";
    public static string InsufficientGoldFmt => IsEnglish ? "Insufficient gold: {0}" : "资金不足: {0}";
    public static string BuyFmt => IsEnglish ? "Buy {0} ({1}g): {2}" : "购买 {0} ({1}金): {2}";
    public static string PurchaseFailed => IsEnglish ? "Purchase failed" : "购买失败";
    public static string NoItems => IsEnglish ? "No items in shop" : "商店无商品";
    public static string NoAffordable => IsEnglish ? "No affordable items, leaving" : "资金不足，离开商店";
    public static string TreasureRoom => IsEnglish ? "Treasure" : "宝物房";
    public static string PickRelic => IsEnglish ? "Pick relic" : "选择遗物";
    public static string CardReward => IsEnglish ? "Card Reward" : "卡牌奖励";
    public static string SkipFmt => IsEnglish ? "Skip: {0}" : "跳过: {0}";
    public static string ChooseFmt => IsEnglish ? "Choose {0}: {1}" : "选择 {0}: {1}";
    public static string UnhandledRoomFmt => IsEnglish ? "Unhandled room: {0}" : "未处理的房间类型: {0}";

    // ── RuleEngine ──
    public static string KillFmt => IsEnglish ? "Kill {0}" : "击杀 {0}";
    public static string DefendFmt => IsEnglish ? "Defend, incoming {0} damage" : "防御，预计受到 {0} 伤害";
    public static string PlayPower => IsEnglish ? "Play Power card" : "打出Power牌";
    public static string Attack => IsEnglish ? "Attack" : "输出攻击";
    public static string PlayAny => IsEnglish ? "Play available card" : "打出可用牌";
    public static string NoNodes => IsEnglish ? "No available nodes" : "无可用节点";
    public static string ChooseNodeFmt => IsEnglish ? "Choose {0} (HP:{1:P0})" : "选择 {0} (HP:{1:P0})";
    public static string LowHpRestFmt => IsEnglish ? "Low HP ({0:P0}), rest to heal" : "HP较低({0:P0})，休息恢复";
    public static string HighHpSmithFmt => IsEnglish ? "HP ok ({0:P0}), upgrade card" : "HP充足({0:P0})，升级卡牌";
    public static string ChooseAvailable => IsEnglish ? "Choose available option" : "选择可用选项";
    public static string NoCardChoices => IsEnglish ? "No card choices" : "无可选卡牌";
    public static string DeckTooLarge => IsEnglish ? "Deck too large, skip" : "牌组过大，跳过";
    public static string ChooseCardFmt => IsEnglish ? "Choose {0}" : "选择 {0}";
    public static string NoRewards => IsEnglish ? "No rewards" : "无奖励可选";
    public static string TakeRewardFmt => IsEnglish ? "Take {0}: {1}" : "拿取 {0}: {1}";
    public static string NoOptions => IsEnglish ? "No options" : "无选项";
    public static string FallbackLeaveShop => IsEnglish ? "Fallback leave shop" : "兜底离开商店";
    public static string FallbackFirstRelic => IsEnglish ? "Fallback choose first relic" : "兜底选择第一个遗物";
    public static string FallbackEndTurn => IsEnglish ? "Unknown scene, fallback end turn" : "未知场景，兜底结束";

    // ── PromptBuilder: section headers ──
    public static string CombatHeader => IsEnglish ? "## Current Combat State" : "## 当前战斗状态";
    public static string CharLabel => IsEnglish ? "Character" : "角色";
    public static string FloorLabel => IsEnglish ? "Floor" : "层数";
    public static string DrawPile => IsEnglish ? "Draw" : "抽牌";
    public static string DiscardPile => IsEnglish ? "Discard" : "弃牌";
    public static string ExhaustPile => IsEnglish ? "Exhaust" : "消耗";
    public static string DeckPile => IsEnglish ? "Pile" : "牌堆";
    public static string RelicsHeader => IsEnglish ? "## Relic Effects" : "## 遗物效果";
    public static string PowersHeader => IsEnglish ? "## Current Powers (Buff/Debuff)" : "## 当前能力(Buff/Debuff)";
    public static string PowersNote => IsEnglish
        ? "(Note: The 'x' value is the number of stacks. For Plating xN, you gain N Block at end of turn. For Strength xN, all Attacks deal N extra damage. For Dexterity xN, all Block gains +N. Pay close attention to these values!)"
        : "(注意: x后面的数字是层数。覆甲xN=回合结束获得N点格挡；力量xN=所有攻击额外造成N点伤害；敏捷xN=所有格挡额外获得N点。请务必关注这些数值来做决策！)";
    public static string DebuffTag => IsEnglish ? "[Debuff]" : "[负面]";
    public static string HandHeader => IsEnglish ? "## Hand" : "## 手牌";
    public static string Unplayable => IsEnglish ? " [Unplayable]" : " [无法打出]";
    public static string CostLabel => IsEnglish ? "Cost" : "费用";
    public static string TypeLabel => IsEnglish ? "Type" : "类型";
    public static string TargetLabel => IsEnglish ? "Target" : "目标";
    public static string EnemiesHeader => IsEnglish ? "## Enemies (★ READ POWERS FIRST before deciding)" : "## 敌人 (★ 先仔细阅读能力再做决策)";
    public static string EnemyAnalysisHint => IsEnglish
        ? "(Step 1: Read each enemy's powers carefully. Damage caps like Slippery make Vulnerable/Strength useless.)"
        : "(第1步：仔细阅读每个敌人的能力。如果敌人有伤害上限类buff如滑溜，则易伤/力量加成全部无效。)";
    public static string ThisTurn => IsEnglish ? "This turn" : "本回合";
    public static string AttackStr => IsEnglish ? "Attack" : "攻击";
    public static string UnknownIntent => IsEnglish ? "Unknown" : "未知";
    public static string Abilities => IsEnglish ? "Abilities" : "能力";
    public static string FutureMoves => IsEnglish ? "Possible future moves" : "后续可能招式";
    public static string PotionsHeader => IsEnglish ? "## Potions" : "## 药水";
    public static string CombatActionHint => IsEnglish
        ? "Choose an action (play_card/use_potion/end_turn), reply in JSON."
        : "请选择一个动作（play_card/use_potion/end_turn），以JSON格式回复。";
    public static string DamageLabel => IsEnglish ? "Damage" : "伤害";
    public static string BlockLabel => IsEnglish ? "Block" : "格挡";
    public static string EnergyLabel => IsEnglish ? "Energy" : "能量";
    public static string GoldLabel => IsEnglish ? "Gold" : "金币";
    public static string DeckSizeLabel => IsEnglish ? "Deck" : "牌组大小";
    public static string DeckUnit => IsEnglish ? " cards" : "张";

    // ── PromptBuilder: map ──
    public static string MapHeader => IsEnglish ? "## Map Choice" : "## 地图选择";
    public static string CurrentPos => IsEnglish ? "Current position" : "当前位置";
    public static string AvailableNodes => IsEnglish ? "## Available Nodes" : "## 可选节点";
    public static string PositionLabel => IsEnglish ? "pos" : "位置";
    public static string MapActionHint => IsEnglish
        ? "Choose a node (choose_map), reply in JSON."
        : "请选择要前往的节点（choose_map），以JSON格式回复。";

    // ── PromptBuilder: reward ──
    public static string RewardHeader => IsEnglish ? "## Reward Selection" : "## 奖励选择";
    public static string RewardActionHint => IsEnglish
        ? "Choose (take_reward/skip_reward), reply in JSON."
        : "请选择（take_reward/skip_reward），以JSON格式回复。";

    // ── PromptBuilder: card selection ──
    public static string CardSelectionHeader => IsEnglish ? "## Card Selection" : "## 卡牌选择";
    public static string CardSelectionHint => IsEnglish
        ? "Choose a card to add (choose_card), or skip (skip_reward), reply in JSON."
        : "请选择一张卡牌加入牌组（choose_card），或跳过（skip_reward），以JSON格式回复。";

    // ── PromptBuilder: event ──
    public static string EventHeader => IsEnglish ? "## Event" : "## 事件";
    public static string EventLabel => IsEnglish ? "Event" : "事件";
    public static string DescLabel => IsEnglish ? "Description" : "描述";
    public static string OptionsHeader => IsEnglish ? "## Options" : "## 选项";
    public static string Locked => IsEnglish ? " [Locked]" : " [锁定]";
    public static string EventActionHint => IsEnglish
        ? "Choose an event option (choose_event), reply in JSON."
        : "请选择一个事件选项（choose_event），以JSON格式回复。";

    // ── PromptBuilder: rest site ──
    public static string RestHeader => IsEnglish ? "## Rest Site" : "## 休息点";
    public static string AvailableOps => IsEnglish ? "## Available Options" : "## 可选操作";
    public static string Unavailable => IsEnglish ? " [Unavailable]" : " [不可用]";
    public static string RestDefault => IsEnglish ? "Options: rest (heal HP), smith (upgrade a card)" : "选项: rest(恢复HP), smith(升级一张牌)";
    public static string RestActionHint => IsEnglish
        ? "Choose (choose_rest_option), specify event_option_index. Reply in JSON.\nIf no specific options listed, use rest or smith as action."
        : "请选择操作（choose_rest_option），指定event_option_index为选项编号，以JSON格式回复。\n如果没有列出具体选项，使用rest或smith作为action。";

    // ── PromptBuilder: shop ──
    public static string ShopHeader => IsEnglish ? "## Shop" : "## 商店";
    public static string ItemList => IsEnglish ? "## Items" : "## 商品列表";
    public static string PriceLabel => IsEnglish ? "Price" : "价格";
    public static string Insufficient => IsEnglish ? " [Insufficient gold]" : " [资金不足]";
    public static string EmptyShop => IsEnglish ? "Shop is empty." : "商店无商品。";
    public static string ShopActionHint => IsEnglish
        ? "Choose an item to buy (buy_shop_item, specify shop_item_index) or leave (leave_shop). One purchase at a time.\nReply in JSON."
        : "请选择要购买的物品（buy_shop_item，指定shop_item_index）或离开商店（leave_shop）。一次只能购买一个物品。\n以JSON格式回复。";

    // ── PromptBuilder: treasure ──
    public static string TreasureHeader => IsEnglish ? "## Treasure Room - Relic Selection" : "## 宝物房 - 遗物选择";
    public static string AvailableRelics => IsEnglish ? "## Available Relics" : "## 可选遗物";
    public static string CurrentRelics => IsEnglish ? "## Current Relics" : "## 当前遗物";
    public static string TreasureActionHint => IsEnglish
        ? "Choose a relic (choose_relic, specify relic_choice_index), reply in JSON."
        : "请选择一个遗物（choose_relic，指定relic_choice_index），以JSON格式回复。";

    // ── ScreenHandler: post-combat & UI ──
    public static string PostCombat => IsEnglish ? "Post-Combat" : "战斗结束";
    public static string ProcessingRewards => IsEnglish ? "Processing rewards..." : "处理奖励中...";
    public static string TakeReward => IsEnglish ? "Take Reward" : "拾取奖励";
    public static string Proceed => IsEnglish ? "Proceed" : "继续前进";
    public static string Upgrade => IsEnglish ? "Upgrade" : "升级";
    public static string AnalyzingUpgrade => IsEnglish ? "Analyzing upgrade..." : "分析升级选项...";
    public static string SelectingPath => IsEnglish ? "Selecting path..." : "选择路径中...";

    // ── GameStateExtractor ──
    public static string CardRemoval => IsEnglish ? "Card Removal" : "移除卡牌";
    public static string CardRemovalDesc => IsEnglish ? "Remove a card from your deck" : "从牌组中移除一张卡牌";

    // ── GameDataLoader ──
    public static string EnergyUnit => IsEnglish ? " Energy" : "能量";
    public static string StarUnit => IsEnglish ? " Star" : "星";
    public static string NonCombat => IsEnglish ? "non-attack" : "非攻防";
}
