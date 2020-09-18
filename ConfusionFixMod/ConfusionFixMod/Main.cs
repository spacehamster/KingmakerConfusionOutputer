using UnityModManagerNet;
using System;
using System.Reflection;
using System.Linq;
using Kingmaker.Blueprints;
using Kingmaker;
using Kingmaker.Blueprints.Classes;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.Designers.Mechanics.Buffs;
using System.Collections.Generic;
using Kingmaker.Blueprints.Items;
using System.IO;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.EntitySystem.Entities;
using TurnBased.Utility;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Parts;
using TurnBased.Controllers;
using UnityEngine;
using Kingmaker.Blueprints.Root.Strings.GameLog;
using Kingmaker.UI.Log;
using Kingmaker.UI._ConsoleUI.CombatLog;
using Kingmaker.UI._ConsoleUI.Context.InGame;
using Kingmaker.Controllers.Units;
using System.Reflection.Emit;
using Harmony12;

namespace ConfusionFixMod
{
    internal class Main
    {

        internal static UnityModManagerNet.UnityModManager.ModEntry.ModLogger logger;
        internal static Harmony12.HarmonyInstance harmony;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            try
            {
                logger = modEntry.Logger;
                harmony = Harmony12.HarmonyInstance.Create(modEntry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception ex)
            {
                //DebugError(ex);
                throw ex;
            }
            return true;

        }

        public static T GetField<T>(object obj, string name)
        {
            return (T)Harmony12.AccessTools.Field(obj.GetType(), name).GetValue(obj);
        }

        public static CombatLogVM getCombatLogVm()
        {
            var in_game_ui_context = Game.Instance?.RootUiContext?.InGameUiContext;
            if (in_game_ui_context == null)
            {
                return null;
            }
            var static_context = GetField<InGameUiStaticPartContext>(Game.Instance?.RootUiContext?.InGameUiContext, "m_StaticPartContext");
            if (static_context == null)
            {
                return null;
            }
            var combat_log_vm = GetField<CombatLogVM>(static_context, "m_CombatLogVm");
            return combat_log_vm;
        }

        public static void AddBattleLogMessage(string message, string tooltip = null, Color? color = null)
        {
            if (Game.Instance.UI.BattleLogManager)
            {
                Game.Instance.UI.BattleLogManager.LogView.AddLogEntry(message, color ?? GameLogStrings.Instance.DefaultColor, tooltip, PrefixIcon.None);
            }
            else
            {
                getCombatLogVm()?.LogListModel?.AddLogEntry(message, color ?? GameLogStrings.Instance.DefaultColor, tooltip, PrefixIcon.None);
            }
        }

        [HarmonyPatch(typeof(UnitConfusionController), "TickOnUnit")]
        static class UnitConfusionController_TickOnUnit_Patch
        {
            static void LogState(ConfusionState state, UnitEntityData unit)
            {
                switch (state)
                {
                    case ConfusionState.ActNormally:
                        AddBattleLogMessage($"{unit.CharacterName} sees clearly through its confusion and acts normally");
                        break;
                    case ConfusionState.AttackNearest:
                        AddBattleLogMessage($"{unit.CharacterName} lashes out at the nearest creature due to its confusion");
                        break;
                    case ConfusionState.DoNothing:
                        AddBattleLogMessage($"{unit.CharacterName} stays still out of confusion");
                        break;
                    case ConfusionState.SelfHarm:
                        AddBattleLogMessage($"{unit.CharacterName} hurts itself in its confusion");
                        break;
                    default:
                        AddBattleLogMessage($"{unit.CharacterName} is not confused");
                        break;
                }
            }
            static int Find(List<CodeInstruction> codes, params CodeInstruction[] findingCodes)
            {
                for (int i = 0; i < codes.Count; i++)
                {
                    bool match = true;
                    for (int j = 0; j < findingCodes.Length; j++)
                    {
                        if (i + j >= codes.Count)
                        {
                            match = false;
                            break;
                        }
                        if (codes[i + j].opcode != findingCodes[j].opcode)
                        {
                            match = false;
                            break;
                        }
                        if (findingCodes[j].operand != null && codes[i + j].operand != findingCodes[j].operand)
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match) return i;
                }
                throw new Exception("Could not find codes");
            }
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var retainControl = AccessTools.Method(typeof(UnitPartConfusion), "RetainControl");
                var getState = AccessTools.Property(typeof(UnitPartConfusion), "GetState").GetMethod;
                var logState = AccessTools.Method(typeof(UnitConfusionController_TickOnUnit_Patch), "LogState");
                var codes = instructions.ToList();
                var index = Find(codes,
                        new CodeInstruction(OpCodes.Ldloc_1),
                        new CodeInstruction(OpCodes.Callvirt, getState),
                        new CodeInstruction(OpCodes.Brtrue_S)
                    );
                for (int i = 0; i < codes.Count; i++)
                {
                    if (index == i)
                    {
                        yield return new CodeInstruction(OpCodes.Ldloc_1);
                        yield return new CodeInstruction(OpCodes.Callvirt, getState);
                        yield return new CodeInstruction(OpCodes.Ldarg_1);
                        yield return new CodeInstruction(OpCodes.Call, logState);
                    }
                    yield return codes[i];
                }
            }
        }

    }
}
