using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Solace
{
    /// <summary>One occasion of somebody being sat with.</summary>
    public class ComfortRecord : IExposable
    {
        public Pawn pawn;
        public int tick;

        public ComfortRecord() { }

        public ComfortRecord(Pawn pawn)
        {
            this.pawn = pawn;
            tick = Find.TickManager.TicksGame;
        }

        public float AgeDays => (Find.TickManager.TicksGame - tick) / (float)GenDate.TicksPerDay;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref tick, "tick", 0);
        }
    }

    /// <summary>
    /// Keeps count of who has been looked after lately, and hands out the steadied mark to anyone
    /// the colony has held up often enough.
    /// </summary>
    public class GameComponent_Solace : GameComponent
    {
        private const int TickInterval = 1000;

        private List<ComfortRecord> comforts = new List<ComfortRecord>();
        private static GameComponent_Solace instance;

        public GameComponent_Solace(Game game)
        {
            instance = this;
        }

        public static GameComponent_Solace Instance => instance ?? Current.Game?.GetComponent<GameComponent_Solace>();

        public override void FinalizeInit()
        {
            instance = this;
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref comforts, "comforts", LookMode.Deep);
            if (comforts == null)
            {
                comforts = new List<ComfortRecord>();
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                comforts.RemoveAll(c => c == null || c.pawn == null);
            }
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % TickInterval != 0)
            {
                return;
            }
            for (int i = comforts.Count - 1; i >= 0; i--)
            {
                ComfortRecord r = comforts[i];
                if (r == null || r.pawn == null || r.pawn.Dead || r.AgeDays > Tuning.SteadyWindowDays)
                {
                    comforts.RemoveAt(i);
                }
            }
        }

        public void Record(Pawn comforted)
        {
            if (comforted == null)
            {
                return;
            }
            comforts.Add(new ComfortRecord(comforted));
            TrySteady(comforted);
        }

        /// <summary>How many times this person has been sat with inside the window.</summary>
        public int RecentComforts(Pawn p)
        {
            int n = 0;
            for (int i = 0; i < comforts.Count; i++)
            {
                ComfortRecord r = comforts[i];
                if (r != null && r.pawn == p && r.AgeDays <= Tuning.SteadyWindowDays)
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>Enough times, close enough together, and they are genuinely steadier for a while.</summary>
        public void TrySteady(Pawn p)
        {
            if (!Tuning.Steadying || p?.health?.hediffSet == null)
            {
                return;
            }
            if (RecentComforts(p) < Tuning.SteadyAfter)
            {
                return;
            }
            Hediff existing = p.health.hediffSet.GetFirstHediffOfDef(SL_DefOf.SL_Steadied);
            if (existing != null)
            {
                // Already steadied. Being looked after again restarts the clock rather than stacking.
                HediffComp_Disappears comp = existing.TryGetComp<HediffComp_Disappears>();
                if (comp != null)
                {
                    comp.ticksToDisappear = comp.Props.disappearsAfterTicks.RandomInRange;
                }
                return;
            }
            p.health.AddHediff(SL_DefOf.SL_Steadied);
            if (PawnUtility.ShouldSendNotificationAbout(p))
            {
                Messages.Message(
                    $"{p.LabelShortCap} is steadier. People here have sat with them when it mattered.",
                    p, MessageTypeDefOf.PositiveEvent);
            }
        }
    }
}
