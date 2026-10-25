using RimWorld;
using UnityEngine;
using Verse;

// 设置、入口、界面相关的，设置也在这里
namespace NivarianBathwaterAle
{
    public sealed class BathwaterAleSettings : ModSettings
    {
        public int TriggerChancePercent = 10;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref TriggerChancePercent, "triggerChancePercent", 10);
            TriggerChancePercent = Mathf.Clamp(TriggerChancePercent, 0, 100);
        }
    }

    public sealed class BathwaterAleMod : Mod
    {
        public static BathwaterAleSettings Settings;
        private static readonly CachedTexture ButtonBackground = new CachedTexture("Nivarian/UI/btn");
        private Vector2 scrollPosition;

        public BathwaterAleMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<BathwaterAleSettings>();
            LongEventHandler.ExecuteWhenFinished(BathwaterAleReward.Initialize);
        }

        public override string SettingsCategory()
        {
            return "BathwaterAle_SettingsCategory".Translate();
        }

        // mod选项卡，我应该单独开一个cs的
        public override void DoSettingsWindowContents(Rect inRect)
        {
            const float sectionWidth = 160f;
            const float sectionGap = 12f;
            const float headerHeight = 36f;
            string title = "BathwaterAle_Basic".Translate();
            Rect sectionRect = new Rect(inRect.x, inRect.y + headerHeight, sectionWidth, 30f);
            DrawButton(sectionRect, title, true);

            Rect titleRect = new Rect(inRect.x + sectionWidth + sectionGap, inRect.y,
                inRect.width - sectionWidth - sectionGap, headerHeight);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, title);
            Text.Font = GameFont.Small;

            Rect viewRect = new Rect(titleRect.x, inRect.y + headerHeight,
                titleRect.width, inRect.height - headerHeight);
            Rect contentRect = new Rect(0f, 0f, viewRect.width - 16f, Mathf.Max(viewRect.height, 160f));
            Widgets.BeginScrollView(viewRect, ref scrollPosition, contentRect);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(contentRect);
            Settings.TriggerChancePercent = Mathf.Clamp(Settings.TriggerChancePercent, 0, 100);
            listing.Label("BathwaterAle_Chance".Translate(Settings.TriggerChancePercent));
            Settings.TriggerChancePercent = Mathf.RoundToInt(listing.Slider(Settings.TriggerChancePercent, 0f, 100f));
            listing.Label("BathwaterAle_ChanceTip".Translate());
            listing.Gap(8f);
            if (DrawButton(listing.GetRect(32f), "BathwaterAle_ResetDefaults".Translate(), false))
                Settings.TriggerChancePercent = 10;
            listing.End();
            Widgets.EndScrollView();
        }

        private static bool DrawButton(Rect rect, string label, bool selected)
        {
            Color previousColor = GUI.color;
            GameFont previousFont = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                if (selected) GUI.color = Color.yellow;
                Widgets.DrawAtlas(rect, ButtonBackground.Texture);
                GUI.color = previousColor;
                return Widgets.ButtonText(rect, label, false, true, true, TextAnchor.MiddleCenter);
            }
            finally
            {
                GUI.color = previousColor;
                Text.Font = previousFont;
            }
        }
    }
}
