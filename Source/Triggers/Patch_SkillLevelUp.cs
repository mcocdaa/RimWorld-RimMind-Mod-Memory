using HarmonyLib;
using RimMind.Memory.Data;
using RimWorld;
using Verse;

namespace RimMind.Memory.Triggers
{
    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Learn))]
    public static class Patch_SkillLevelUp
    {
        private static int _previousLevel;

        static void Prefix(SkillRecord __instance)
        {
            _previousLevel = __instance.Level;
        }

        static void Postfix(SkillRecord __instance)
        {
            if (__instance.Level <= _previousLevel) return;
            if (!RimMindMemoryMod.Settings.enableMemory) return;
            if (!RimMindMemoryMod.Settings.triggerSkillLevelUp) return;

            try
            {
                var pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
                if (pawn == null || !pawn.IsFreeNonSlaveColonist || pawn.Name == null) return;

                var wc = RimMindMemoryWorldComponent.Instance;
                if (wc == null) return;

                var settings = RimMindMemoryMod.Settings;
                int now = Find.TickManager.TicksGame;

                float importance = __instance.Level >= 15 ? 0.7f : 0.5f;
                string skillLabel = (__instance.def?.LabelCap.RawText.NullOrEmpty() ?? true)
                    ? (__instance.def?.defName ?? "RimMind.Memory.Trigger.Skill".Translate())
                    : __instance.def.LabelCap.RawText;
                string content = "RimMind.Memory.Trigger.SkillUp".Translate(
                    skillLabel, __instance.Level.ToString(), _previousLevel.ToString(), __instance.Level.ToString());

                var store = wc.GetOrCreatePawnStore(pawn);
                store.AddActive(MemoryEntry.Create(content, MemoryType.Event, now, importance),
                    settings.maxActive, settings.maxArchive);

                if (importance >= settings.pawnToNarratorThreshold)
                {
                    wc.NarratorStore.AddActive(
                        MemoryEntry.Create($"[{pawn.Name.ToStringShort}] {content}", MemoryType.Event, now, importance, pawn.ThingID),
                        settings.narratorMaxActive, settings.narratorMaxArchive);
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[RimMind-Memory] Patch_SkillLevelUp error: {ex.Message}");
            }
        }
    }
}
