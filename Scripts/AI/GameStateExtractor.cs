using AISpire.Config;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Entities.Merchant;

namespace AISpire.AI;

public static class GameStateExtractor
{
    private static string SafeFormat(Func<string> getText, string fallback = "")
    {
        try { return getText() ?? fallback; }
        catch { return fallback; }
    }

    public static GameState ExtractCombatState(CombatState combatState, Player player)
    {
        var state = new GameState
        {
            Screen = "combat",
            Floor = combatState.RunState.TotalFloor,
            ActIndex = combatState.RunState.CurrentActIndex,
            Player = ExtractPlayerInfo(player),
            Combat = ExtractCombatInfo(combatState, player)
        };
        return state;
    }

    public static GameState ExtractMapState(IRunState runState, Player player)
    {
        var state = new GameState
        {
            Screen = "map",
            Floor = runState.TotalFloor,
            ActIndex = runState.CurrentActIndex,
            Player = ExtractPlayerInfo(player),
            Map = ExtractMapInfo(runState)
        };
        return state;
    }

    public static PlayerInfo ExtractPlayerInfo(Player player)
    {
        var info = new PlayerInfo
        {
            Character = player.Character?.Id.Entry ?? "Unknown",
            Hp = player.Creature.CurrentHp,
            MaxHp = player.Creature.MaxHp,
            Gold = player.Gold,
            DeckSize = player.Deck?.Cards.Count ?? 0
        };

        // 遗物
        try
        {
            foreach (var relic in player.Relics)
            {
                var relicName = SafeFormat(() => relic.Title.GetFormattedText(), "Relic");
                string relicId = "";
                try { relicId = relic.Id?.Entry ?? ""; } catch { }
                var codexRelic = GameDataLoader.FindRelic(relicId, relicName);
                var desc = codexRelic != null
                    ? GameDataLoader.CleanDescription(codexRelic.Description)
                    : SafeFormat(() => relic.Description.GetFormattedText());
                info.Relics.Add(new RelicInfo { Name = relicName, Description = desc });
            }
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error extracting relics: {e.Message}");
        }

        // 药水
        try
        {
            var slots = player.PotionSlots;
            for (int i = 0; i < slots.Count; i++)
            {
                var potion = slots[i];
                if (potion != null)
                {
                    info.Potions.Add(new PotionInfo
                    {
                        Index = i,
                        Name = SafeFormat(() => potion.Title.GetFormattedText(), "Potion"),
                        Description = SafeFormat(() => potion.Description.GetFormattedText()),
                        NeedsTarget = false // 简化处理
                    });
                }
            }
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error extracting potions: {e.Message}");
        }

        return info;
    }

    private static CombatInfo ExtractCombatInfo(CombatState combatState, Player player)
    {
        var pcs = player.PlayerCombatState;
        var creature = player.Creature;

        var info = new CombatInfo
        {
            Energy = pcs.Energy,
            MaxEnergy = pcs.MaxEnergy,
            Block = creature.Block,
            Hp = creature.CurrentHp,
            MaxHp = creature.MaxHp,
            DrawPileCount = pcs.DrawPile.Cards.Count,
            DiscardPileCount = pcs.DiscardPile.Cards.Count,
            ExhaustPileCount = pcs.ExhaustPile.Cards.Count
        };

        // 玩家 Powers
        try
        {
            foreach (var power in creature.Powers)
            {
                var powerName = SafeFormat(() => power.Title.GetFormattedText(), "Power");
                string powerId = "";
                try { powerId = power.Id?.Entry ?? ""; } catch { }
                var codexPower = GameDataLoader.FindPower(powerId, powerName);
                var desc = codexPower != null
                    ? GameDataLoader.CleanDescription(codexPower.Description)
                    : "";
                info.PlayerPowers.Add(new PowerInfo
                {
                    Name = powerName,
                    Description = desc,
                    Amount = power.Amount,
                    IsDebuff = power.TypeForCurrentAmount == PowerType.Debuff
                });
            }
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error extracting player powers: {e.Message}");
        }

        // 手牌
        try
        {
            var cards = pcs.Hand.Cards;
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                info.Hand.Add(ExtractCardInfo(card, i));
            }
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error extracting hand: {e.Message}");
        }

        // 敌人
        try
        {
            var enemies = combatState.HittableEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                info.Enemies.Add(ExtractEnemyInfo(enemies[i], i, combatState));
            }
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error extracting enemies: {e.Message}");
        }

        return info;
    }

    private static CardInfo ExtractCardInfo(CardModel card, int index)
    {
        // 尝试获取卡牌内部 ID 用于 codex 查找
        string cardId = "";
        try { cardId = card.Id?.Entry ?? ""; } catch { }

        // 优先使用 codex 的精确描述（已解析 SmartFormat 变量）
        string description = "";
        int damage = 0, block = 0, hitCount = 1;

        var codexCard = GameDataLoader.FindCard(cardId, card.Title);
        if (codexCard != null)
        {
            description = GameDataLoader.CleanDescription(codexCard.Description);
            damage = codexCard.Damage ?? 0;
            block = codexCard.Block ?? 0;
            hitCount = codexCard.HitCount ?? 1;
        }

        if (string.IsNullOrEmpty(description))
            description = SafeFormat(() => card.Description.GetFormattedText(), card.Title);

        return new CardInfo
        {
            Index = index,
            CardId = cardId,
            Name = card.Title,
            Cost = card.EnergyCost.CostsX ? -1 : card.EnergyCost.GetWithModifiers(CostModifiers.All),
            Type = card.Type.ToString(),
            Target = card.TargetType.ToString(),
            CanPlay = card.CanPlay(),
            IsUpgraded = card.IsUpgraded,
            Description = description,
            Damage = damage,
            Block = block,
            HitCount = hitCount
        };
    }

    private static EnemyInfo ExtractEnemyInfo(Creature enemy, int index, CombatState combatState)
    {
        var info = new EnemyInfo
        {
            Index = index,
            Name = enemy.Name,
            Hp = enemy.CurrentHp,
            MaxHp = enemy.MaxHp,
            Block = enemy.Block
        };

        // 意图
        try
        {
            var monster = enemy.Monster;
            if (monster?.NextMove?.Intents != null)
            {
                // 提取当前招式 ID/Name
                try
                {
                    info.IntentMoveId = monster.NextMove.StateId ?? "";
                    // 尝试从 codex 匹配招式中文名
                    var codexMonster = GameDataLoader.FindMonster(enemy.Name);
                    if (codexMonster?.Moves != null && !string.IsNullOrEmpty(info.IntentMoveId))
                    {
                        var matchedMove = codexMonster.Moves.FirstOrDefault(
                            m => string.Equals(m.Id, info.IntentMoveId, StringComparison.OrdinalIgnoreCase));
                        if (matchedMove != null)
                            info.IntentMoveName = matchedMove.Name;
                    }
                }
                catch { }

                var intents = monster.NextMove.Intents;
                string intentType = "Unknown";
                int totalDamage = 0;
                int totalHits = 0;

                foreach (var intent in intents)
                {
                    var iType = intent.IntentType;
                    if (intent is AttackIntent attackIntent)
                    {
                        intentType = "Attack";
                        try
                        {
                            totalDamage += attackIntent.GetSingleDamage(combatState.Allies, enemy);
                            totalHits += Math.Max(1, attackIntent.Repeats);
                        }
                        catch { }
                    }
                    else if (intentType == "Unknown")
                    {
                        intentType = iType.ToString();
                    }
                }

                info.Intent = intentType;
                info.IntentDamage = totalDamage;
                info.IntentHits = totalHits;
            }
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error extracting intent for {enemy.Name}: {e.Message}");
        }

        // Powers
        try
        {
            foreach (var power in enemy.Powers)
            {
                var powerName = SafeFormat(() => power.Title.GetFormattedText(), "Power");
                string powerId = "";
                try { powerId = power.Id?.Entry ?? ""; } catch { }
                var codexPower = GameDataLoader.FindPower(powerId, powerName);
                var desc = codexPower != null
                    ? GameDataLoader.CleanDescription(codexPower.Description)
                    : "";
                info.Powers.Add(new PowerInfo
                {
                    Name = powerName,
                    Description = desc,
                    Amount = power.Amount,
                    IsDebuff = power.TypeForCurrentAmount == PowerType.Debuff
                });
            }
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error extracting enemy powers: {e.Message}");
        }

        return info;
    }

    private static MapInfo ExtractMapInfo(IRunState runState)
    {
        var info = new MapInfo
        {
            CurrentCoord = runState.CurrentMapCoord.ToString()
        };

        try
        {
            var currentPoint = runState.CurrentMapPoint;
            if (currentPoint != null)
            {
                int idx = 0;
                foreach (var child in currentPoint.Children)
                {
                    info.AvailableNodes.Add(new MapNodeInfo
                    {
                        Index = idx++,
                        Type = child.PointType.ToString(),
                        Coord = child.coord.ToString()
                    });
                }
            }
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error extracting map: {e.Message}");
        }

        return info;
    }

    // ─── 事件 ───

    public static GameState ExtractEventState(EventRoom eventRoom, IRunState runState, Player player)
    {
        var state = new GameState
        {
            Screen = "event",
            Floor = runState.TotalFloor,
            ActIndex = runState.CurrentActIndex,
            Player = ExtractPlayerInfo(player),
            EventOptions = new List<EventOptionInfo>()
        };

        try
        {
            var evt = eventRoom.LocalMutableEvent;
            if (evt != null)
            {
                state.EventTitle = SafeFormat(() => evt.Title?.GetFormattedText() ?? "", "事件");
                state.EventDescription = SafeFormat(() => evt.Description?.GetFormattedText() ?? "", "");
                var options = evt.CurrentOptions;
                if (options != null)
                {
                    int idx = 0;
                    foreach (var opt in options)
                    {
                        state.EventOptions.Add(new EventOptionInfo
                        {
                            Index = idx++,
                            Title = SafeFormat(() => opt.Title?.GetFormattedText() ?? "", $"选项{idx}"),
                            Description = SafeFormat(() => opt.Description?.GetFormattedText() ?? "", ""),
                            IsLocked = opt.IsLocked
                        });
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error extracting event: {e.Message}");
        }

        return state;
    }

    // ─── 营地 ───

    public static GameState ExtractRestSiteState(RestSiteRoom restRoom, IRunState runState, Player player)
    {
        var state = new GameState
        {
            Screen = "rest",
            Floor = runState.TotalFloor,
            ActIndex = runState.CurrentActIndex,
            Player = ExtractPlayerInfo(player),
            RestSiteOptions = new List<RestSiteOptionInfo>()
        };

        try
        {
            var options = restRoom.Options;
            if (options != null)
            {
                int idx = 0;
                foreach (var opt in options)
                {
                    state.RestSiteOptions.Add(new RestSiteOptionInfo
                    {
                        Index = idx++,
                        Id = opt.OptionId ?? "",
                        Name = SafeFormat(() => opt.Title?.GetFormattedText() ?? "", opt.OptionId ?? "选项"),
                        Description = SafeFormat(() => opt.Description?.GetFormattedText() ?? "", ""),
                        IsEnabled = opt.IsEnabled
                    });
                }
            }
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error extracting rest site: {e.Message}");
        }

        return state;
    }

    // ─── 商店 ───

    // 保存最近一次提取的商店条目引用，供 ActionExecutor 调用购买
    private static readonly List<object> _lastShopEntries = new();
    public static object? GetShopEntry(int index) =>
        index >= 0 && index < _lastShopEntries.Count ? _lastShopEntries[index] : null;

    public static GameState ExtractShopState(MerchantRoom shopRoom, IRunState runState, Player player)
    {
        var state = new GameState
        {
            Screen = "shop",
            Floor = runState.TotalFloor,
            ActIndex = runState.CurrentActIndex,
            Player = ExtractPlayerInfo(player),
            ShopItems = new List<ShopItemInfo>()
        };

        _lastShopEntries.Clear();

        try
        {
            var inventory = shopRoom.Inventory;
            if (inventory == null) return state;

            int idx = 0;

            // 角色卡牌
            foreach (var entry in inventory.CharacterCardEntries)
            {
                if (!entry.IsStocked) continue;
                var card = entry.CreationResult?.Card;
                string name = card?.Title ?? "Card";
                string cardId = "";
                try { cardId = card?.Id?.Entry ?? ""; } catch { }
                var codex = GameDataLoader.FindCard(cardId, name);
                string desc = codex != null ? GameDataLoader.CleanDescription(codex.Description) : "";

                _lastShopEntries.Add(entry);
                state.ShopItems.Add(new ShopItemInfo
                {
                    Index = idx++, Type = "Card", Name = name,
                    Description = desc, Price = entry.Cost, CanAfford = entry.EnoughGold
                });
            }

            // 无色卡牌
            foreach (var entry in inventory.ColorlessCardEntries)
            {
                if (!entry.IsStocked) continue;
                var card = entry.CreationResult?.Card;
                string name = card?.Title ?? "Card";
                string cardId = "";
                try { cardId = card?.Id?.Entry ?? ""; } catch { }
                var codex = GameDataLoader.FindCard(cardId, name);
                string desc = codex != null ? GameDataLoader.CleanDescription(codex.Description) : "";

                _lastShopEntries.Add(entry);
                state.ShopItems.Add(new ShopItemInfo
                {
                    Index = idx++, Type = "Card", Name = name,
                    Description = desc, Price = entry.Cost, CanAfford = entry.EnoughGold
                });
            }

            // 遗物
            foreach (var entry in inventory.RelicEntries)
            {
                if (!entry.IsStocked) continue;
                var relic = entry.Model;
                string name = SafeFormat(() => relic?.Title?.GetFormattedText() ?? "", "Relic");
                string relicId = "";
                try { relicId = relic?.Id?.Entry ?? ""; } catch { }
                var codex = GameDataLoader.FindRelic(relicId, name);
                string desc = codex != null ? GameDataLoader.CleanDescription(codex.Description)
                    : SafeFormat(() => relic?.Description?.GetFormattedText() ?? "");

                _lastShopEntries.Add(entry);
                state.ShopItems.Add(new ShopItemInfo
                {
                    Index = idx++, Type = "Relic", Name = name,
                    Description = desc, Price = entry.Cost, CanAfford = entry.EnoughGold
                });
            }

            // 药水
            foreach (var entry in inventory.PotionEntries)
            {
                if (!entry.IsStocked) continue;
                var potion = entry.Model;
                string name = SafeFormat(() => potion?.Title?.GetFormattedText() ?? "", "Potion");
                string desc = SafeFormat(() => potion?.Description?.GetFormattedText() ?? "");

                _lastShopEntries.Add(entry);
                state.ShopItems.Add(new ShopItemInfo
                {
                    Index = idx++, Type = "Potion", Name = name,
                    Description = desc, Price = entry.Cost, CanAfford = entry.EnoughGold
                });
            }

            // 卡牌移除
            var removal = inventory.CardRemovalEntry;
            if (removal != null && removal.IsStocked)
            {
                _lastShopEntries.Add(removal);
                state.ShopItems.Add(new ShopItemInfo
                {
                    Index = idx++, Type = "CardRemoval", Name = Loc.CardRemoval,
                    Description = Loc.CardRemovalDesc,
                    Price = removal.Cost, CanAfford = removal.EnoughGold
                });
            }
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error extracting shop: {e.Message}");
        }

        return state;
    }

    // ─── 宝物房遗物选择 ───

    public static GameState ExtractTreasureState(IRunState runState, Player player)
    {
        var state = new GameState
        {
            Screen = "treasure",
            Floor = runState.TotalFloor,
            ActIndex = runState.CurrentActIndex,
            Player = ExtractPlayerInfo(player),
            RelicChoices = new List<RelicChoiceInfo>()
        };
        // 宝物房遗物在 TreasureRoomRelicSynchronizer 内部管理
        // 具体遗物信息会在 AIDecisionEngine 中通过 Synchronizer 获取
        return state;
    }
}
