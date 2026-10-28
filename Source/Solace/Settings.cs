using UnityEngine;
using Verse;

namespace Solace
{
    public class SolaceSettings : ModSettings
    {
        public float comfortWeight = 0.5f;
        public float comfortStrength = 1f;
        public int steadyAfter = 4;

        public bool burnout = true;
        public bool steadying = true;

        public override void ExposeData()
        {
            SolaceSettings d = new SolaceSettings();
            Scribe_Values.Look(ref comfortWeight, "comfortWeight", d.comfortWeight);
            Scribe_Values.Look(ref comfortStrength, "comfortStrength", d.comfortStrength);
            Scribe_Values.Look(ref steadyAfter, "steadyAfter", d.steadyAfter);
            Scribe_Values.Look(ref burnout, "burnout", true);
            Scribe_Values.Look(ref steadying, "steadying", true);
        }

        public void Reset()
        {
            SolaceSettings d = new SolaceSettings();
            comfortWeight = d.comfortWeight;
            comfortStrength = d.comfortStrength;
            steadyAfter = d.steadyAfter;
            burnout = steadying = true;
        }
    }

    public class SolaceMod : Mod
    {
        public static SolaceSettings Settings = new SolaceSettings();

        public SolaceMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<SolaceSettings>();
        }

        public override string SettingsCategory() => "Solace";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            SolaceSettings s = Settings;
            Listing_Standard list = new Listing_Standard { ColumnWidth = (inRect.width - 34f) / 2f };
            list.Begin(inRect);

            list.Label("Comfort");
            s.comfortWeight = list.SliderLabeled($"Comfort frequency {s.comfortWeight:0.00}",
                s.comfortWeight, 0f, 1.5f, 0.6f,
                "How readily colonists sit with someone who is struggling. Vanilla chit-chat is 1.00. It can only happen at all when the other person is near their breaking point.");
            s.comfortStrength = list.SliderLabeled($"Comfort helps at {s.comfortStrength:P0}",
                s.comfortStrength, 0.25f, 2f, 0.6f,
                "How much the mood lift is worth. The comforter's social skill, kindness and regard for the person still scale it on top of this.");
            s.steadyAfter = Mathf.RoundToInt(list.SliderLabeled($"Steadied after {s.steadyAfter} comforts",
                s.steadyAfter, 2f, 10f, 0.6f,
                "How many times a colonist has to be looked after, within about twelve days, before it leaves them lastingly steadier."));

            list.NewColumn();
            list.Label("Optional behaviours");
            list.CheckboxLabeled("It costs the comforter", ref s.burnout,
                "Sitting with someone in a bad way takes something out of the person doing it. Switch this off and comforting is free, which makes one high-social colonist an unlimited supply of it.");
            list.CheckboxLabeled("Steadied", ref s.steadying,
                "Being looked after repeatedly leaves a colonist with a lower breaking point for a while.");

            list.Gap(12f);
            list.Label("A Psychopath never comforts anyone. A Sociopath will say all the right things, and it will heal nothing underneath.");

            list.Gap(24f);
            if (list.ButtonText("Reset to defaults"))
            {
                s.Reset();
            }
            list.End();
        }
    }
}
