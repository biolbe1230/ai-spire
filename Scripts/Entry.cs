using Godot.Bridge;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using AISpire.AI;
using AISpire.Config;

namespace AISpire.Scripts;

[ModInitializer("Init")]
public class Entry
{
    public static void Init()
    {
        var harmony = new Harmony("sts2.aispire.ai");
        harmony.PatchAll();
        ScriptManagerBridge.LookupScriptsInAssembly(typeof(Entry).Assembly);

        // 加载用户配置
        AIConfig.Load();

        // 加载 spire-codex 游戏数据
        GameDataLoader.Init(AIConfig.DataPath);

        // 初始化 AI 决策显示面板
        AIOverlay.Init();

        Log.Info("[AISpire] Mod initialized! AI player ready.");
    }
}

/// <summary>
/// 战斗回合开始时触发 AI 决策
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterPlayerTurnStart))]
public static class Patch_AfterPlayerTurnStart
{
    public static async void Postfix(CombatState combatState, Player player)
    {
        if (!AIConfig.Enabled) return;

        // 只处理本地玩家
        var me = LocalContext.GetMe(combatState);
        if (me == null || me != player) return;

        Log.Info("[AISpire] Player turn started, triggering AI...");
        try
        {
            // 等待一小段时间，让游戏状态稳定
            await Task.Delay(500);
            await AIDecisionEngine.HandleCombatTurn(combatState, player);
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] AI combat error: {e.Message}\n{e.StackTrace}");
        }
    }
}

/// <summary>
/// 战斗胜利后触发奖励收取 + 地图选路
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatVictory))]
public static class Patch_AfterCombatVictory
{
    public static async void Postfix(IRunState runState, CombatState? combatState, CombatRoom room)
    {
        if (!AIConfig.Enabled) return;

        var player = LocalContext.GetMe(runState);
        if (player == null) return;

        Log.Info("[AISpire] Combat victory detected, handling post-combat...");
        try
        {
            await Task.Delay(2000); // 等待胜利动画 / 奖励画面出现
            await AIDecisionEngine.HandlePostCombat(runState, player);
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] Post-combat error: {e.Message}\n{e.StackTrace}");
        }
    }
}

/// <summary>
/// 进入房间后触发非战斗场景 AI 决策（地图、事件、营地、商店、宝物房）
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterRoomEntered))]
public static class Patch_AfterRoomEntered
{
    public static async void Postfix(IRunState runState, AbstractRoom room)
    {
        if (!AIConfig.Enabled) return;

        try
        {
            var player = LocalContext.GetMe(runState);
            if (player == null) return;

            Log.Info($"[AISpire] Room entered: {room.GetType().Name} ({room.RoomType})");
            await AIDecisionEngine.HandleRoomEntered(room, runState, player);
        }
        catch (Exception e)
        {
            Log.Info($"[AISpire] AfterRoomEntered error: {e.Message}\n{e.StackTrace}");
        }
    }
}
