using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Solace
{
    /// <summary>
    /// The mood lift from being comforted, scaled by how good the comforter actually was.
    /// The scale is worked out at the moment it is given and kept on the thought, so a good
    /// word from the right person keeps its value afterwards.
    /// </summary>
    public class Thought_Comforted : Thought_Memory
    {
        public float quality = 1f;

        public override float MoodOffset()
        {
            return base.MoodOffset() * quality * Tuning.ComfortStrength;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref quality, "quality", 1f);
        }
    }

    /// <summary>
    /// Sitting with someone who is struggling. Only available when the recipient is genuinely
    /// near their breaking point and the initiator thinks well enough of them to bother.
    /// </summary>
    public class InteractionWorker_Comfort : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            if (!SL.CanTakePart(initiator) || !SL.CanTakePart(recipient))
            {
                return 0f;
            }
            if (SL.WontBother(initiator))
            {
                return 0f;
            }
            if (!SL.IsStruggling(recipient))
            {
                return 0f;
            }
            // Nobody sits with someone they cannot stand.
            if (initiator.relations != null &&
                initiator.relations.OpinionOf(recipient) < Tuning.MinOpinionToComfort)
            {
                return 0f;
            }
            // Someone in a bad way themselves has little to give.
            if (SL.IsStruggling(initiator))
            {
                return 0f;
            }
            return Tuning.ComfortWeight;
        }

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks,
            out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            letterText = null;
            letterLabel = null;
            letterDef = null;
            lookTargets = null;

            MemoryThoughtHandler theirs = recipient?.needs?.mood?.thoughts?.memories;
            if (theirs == null)
            {
                return;
            }

            // A Sociopath performs it perfectly and means none of it. It lands and heals nothing.
            if (SL.ComfortIsHollow(initiator))
            {
                theirs.TryGainMemory(SL_DefOf.SL_HollowComfort);
                return;
            }

            float quality = SL.Quality(initiator, recipient);

            Thought_Comforted mood = (Thought_Comforted)ThoughtMaker.MakeThought(SL_DefOf.SL_ComfortedMood);
            mood.quality = quality;
            theirs.TryGainMemory(mood);
            theirs.TryGainMemory(SL_DefOf.SL_Comforted, initiator);

            // It moves real mourning along, not only the mood sitting on top of it.
            GriefBridge.Ease(recipient);

            GameComponent_Solace.Instance?.Record(recipient);

            // And it takes something out of the person who did it.
            if (Tuning.Burnout)
            {
                initiator.needs?.mood?.thoughts?.memories?.TryGainMemory(SL_DefOf.SL_CarriedThem);
            }
        }
    }
}
