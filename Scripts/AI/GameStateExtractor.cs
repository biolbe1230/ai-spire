using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Context;

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
            Cost = card.EnergyCost.GetWithModifiers(CostModifiers.All),
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

    // ── Event State ──

    public static GameState ExtractEventState(EventRoom eventRoom, IRunState runState, Player player)
    {
        var state = new GameState
        {
            Screen = "event",
            Floor = runState.TotalFloor,
            ActIndex = runState.CurrentActIndex,
            Player = ExtractPlayerInfo(player)
        };

        try
        {
            var eventModel = eventRoom.CanonicalEvent;
            var eventInfo = new EventInfo();
            var options = new List<EventOptionInfo>();

            if (eventModel?.CurrentOptions != null)
            {
                int idx = 0;
                foreach (var opt in eventModel.CurrentOptions)
                {
                    options.Add(new EventOptionInfo
                    {
                        Index = idx,
                        Title = SafeFormat(() => opt.Title.GetFormattedText(), $"Option {idx}"),
                        Description = SafeFormat(() => opt.Description.GetFormattedText()),
                        IsLocked = opt.IsLocked
                    });
                    idx++;
                }
            }

            eventInfo.Options = options;
            state.Event = eventInfo;
            state.EventOptions = options;
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error extracting event state: {e.Message}");
        }

        return state;
    }

    // ── Rest Site State ──

    public static GameState ExtractRestSiteState(RestSiteRoom restRoom, IRunState runState, Player player)
    {
        var state = new GameState
        {
            Screen = "rest",
            Floor = runState.TotalFloor,
            ActIndex = runState.CurrentActIndex,
            Player = ExtractPlayerInfo(player)
        };

        try
        {
            var options = new List<EventOptionInfo>();
            if (restRoom.Options != null)
            {
                int idx = 0;
                foreach (var opt in restRoom.Options)
                {
                    options.Add(new EventOptionInfo
                    {
                        Index = idx,
                        Title = SafeFormat(() => opt.Title.GetFormattedText(), opt.OptionId),
                        Description = SafeFormat(() => opt.Description.GetFormattedText()),
                        IsLocked = !opt.IsEnabled
                    });
                    idx++;
                }
            }
            state.EventOptions = options;
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error extracting rest site state: {e.Message}");
        }

        return state;
    }

    // ── Shop State ──

    public static GameState ExtractShopState(MerchantRoom shopRoom, IRunState runState, Player player)
    {
        var state = new GameState
        {
            Screen = "shop",
            Floor = runState.TotalFloor,
            ActIndex = runState.CurrentActIndex,
            Player = ExtractPlayerInfo(player)
        };

        try
        {
            var shopInfo = new ShopInfo();
            var inventory = shopRoom.Inventory;
            int idx = 0;

            if (inventory != null)
            {
                // Cards
                foreach (var entry in inventory.CardEntries)
                {
                    if (!entry.IsStocked) continue;
                    var cardName = SafeFormat(() => entry.CreationResult?.Card?.Title ?? "Card");
                    shopInfo.Items.Add(new ShopItemInfo
                    {
                        Index = idx++,
                        Name = cardName,
                        Type = "card",
                        Price = entry.Cost,
                        Description = SafeFormat(() => entry.CreationResult?.Card?.Description.GetFormattedText() ?? "")
                    });
                }

                // Relics
                foreach (var entry in inventory.RelicEntries)
                {
                    if (!entry.IsStocked) continue;
                    var relicName = SafeFormat(() => entry.Model?.Title.GetFormattedText() ?? "Relic");
                    shopInfo.Items.Add(new ShopItemInfo
                    {
                        Index = idx++,
                        Name = relicName,
                        Type = "relic",
                        Price = entry.Cost,
                        Description = SafeFormat(() => entry.Model?.Description.GetFormattedText() ?? "")
                    });
                }

                // Potions
                foreach (var entry in inventory.PotionEntries)
                {
                    if (!entry.IsStocked) continue;
                    var potName = SafeFormat(() => entry.Model?.Title.GetFormattedText() ?? "Potion");
                    shopInfo.Items.Add(new ShopItemInfo
                    {
                        Index = idx++,
                        Name = potName,
                        Type = "potion",
                        Price = entry.Cost,
                        Description = SafeFormat(() => entry.Model?.Description.GetFormattedText() ?? "")
                    });
                }

                // Card removal
                if (inventory.CardRemovalEntry is { IsStocked: true } removal)
                {
                    shopInfo.Items.Add(new ShopItemInfo
                    {
                        Index = idx++,
                        Name = "Remove Card",
                        Type = "removal",
                        Price = removal.Cost
                    });
                }
            }

            state.Shop = shopInfo;
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Error extracting shop state: {e.Message}");
        }

        return state;
    }
}
