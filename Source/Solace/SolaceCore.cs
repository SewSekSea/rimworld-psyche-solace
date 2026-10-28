using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Solace
{
    [DefOf]
    public static class SL_DefOf
    {
        public static ThoughtDef SL_Comforted;
        public static ThoughtDef SL_ComfortedMood;
        public static ThoughtDef SL_CarriedThem;
        public static ThoughtDef SL_HollowComfort;
        public static HediffDef SL_Steadied;
        public static InteractionDef SL_Comfort;

        static SL_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SL_DefOf));
        }
    }

    /// <summary>All the tuning numbers in one place. The ones players can change come from the settings menu.</summary>
    public static class Tuning
    {
        private static SolaceSettings S => SolaceMod.Settings;

        public static float ComfortWeight => S.comfortWeight;
        public static float ComfortStrength => S.comfortStrength;
        public static bool Burnout => S.burnout;
        public static bool Steadying => S.steadying;

        /// <summary>How many comforts, inside the window, earn a colonist the steadied mark.</summary>
        public static int SteadyAfter => S.steadyAfter;

        /// <summary>The comforter must think at least this well of the person to bother.</summary>
        public const int MinOpinionToComfort = 5;

        /// <summary>Comforts older than this stop counting towards being steadied.</summary>
        public const float SteadyWindowDays = 12f;

        /// <summary>Bounds on how much the comforter's quality moves the result.</summary>
        public const float MinQuality = 0.45f;
        public const float MaxQuality = 1.8f;
    }

    [StaticConstructorOnStartup]
    public static class SL
    {
        static SL()
        {
            new Harmony("solace.comfort").PatchAll();
            Log.Message($"[Solace] Loaded.{(GriefBridge.Available ? " Grief found: comfort will move mourning along." : string.Empty)}");
        }

        /// <summary>Alive, grown enough to have a mood, and able to take part.</summary>
        public static bool CanTakePart(Pawn p)
        {
            return p != null && !p.Dead && p.RaceProps != null && p.RaceProps.Humanlike &&
                   p.needs?.mood != null && p.Awake();
        }

        /// <summary>Below their own minor breaking point: the state this mod exists to answer.</summary>
        public static bool IsStruggling(Pawn p)
        {
            if (p?.needs?.mood == null || p.mindState?.mentalBreaker == null)
            {
                return false;
            }
            return p.needs.mood.CurLevel < p.mindState.mentalBreaker.BreakThresholdMinor;
        }

        /// <summary>A Psychopath does not bother. Checked by def name so this mod needs no other mod.</summary>
        public static bool WontBother(Pawn p)
        {
            return p?.story?.traits != null && p.story.traits.HasTrait(TraitDefOf.Psychopath);
        }

        /// <summary>
        /// A Sociopath or Callous colonist can say all the right things and mean none of it.
        /// Soft-checked, so this mod works perfectly well without the Sociopath mod installed.
        /// </summary>
        public static bool ComfortIsHollow(Pawn p)
        {
            TraitDef socio = DefDatabase<TraitDef>.GetNamedSilentFail("SP_Sociopath");
            return socio != null && p?.story?.traits != null && p.story.traits.HasTrait(socio);
        }

        /// <summary>
        /// How good this person is at this, from about 0.45 to 1.8. Social skill, being Kind,
        /// and actually caring about the person in front of them.
        /// </summary>
        public static float Quality(Pawn comforter, Pawn comforted)
        {
            if (comforter == null)
            {
                return 1f;
            }
            float q = 0.7f;

            SkillRecord social = comforter.skills?.GetSkill(SkillDefOf.Social);
            if (social != null && !social.TotallyDisabled)
            {
                q += social.Level * 0.045f;          // up to +0.9 at skill 20
            }
            if (comforter.story?.traits != null && comforter.story.traits.HasTrait(TraitDefOf.Kind))
            {
                q += 0.3f;
            }
            int opinion = comforter.relations == null || comforted == null
                ? 0
                : comforter.relations.OpinionOf(comforted);
            q += Math.Max(-0.2f, Math.Min(0.35f, opinion * 0.004f));

            if (q < Tuning.MinQuality) { return Tuning.MinQuality; }
            return q > Tuning.MaxQuality ? Tuning.MaxQuality : q;
        }
    }

    /// <summary>
    /// A one-way soft link to the Grief mod. Comfort should move real mourning along, not just
    /// sit a good mood on top of it. Found by name at runtime, so Solace never requires Grief and
    /// cannot break if Grief is absent, renamed or changed.
    /// </summary>
    public static class GriefBridge
    {
        private static bool looked;
        private static Type componentType;
        private static MethodInfo easeMethod;

        public static bool Available
        {
            get
            {
                Look();
                return componentType != null && easeMethod != null;
            }
        }

        private static void Look()
        {
            if (looked)
            {
                return;
            }
            looked = true;
            try
            {
                componentType = AccessTools.TypeByName("Grief.GameComponent_Grief");
                if (componentType != null)
                {
                    easeMethod = AccessTools.Method(componentType, "RememberTogether",
                        new[] { typeof(Pawn), typeof(Pawn) });
                }
            }
            catch
            {
                componentType = null;
                easeMethod = null;
            }
        }

        /// <summary>Move this person's mourning along a little, if the Grief mod is present.</summary>
        public static void Ease(Pawn pawn)
        {
            if (!Available || pawn == null || Current.Game == null)
            {
                return;
            }
            try
            {
                List<GameComponent> comps = Current.Game.components;
                for (int i = 0; i < comps.Count; i++)
                {
                    if (comps[i] != null && comps[i].GetType() == componentType)
                    {
                        // Passing the same person twice eases only their own losses.
                        easeMethod.Invoke(comps[i], new object[] { pawn, pawn });
                        return;
                    }
                }
            }
            catch
            {
                // Another mod's internals are not this mod's business to guarantee.
            }
        }
    }
}
