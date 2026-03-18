using System.Text.Json.Serialization;

namespace AISpire.AI;

/// <summary>
/// 传给 LLM 的完整游戏状态快照
/// </summary>
public class GameState
{
    // 场景类型
    public string Screen { get; set; } = "";   // "combat", "map", "reward", "event", "shop", "rest", "card_selection"
    public int Floor { get; set; }
    public int ActIndex { get; set; }

    // 玩家信息
    public PlayerInfo Player { get; set; } = new();

    // 战斗状态（仅 combat 时有值）
    public CombatInfo? Combat { get; set; }

    // 地图（仅 map 时有值）
    public MapInfo? Map { get; set; }

    // 奖励列表（仅 reward 时有值）
    public List<RewardInfo>? Rewards { get; set; }

    // 事件选项（仅 event 时有值）
    public List<EventOptionInfo>? EventOptions { get; set; }
    public EventInfo? Event { get; set; }

    // 商店（仅 shop 时有值）
    public ShopInfo? Shop { get; set; }

    // 卡牌选择（选卡奖励等）
    public List<CardInfo>? CardChoices { get; set; }
}

public class PlayerInfo
{
    public string Character { get; set; } = "";
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Gold { get; set; }
    public List<RelicInfo> Relics { get; set; } = new();
    public List<PotionInfo> Potions { get; set; } = new();
    public int DeckSize { get; set; }
}

public class RelicInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

public class PotionInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool NeedsTarget { get; set; }
}

public class CombatInfo
{
    public int Energy { get; set; }
    public int MaxEnergy { get; set; }
    public int Block { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public List<PowerInfo> PlayerPowers { get; set; } = new();
    public List<CardInfo> Hand { get; set; } = new();
    public List<EnemyInfo> Enemies { get; set; } = new();
    public int DrawPileCount { get; set; }
    public int DiscardPileCount { get; set; }
    public int ExhaustPileCount { get; set; }
}

public class CardInfo
{
    public int Index { get; set; }
    public string CardId { get; set; } = "";   // 内部ID，如 STRIKE_IRONCLAD
    public string Name { get; set; } = "";
    public int Cost { get; set; }
    public string Type { get; set; } = "";   // Attack, Skill, Power
    public string Target { get; set; } = "";  // None, AnyEnemy, AllEnemies, Self
    public bool CanPlay { get; set; }
    public bool IsUpgraded { get; set; }
    public string Description { get; set; } = "";
    // 从 codex 加载的精确数值
    public int Damage { get; set; }
    public int Block { get; set; }
    public int HitCount { get; set; } = 1;
}

public class EnemyInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Block { get; set; }
    public string Intent { get; set; } = "";       // Attack, Defend, Buff, Debuff, Unknown
    public string IntentMoveId { get; set; } = ""; // 游戏当前招式 ID (NextMove.StateId)
    public string IntentMoveName { get; set; } = ""; // codex 翻译后的招式名
    public int IntentDamage { get; set; }
    public int IntentHits { get; set; }
    public List<PowerInfo> Powers { get; set; } = new();
}

public class PowerInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Amount { get; set; }
    public bool IsDebuff { get; set; }
}

public class MapInfo
{
    public string CurrentCoord { get; set; } = "";
    public List<MapNodeInfo> AvailableNodes { get; set; } = new();
}

public class MapNodeInfo
{
    public int Index { get; set; }
    public string Type { get; set; } = "";    // Monster, Elite, Boss, Shop, RestSite, Event, Treasure, Unknown
    public string Coord { get; set; } = "";
}

public class RewardInfo
{
    public int Index { get; set; }
    public string Type { get; set; } = "";   // card, gold, potion, relic
    public string Description { get; set; } = "";
}

public class EventOptionInfo
{
    public int Index { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsLocked { get; set; }
}

// AI 决策结果
public class AIDecision
{
    public string Action { get; set; } = "";   // play_card, end_turn, use_potion, choose_card, skip_reward, take_reward, choose_event, choose_map, rest, smith
    public int CardIndex { get; set; } = -1;
    public int TargetIndex { get; set; } = -1;
    public int PotionIndex { get; set; } = -1;
    public int RewardIndex { get; set; } = -1;
    public int EventOptionIndex { get; set; } = -1;
    public int MapNodeIndex { get; set; } = -1;
    public int CardChoiceIndex { get; set; } = -1;
    public int ShopItemIndex { get; set; } = -1;
    public string Reasoning { get; set; } = "";
}

// LLM 多轮对话消息
public record ChatMessage(string Role, string Content);

// 事件信息
public class EventInfo
{
    public string Name { get; set; } = "";
    public List<EventOptionInfo> Options { get; set; } = new();
}

// 商店信息
public class ShopInfo
{
    public List<ShopItemInfo> Items { get; set; } = new();
}

public class ShopItemInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";  // card, relic, potion
    public int Price { get; set; }
    public string Description { get; set; } = "";
}
