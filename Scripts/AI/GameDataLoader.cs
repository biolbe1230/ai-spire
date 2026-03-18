using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Logging;

namespace AISpire.AI;

// ── JSON data models matching spire-codex structure ──

public class CodexCard
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("cost")] public int? Cost { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("rarity")] public string Rarity { get; set; } = "";
    [JsonPropertyName("target")] public string Target { get; set; } = "";
    [JsonPropertyName("color")] public string Color { get; set; } = "";
    [JsonPropertyName("damage")] public int? Damage { get; set; }
    [JsonPropertyName("block")] public int? Block { get; set; }
    [JsonPropertyName("hit_count")] public int? HitCount { get; set; }
    [JsonPropertyName("powers_applied")] public List<CodexPowerApplied>? PowersApplied { get; set; }
    [JsonPropertyName("cards_draw")] public int? CardsDraw { get; set; }
    [JsonPropertyName("energy_gain")] public int? EnergyGain { get; set; }
    [JsonPropertyName("hp_loss")] public int? HpLoss { get; set; }
    [JsonPropertyName("keywords")] public List<string>? Keywords { get; set; }
    [JsonPropertyName("is_x_cost")] public bool? IsXCost { get; set; }
    [JsonPropertyName("upgrade")] public Dictionary<string, JsonElement>? Upgrade { get; set; }
}

public class CodexPowerApplied
{
    [JsonPropertyName("power")] public string Power { get; set; } = "";
    [JsonPropertyName("amount")] public int Amount { get; set; }
}

public class CodexMonster
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("min_hp")] public int MinHp { get; set; }
    [JsonPropertyName("max_hp")] public int MaxHp { get; set; }
    [JsonPropertyName("moves")] public List<CodexMove>? Moves { get; set; }
    [JsonPropertyName("damage_values")] public Dictionary<string, CodexDamageEntry>? DamageValues { get; set; }
    [JsonPropertyName("block_values")] public Dictionary<string, int>? BlockValues { get; set; }
}

public class CodexMove
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

public class CodexDamageEntry
{
    [JsonPropertyName("normal")] public int Normal { get; set; }
    [JsonPropertyName("ascension")] public int Ascension { get; set; }
}

public class CodexPower
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
}

public class CodexRelic
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("rarity")] public string Rarity { get; set; } = "";
}

/// <summary>
/// 加载 spire-codex 数据，提供卡牌/敌怪/能力的精确参考信息
/// </summary>
public static class GameDataLoader
{
    public static Dictionary<string, CodexCard> CardsById { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public static Dictionary<string, CodexCard> CardsByName { get; private set; } = new();
    public static Dictionary<string, CodexMonster> MonstersByName { get; private set; } = new();
    public static Dictionary<string, CodexPower> PowersByName { get; private set; } = new();
    public static Dictionary<string, CodexPower> PowersById { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public static Dictionary<string, CodexRelic> RelicsById { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public static Dictionary<string, CodexRelic> RelicsByName { get; private set; } = new();

    public static bool IsLoaded { get; private set; }

    public static void Init(string dataDir)
    {
        try
        {
            Log.Info($"[AISpire] Loading game data from: {dataDir}");
            LoadCards(Path.Combine(dataDir, "cards.json"));
            LoadMonsters(Path.Combine(dataDir, "monsters.json"));
            LoadPowers(Path.Combine(dataDir, "powers.json"));
            LoadRelics(Path.Combine(dataDir, "relics.json"));
            IsLoaded = true;
            Log.Info($"[AISpire] Data loaded: {CardsById.Count} cards, {MonstersByName.Count} monsters, {PowersByName.Count} powers, {RelicsById.Count} relics");
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Failed to load game data: {e.Message}");
            IsLoaded = false;
        }
    }

    private static void LoadCards(string path)
    {
        if (!File.Exists(path)) { Log.Info($"[AISpire] Not found: {path}"); return; }
        var cards = JsonSerializer.Deserialize<List<CodexCard>>(File.ReadAllText(path)) ?? new();
        foreach (var card in cards)
        {
            if (!string.IsNullOrEmpty(card.Id))
                CardsById[card.Id] = card;
            if (!string.IsNullOrEmpty(card.Name))
                CardsByName.TryAdd(card.Name, card);
        }
    }

    private static void LoadMonsters(string path)
    {
        if (!File.Exists(path)) { Log.Info($"[AISpire] Not found: {path}"); return; }
        var monsters = JsonSerializer.Deserialize<List<CodexMonster>>(File.ReadAllText(path)) ?? new();
        foreach (var m in monsters)
        {
            if (!string.IsNullOrEmpty(m.Name))
                MonstersByName.TryAdd(m.Name, m);
        }
    }

    private static void LoadPowers(string path)
    {
        if (!File.Exists(path)) { Log.Info($"[AISpire] Not found: {path}"); return; }
        var powers = JsonSerializer.Deserialize<List<CodexPower>>(File.ReadAllText(path)) ?? new();
        foreach (var p in powers)
        {
            if (!string.IsNullOrEmpty(p.Id))
                PowersById[p.Id] = p;
            if (!string.IsNullOrEmpty(p.Name))
                PowersByName.TryAdd(p.Name, p);
        }
    }

    private static void LoadRelics(string path)
    {
        if (!File.Exists(path)) { Log.Info($"[AISpire] Not found: {path}"); return; }
        var relics = JsonSerializer.Deserialize<List<CodexRelic>>(File.ReadAllText(path)) ?? new();
        foreach (var r in relics)
        {
            if (!string.IsNullOrEmpty(r.Id))
                RelicsById[r.Id] = r;
            if (!string.IsNullOrEmpty(r.Name))
                RelicsByName.TryAdd(r.Name, r);
        }
    }

    // ── 查询辅助 ──

    public static CodexCard? FindCard(string? cardId, string? cardName)
    {
        CodexCard? card = null;
        if (!string.IsNullOrEmpty(cardId))
            CardsById.TryGetValue(cardId, out card);
        if (card == null && !string.IsNullOrEmpty(cardName))
            CardsByName.TryGetValue(cardName, out card);
        return card;
    }

    public static CodexMonster? FindMonster(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        MonstersByName.TryGetValue(name, out var m);
        return m;
    }

    public static CodexPower? FindPower(string? powerId, string? powerName)
    {
        CodexPower? power = null;
        if (!string.IsNullOrEmpty(powerId))
            PowersById.TryGetValue(powerId, out power);
        if (power == null && !string.IsNullOrEmpty(powerName))
            PowersByName.TryGetValue(powerName, out power);
        return power;
    }

    public static CodexRelic? FindRelic(string? relicId, string? relicName)
    {
        CodexRelic? relic = null;
        if (!string.IsNullOrEmpty(relicId))
            RelicsById.TryGetValue(relicId, out relic);
        if (relic == null && !string.IsNullOrEmpty(relicName))
            RelicsByName.TryGetValue(relicName, out relic);
        return relic;
    }

    /// <summary>
    /// 清洗描述文本：移除 [gold][/gold] 等格式标签，转换 [energy:X] 为可读文本
    /// </summary>
    public static string CleanDescription(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        text = Regex.Replace(text, @"\[gold\](.*?)\[/gold\]", "$1");
        text = Regex.Replace(text, @"\[energy:(\d+)\]", "$1能量");
        text = Regex.Replace(text, @"\[star:(\d+)\]", "$1星");
        text = Regex.Replace(text, @"\[/?[^\]]+\]", "");
        return text.Replace("\n", " ").Trim();
    }

    /// <summary>
    /// 根据招式 ID 查找其伤害值（normal）。
    /// damage_values key 是 PascalCase（如 "HammerUppercut"），move id 是 UPPER_SNAKE_CASE（如 "HAMMER_UPPERCUT"），需匹配。
    /// </summary>
    public static int? FindMoveDamage(CodexMonster monster, string moveId)
    {
        if (monster.DamageValues == null || string.IsNullOrEmpty(moveId)) return null;
        var pascal = SnakeToPascal(moveId);
        // 精确匹配
        if (monster.DamageValues.TryGetValue(pascal, out var entry))
            return entry.Normal;
        // 部分匹配：有些 key 只用了招式名的一部分（如 move "SPIKE_EXPLOSION" → key "Explosion"）
        foreach (var kv in monster.DamageValues)
        {
            if (pascal.Contains(kv.Key, StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Contains(pascal, StringComparison.OrdinalIgnoreCase))
                return kv.Value.Normal;
        }
        return null;
    }

    /// <summary>
    /// 根据招式 ID 查找其格挡值。
    /// </summary>
    public static int? FindMoveBlock(CodexMonster monster, string moveId)
    {
        if (monster.BlockValues == null || string.IsNullOrEmpty(moveId)) return null;
        var pascal = SnakeToPascal(moveId);
        foreach (var kv in monster.BlockValues)
        {
            if (string.Equals(kv.Key, pascal, StringComparison.OrdinalIgnoreCase) ||
                pascal.Contains(kv.Key, StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Contains(pascal, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }
        return null;
    }

    /// <summary>
    /// 构建招式描述字符串，包含伤害/格挡数值
    /// </summary>
    public static string DescribeMove(CodexMonster monster, CodexMove move)
    {
        var parts = new List<string> { move.Name };
        var dmg = FindMoveDamage(monster, move.Id);
        var blk = FindMoveBlock(monster, move.Id);
        if (dmg.HasValue) parts.Add($"伤害:{dmg.Value}");
        if (blk.HasValue) parts.Add($"格挡:{blk.Value}");
        if (dmg == null && blk == null) parts.Add("非攻防"); // buff/debuff/summon 等
        return string.Join(" ", parts);
    }

    /// <summary>
    /// UPPER_SNAKE_CASE → PascalCase: "HAMMER_UPPERCUT" → "HammerUppercut"
    /// </summary>
    private static string SnakeToPascal(string snakeCase)
    {
        return string.Concat(
            snakeCase.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => char.ToUpper(w[0]) + w[1..].ToLower()));
    }
}
