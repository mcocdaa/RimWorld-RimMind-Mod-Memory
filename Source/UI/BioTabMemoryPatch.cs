using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimMind.Memory.Data;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Memory.UI
{
    [HarmonyPatch(typeof(CharacterCardUtility), "DoTopStack")]
    public static class BioTabMemoryPatch
    {
        private static void AddMemoryButton(Pawn pawn)
        {
            var tmpStackElements = (List<GenUI.AnonymousStackElement>?)
                AccessTools.Field(typeof(CharacterCardUtility), "tmpStackElements")?.GetValue(null);
            if (tmpStackElements == null) return;

            string label = "RimMind.Memory.UI.MemoryTab".Translate();
            float textW = Text.CalcSize(label).x;
            float totalW = textW + 16f;

            tmpStackElements.Add(new GenUI.AnonymousStackElement
            {
                width = totalW,
                drawer = rect =>
                {
                    Widgets.DrawOptionBackground(rect, false);
                    Widgets.DrawHighlightIfMouseover(rect);

                    var wc = RimMindMemoryWorldComponent.Instance;
                    var store = wc?.GetOrCreatePawnStore(pawn);
                    int count = store?.active.Count + store?.archive.Count + store?.dark.Count ?? 0;
                    string tip = count > 0
                        ? "RimMind.Memory.UI.MemorySummary".Translate(
                            store!.active.Count, store.archive.Count, store.dark.Count)
                        : "RimMind.Memory.UI.NoMemory".Translate();
                    TooltipHandler.TipRegion(rect, tip);

                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(rect, label);
                    Text.Anchor = TextAnchor.UpperLeft;

                    if (Widgets.ButtonInvisible(rect))
                        Find.WindowStack.Add(new Dialog_MemoryLog(pawn));
                }
            });
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo anchor = AccessTools.Method(
                typeof(QuestUtility),
                nameof(QuestUtility.AppendInspectStringsFromQuestParts),
                new Type[] { typeof(Action<string, Quest>), typeof(ISelectable), typeof(int).MakeByRefType() });

            foreach (var instr in instructions)
            {
                yield return instr;
                if (anchor != null && instr.Calls(anchor))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(BioTabMemoryPatch), nameof(AddMemoryButton)));
                }
            }
        }
    }
}
