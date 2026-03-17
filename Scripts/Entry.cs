using Godot.Bridge;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
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

        // 加载 spire-codex 游戏数据
        GameDataLoader.Init(AIConfig.DataPath);

        // 初始化 AI 决策显示面板
        AIOverlay.Init();

        Log.Debug("[AISpire] Mod initialized! AI player ready.");
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

        Log.Debug("[AISpire] Player turn started, triggering AI...");
        try
        {
            // 等待一小段时间，让游戏状态稳定
            await Task.Delay(500);
            await AIDecisionEngine.HandleCombatTurn(combatState, player);
        }
        catch (Exception e)
        {
            Log.Debug($"[AISpire] AI combat error: {e.Message}\n{e.StackTrace}");
        }
    }
}
