using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Discord;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;

namespace OBSSync;

internal static class Patches
{
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ResetPlayersLoadedValueClientRpc))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> OnNewRoundStarted(IEnumerable<CodeInstruction> instructions)
    {
        void ActualFunction()
        {
            ObsSyncPlugin.Instance.RoundStarting();
        }

        return new CodeMatcher(instructions)
            .SkipRpcCrap()
            .Insert(new CodeInstruction(OpCodes.Call, ((Delegate)ActualFunction).Method))
            .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.EndOfGame))]
    [HarmonyPrefix]
    public static void StartOfRound_EndOfGame_Prefix()
    {
        ObsSyncPlugin.Instance.RoundFinished();
    }

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Start))]
    [HarmonyPrefix]
    public static void OnGameJoined()
    {
        ObsSyncPlugin.Instance.JoinedGame();
    }

    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.StartDisconnect))]
    [HarmonyPrefix]
    public static void OnGameLeave()
    {
        ObsSyncPlugin.Instance.LeftGame();
    }

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.KillPlayerClientRpc))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> OnPlayerKilled(IEnumerable<CodeInstruction> instructions)
    {
        void ActualFunction(int playerId, int causeOfDeath)
        {
            string playerName = StartOfRound.Instance.allPlayerScripts[playerId].playerUsername;
            ObsSyncPlugin.Instance.WriteTimestamppedEvent($"Player {playerName} died ({(CauseOfDeath)causeOfDeath})");
        }

        return new CodeMatcher(instructions)
            .SkipRpcCrap()
            .Insert(
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldarg, 4),
                new CodeInstruction(OpCodes.Call, ((Delegate)ActualFunction).Method))
            .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.KillEnemy))]
    [HarmonyPostfix]
    public static void OnEnemyKilled(EnemyAI __instance)
    {
        // Don't care about these birds...
        if (__instance.gameObject.name.Contains("Doublewinged")) return;

        ObsSyncPlugin.Instance.WriteTimestamppedEvent($"Enemy {__instance.gameObject.name} died");
    }

    [HarmonyPatch(typeof(FlowermanAI), nameof(FlowermanAI.EnterAngerModeClientRpc))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> OnBrackenAngered(IEnumerable<CodeInstruction> instructions)
    {
        void ActualFunction(FlowermanAI self)
        {
            if (self.lookAtPlayer != null)
                ObsSyncPlugin.Instance.WriteTimestamppedEvent($"Bracken angered towards {self.lookAtPlayer.playerUsername}");
        }

        return new CodeMatcher(instructions)
            .SkipRpcCrap()
            .Insert(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, ((Delegate)ActualFunction).Method))
            .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(JesterAI), nameof(JesterAI.Update))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> OnJesterStartWinding(IEnumerable<CodeInstruction> instructions)
    {
        void ActualFunction(JesterAI self)
        {
            ObsSyncPlugin.Instance.WriteTimestamppedEvent("Jester started winding");
        }

        FieldInfo previousState = typeof(JesterAI).GetField("previousState", BindingFlags.Instance | BindingFlags.NonPublic)!;

        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldc_I4_1),
                new CodeMatch(OpCodes.Stfld, previousState))
            .Insert(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, ((Delegate)ActualFunction).Method))
            .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(HoarderBugAI), nameof(HoarderBugAI.Update))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> OnLootBugAngered(IEnumerable<CodeInstruction> instructions)
    {
        void ActualFunction(HoarderBugAI self)
        {
            ObsSyncPlugin.Instance.WriteTimestamppedEvent($"Loot bug angered by {self.targetPlayer.playerUsername}");
        }

        FieldInfo inChase = typeof(HoarderBugAI).GetField("inChase", BindingFlags.Instance | BindingFlags.NonPublic)!;

        return new CodeMatcher(instructions)
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldc_I4_1),
                new CodeMatch(OpCodes.Stfld, inChase))
            .Insert(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, ((Delegate)ActualFunction).Method))
            .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(StunGrenadeItem), nameof(StunGrenadeItem.ExplodeStunGrenade))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> OnEggExploded(IEnumerable<CodeInstruction> instructions)
    {
        void ActualFunction(StunGrenadeItem self)
        {
            // Easter eggs share code with stun grenades
            if (!self.name.Contains("Easter egg")) return;

            ObsSyncPlugin.Instance.WriteTimestamppedEvent("Easter egg exploded");
        }

        FieldInfo hasExploded = typeof(StunGrenadeItem).GetField("hasExploded", BindingFlags.Instance | BindingFlags.Public)!;

        return new CodeMatcher(instructions)
            .MatchForward(true,
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldc_I4_1),
                new CodeMatch(OpCodes.Stfld, hasExploded))
            .Insert(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, ((Delegate)ActualFunction).Method))
            .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayerClientRpc))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> OnPlayerDamaged(IEnumerable<CodeInstruction> instructions)
    {
        void ActualFunction(PlayerControllerB self)
        {
            ObsSyncPlugin.Instance.WriteTimestamppedEvent($"Player {self.playerUsername} took damage");
        }

        return new CodeMatcher(instructions)
            .SkipRpcCrap()
            .Insert(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, ((Delegate)ActualFunction).Method))
            .InstructionEnumeration();
    }

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.JumpToFearLevel))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> OnJumpToFearLevel(IEnumerable<CodeInstruction> instructions)
    {
        void ActualFunction()
        {
            switch (StartOfRound.Instance.fearLevel)
            {
                case > 0.9f:
                    ObsSyncPlugin.Instance.WriteTimestamppedEvent("Extreme fear event");
                    break;
                case > 0.75f:
                    ObsSyncPlugin.Instance.WriteTimestamppedEvent("High fear event");
                    break;
                case > 0.4f:
                    ObsSyncPlugin.Instance.WriteTimestamppedEvent("Moderate fear event");
                    break;
                default:
                    ObsSyncPlugin.Instance.WriteTimestamppedEvent("Low fear event");
                    break;
            }
        }

        return new CodeMatcher(instructions)
            .End()
            .MatchBack(false, new CodeMatch(OpCodes.Ret))
            .Insert(new CodeInstruction(OpCodes.Call, ((Delegate)ActualFunction).Method))
            .InstructionEnumeration();
    }

    private static CodeMatcher SkipRpcCrap(this CodeMatcher matcher)
    {
        FieldInfo rpcExecStage = typeof(NetworkBehaviour).GetField("__rpc_exec_stage", BindingFlags.Instance | BindingFlags.NonPublic)!;

        for (int i = 0; i < 2; i++)
            matcher.MatchForward(true, new CodeMatch(OpCodes.Ldarg_0), new CodeMatch(OpCodes.Ldfld, rpcExecStage));
        matcher.MatchForward(true, new CodeMatch(OpCodes.Ret), new CodeMatch(OpCodes.Nop));
        matcher.Advance(1);

        matcher.ThrowIfInvalid("Could not match for rpc stuff");

        return matcher;
    }
}