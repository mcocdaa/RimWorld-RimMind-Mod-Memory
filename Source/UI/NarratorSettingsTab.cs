using RimMind.Core.UI;
using RimMind.Memory.Core;
using RimMind.Memory.Data;
using UnityEngine;
using Verse;

namespace RimMind.Memory.UI
{
    public static class NarratorSettingsTab
    {
        public static void Draw(Rect rect)
        {
            var wc = RimMindMemoryWorldComponent.Instance;
            if (wc == null)
            {
                Widgets.Label(rect, "RimMind.Memory.UI.NotInitialized".Translate());
                return;
            }

            var store = wc.NarratorStore;
            var settings = RimMindMemoryMod.Settings;
            int now = Find.TickManager.TicksGame;

            var listing = new Listing_Standard();
            listing.Begin(rect);

            SettingsUIHelper.DrawSectionHeader(listing, "RimMind.Memory.UI.NarratorMemory".Translate());

            if (store.dark.Count > 0)
            {
                SettingsUIHelper.DrawSectionHeader(listing, "RimMind.Memory.UI.LongTermNarrative".Translate());
                foreach (var d in store.dark)
                    listing.Label($"  {d.content}");
            }

            SettingsUIHelper.DrawSectionHeader(listing, "RimMind.Memory.UI.ActiveNarrative".Translate(store.active.Count, settings.narratorMaxActive));
            int removeIdx = -1;
            for (int i = 0; i < store.active.Count && i < 20; i++)
            {
                var e = store.active[i];
                string timeStr = TimeFormatter.FormatTimeAgo(e.tick, now);
                listing.Label("RimMind.Memory.Time.TimeContent".Translate(timeStr, e.content));
                if (Widgets.ButtonText(new Rect(rect.xMax - 28f, listing.CurHeight - 20f, 24f, 20f), "X"))
                    removeIdx = i;
            }
            if (removeIdx >= 0 && removeIdx < store.active.Count)
                store.active.RemoveAt(removeIdx);

            listing.Gap(8f);
            if (Widgets.ButtonText(new Rect(rect.x, listing.CurHeight, 120f, 30f),
                "RimMind.Memory.UI.ClearAll".Translate()))
            {
                store.active.Clear();
                store.archive.Clear();
            }

            listing.End();
        }
    }
}
