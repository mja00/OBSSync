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
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.StartGame))]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ResetPlayersLoadedValueClientRpc))]
    [HarmonyPrefix]
    public static void OnNewRoundStarted()
    {
        OBSSync.Instance.RoundStarting();
    }
    
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.EndOfGame))]
    [HarmonyPrefix]
    public static void StartOfRound_EndOfGame_Prefix()
    {
        OBSSync.Instance.RoundFinished();
    }
    
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Start))]
    [HarmonyPrefix]
    public static void OnGameJoined()
    {
        OBSSync.Instance.JoinedGame();
    }
    
    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.StartDisconnect))]
    [HarmonyPrefix]
    public static void OnGameLeave()
    {
        OBSSync.Instance.LeftGame();
    }
    
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.KillPlayerClientRpc))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> OnPlayerKilled(IEnumerable<CodeInstruction> instructions)
    {
        void ActualFunction(int playerId, int causeOfDeath)
        {
            string playerName = StartOfRound.Instance.allPlayerScripts[playerId].playerUsername;
            OBSSync.Instance.WriteTimestamppedEvent($"Player {playerName} died ({(CauseOfDeath) causeOfDeath})");
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
        OBSSync.Instance.WriteTimestamppedEvent($"Enemy {__instance.gameObject.name} died");
    }
    
    [HarmonyPatch(typeof(FlowermanAI), nameof(FlowermanAI.EnterAngerModeClientRpc))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> OnBrackenAngered(IEnumerable<CodeInstruction> instructions)
    {
        void ActualFunction(FlowermanAI self)
        {
            if (self.lookAtPlayer != null)
                OBSSync.Instance.WriteTimestamppedEvent($"Bracken angered towards {self.lookAtPlayer.playerUsername}");
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
            OBSSync.Instance.WriteTimestamppedEvent("Jester started winding");
        }

        FieldInfo previousState = typeof(JesterAI).GetField("previousState", BindingFlags.Instance | BindingFlags.NonPublic)!;

        return new CodeMatcher(instructions)
            .MatchForward(true,
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, previousState),
                new CodeMatch(OpCodes.Ldc_I4_1),
                new CodeMatch(OpCodes.Beq_S),
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
            OBSSync.Instance.WriteTimestamppedEvent($"Loot bug angered by {self.targetPlayer.playerUsername}");
        }

        FieldInfo inChase = typeof(HoarderBugAI).GetField("inChase", BindingFlags.Instance | BindingFlags.NonPublic)!;

        return new CodeMatcher(instructions)
            .MatchForward(true,
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, inChase),
                new CodeMatch(OpCodes.Brtrue),
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
            
            OBSSync.Instance.WriteTimestamppedEvent("Easter egg exploded");
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
            OBSSync.Instance.WriteTimestamppedEvent($"Player {self.playerUsername} took damage");
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
                    OBSSync.Instance.WriteTimestamppedEvent("Extreme fear event");
                    break;
                case > 0.75f:
                    OBSSync.Instance.WriteTimestamppedEvent("High fear event");
                    break;
                case >0.5f:
                    OBSSync.Instance.WriteTimestamppedEvent("Moderate fear event");
                    break;
            }
        }
        
        return new CodeMatcher(instructions)
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