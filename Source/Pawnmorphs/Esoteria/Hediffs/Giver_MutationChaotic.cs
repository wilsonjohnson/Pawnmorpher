﻿// Giver_MutationChaotic.cs modified by Iron Wolf for Pawnmorph on 08/08/2019 5:36 PM
// last updated 08/08/2019  5:36 PM

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Multiplayer.API;
using Pawnmorph.Utilities;
using RimWorld;
using Verse;

namespace Pawnmorph.Hediffs
{
    /// <summary>
    /// hediff giver based off of HediffGiver_Mutation, but instead of one mutation it gives one of many 
    /// </summary>
    public class Giver_MutationChaotic : HediffGiver
    {
        private List<HediffGiver_Mutation> _possibleMutations;
        /// <summary>
        /// the morphType to get hediff givers from 
        /// </summary>
        public System.Type morphType = typeof(Hediff_Morph); //the hediff type to get possible mutations from 
        /// <summary>
        /// list of morph categories to exclude 
        /// </summary>
        public List<MorphCategoryDef> blackListCategories = new List<MorphCategoryDef>();
        /// <summary>
        /// list of hediff defs to ignore 
        /// </summary>
        public List<HediffDef> blackListDefs = new List<HediffDef>();
        /// <summary>
        /// list of morphs to exclude 
        /// </summary>
        public List<MorphDef> blackListMorphs = new List<MorphDef>();

        bool CheckHediff(HediffDef def)
        {
            if (!morphType.IsAssignableFrom(def.hediffClass)) return false;
            if (blackListDefs.Contains(def)) return false;
            
            return true; 
        }

        bool CheckGiver(HediffGiver_Mutation giver)
        {
            if (giver == null) return false; 
            if (blackListDefs.Contains(giver.hediff)) return false;
            var comp = giver.hediff.CompProps<CompProperties_MorphInfluence>();
            if (comp != null)
            {
                if (blackListMorphs.Contains(comp.morph)) return false;
                foreach (var morphCategory in comp.morph.categories)
                {
                    if (blackListCategories.Contains(morphCategory)) return false;
                }
            }

            return true; 
        }
        /// <summary>
        /// how often to give mutations 
        /// </summary>
        public float mtbDays = 0.4f; 

        List<HediffGiver_Mutation> Mutations //hediff giver doesn't seem to have a on load or resolve references so I'm using lazy initialization
        {
            get
            {
                if (_possibleMutations == null)
                {
                    _possibleMutations = DefDatabase<HediffDef>
                                        .AllDefs.Where(CheckHediff)
                                        .SelectMany(def => def.stages ?? Enumerable.Empty<HediffStage>()) //get all stages 
                                        .SelectMany(s => s.hediffGivers ?? Enumerable.Empty<HediffGiver>()) //get all givers 
                                        .OfType<HediffGiver_Mutation>()//convert to giver_mutation
                                        .Where(CheckGiver)  
                                        .GroupBy(g => g.hediff)
                                        .Select(g => g.First()) //keep only the distinct value 
                                        .ToList(); //make a list to keep around

                    if (_possibleMutations.Count == 0)
                    {
                        Log.Warning($"a ChaoticMutation can't get any mutations to add! either things didn't load or the black lists are too large ");
                    }
                   





                }

                return _possibleMutations; 
            }
        }

        /// <summary>
        /// occurs every so often for all hediffs that have this giver 
        /// </summary>
        /// <param name="pawn"></param>
        /// <param name="cause"></param>
        public override void OnIntervalPassed(Pawn pawn, Hediff cause)
        {
            if (Mutations.Count == 0) return;

            if (MP.IsInMultiplayer)
            {
                Rand.PushState(RandUtilities.MPSafeSeed); 
            }

            if (Rand.MTBEventOccurs(mtbDays, 6000, 60) && pawn.RaceProps.intelligence == Intelligence.Humanlike)
            {
                var mutagen = (cause as Hediff_Morph)?.GetMutagenDef() ?? MutagenDefOf.defaultMutagen; 
                var mut = Mutations[Rand.Range(0, Mutations.Count)]; //grab a random mutation 
                if (mut.TryApply(pawn, mutagen, null, cause))
                {
                    IntermittentMagicSprayer.ThrowMagicPuffDown(pawn.Position.ToVector3(), pawn.MapHeld);
                    if (cause.def.HasComp(typeof(HediffComp_Single)))
                    {
                        pawn.health.RemoveHediff(cause); 
                    }

                    if (mut.tale != null)
                    {
                        TaleRecorder.RecordTale(mut.tale, pawn); 
                    }
                }
            }

            if (MP.IsInMultiplayer)
            {
                Rand.PopState();
            }

        }
    }
}