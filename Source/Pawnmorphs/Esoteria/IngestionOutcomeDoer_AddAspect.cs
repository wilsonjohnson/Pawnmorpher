﻿// IngestionOutcomeDooer_Productive.cs modified by Iron Wolf for Pawnmorph on 10/05/2019 1:04 PM
// last updated 10/05/2019  1:04 PM

using RimWorld;
using Verse;

namespace Pawnmorph
{
    /// <summary>
    /// ingestion out come doer that adds an aspect to a pawn
    /// </summary>
    /// <seealso cref="RimWorld.IngestionOutcomeDoer" />
    public class IngestionOutcomeDoer_AddAspect : IngestionOutcomeDoer
    {
        /// <summary>The aspect to add</summary>
        public AspectDef aspectDef;
        /// If true will increase the stage of the aspect by 1 every time the thing is consumed.
        public bool increaseStage;

        /// <summary>Does the ingestion outcome special.</summary>
        /// <param name="pawn">The pawn.</param>
        /// <param name="ingested">The ingested.</param>
        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested)
        {
            var aspectT = pawn.GetAspectTracker();
            if (aspectT == null) return;

            var aspect = aspectT.GetAspect(aspectDef);
            if (aspect == null)
            {
                aspectT.Add(aspectDef);
            }
            else if (increaseStage)
            {
                aspect.StageIndex += 1;
            }
        }
    }
}
