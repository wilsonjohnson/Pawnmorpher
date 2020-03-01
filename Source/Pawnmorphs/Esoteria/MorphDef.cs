﻿// MorphDef.cs modified by Iron Wolf for Pawnmorph on 08/02/2019 2:32 PM
// last updated 08/02/2019  2:32 PM

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using JetBrains.Annotations;
using Pawnmorph.Hediffs;
using Pawnmorph.Hybrids;
using Pawnmorph.Utilities;
using RimWorld;
using Verse;

namespace Pawnmorph
{
    /// <summary> Def class for a morph. Used to generate the morph's implicit race. </summary>
    public class MorphDef : AnimalClassBase
    {
        /// <summary>
        ///     The categories that the morph belongs to. <br />
        ///     For example, a Pigmorph belongs to the Farm and Production morph groups.
        /// </summary>
        public List<MorphCategoryDef> categories = new List<MorphCategoryDef>();

        /// <summary>
        ///     The creature this race is a morph of.<br />
        ///     For example, a Wargmorph's race should be Warg.
        /// </summary>
        public ThingDef race;

        /// <summary> If specified, the race to use in place of the implicit one.</summary>
        public ThingDef explicitHybridRace;


        /// <summary>
        ///     The genus of this morph
        ///     this should be a class like 'canis'
        /// </summary>
        public AnimalClassDef classification;

        /// <summary>
        ///     The group the morph belongs to. <br />
        ///     For example, a Huskymorph belongs to the pack, while a Cowmorph is a member of the herd.
        /// </summary>
        public MorphGroupDef group;

        /// <summary> Various settings for the morph's implied race.</summary>
        public HybridRaceSettings raceSettings = new HybridRaceSettings();

        /// <summary> Various settings determining what happens when a pawn is transformed or reverted.</summary>
        public TransformSettings transformSettings = new TransformSettings();

        /// <summary> Aspects that a morph of this race get.</summary>
        public List<AddedAspect> addedAspects = new List<AddedAspect>();

        /// <summary>
        ///     The full transformation chain
        /// </summary>
        [CanBeNull] public HediffDef fullTransformation;

        /// <summary>
        ///     The partial transformation chain
        /// </summary>
        [CanBeNull] public HediffDef partialTransformation;

        /// <summary> The morph's implicit race.</summary>
        [Unsaved] public ThingDef hybridRaceDef;


        [Unsaved] private readonly Dictionary<BodyDef, float> _maxInfluenceCached = new Dictionary<BodyDef, float>();

        [Unsaved] private Dictionary<BodyPartDef, List<MutationDef>> _mutationsByParts;

        [Unsaved] private List<MutationDef> _allAssociatedMutations;

        /// <summary>
        ///     Gets the children.
        /// </summary>
        /// <value>
        ///     The children.
        /// </value>
        public override IEnumerable<AnimalClassBase> Children =>
            Enumerable.Empty<AnimalClassBase>(); //morphs can't have class children

        /// <summary>
        ///     Gets the label.
        /// </summary>
        /// <value>
        ///     The label.
        /// </value>
        public override string Label => label;

        /// <summary>
        ///     Gets the parent class.
        /// </summary>
        /// <value>
        ///     The parent class.
        /// </value>
        public override AnimalClassDef ParentClass => classification;

        /// <summary> Gets an enumerable collection of all the morph type's defs.</summary>
        [NotNull]
        public static IEnumerable<MorphDef> AllDefs => DefDatabase<MorphDef>.AllDefs;


        /// <summary>Gets the collection of all mutations associated with this morph def</summary>
        /// <value>All associated mutations.</value>
        [NotNull]
        public IEnumerable<MutationDef> AllAssociatedMutations
        {
            get
            {
                if (_allAssociatedMutations == null)
                {
                    _allAssociatedMutations = GetAllAssociatedMutations().Distinct().ToList(); 
                }

                return _allAssociatedMutations;
            }
        }



        [NotNull]
        IEnumerable<MutationDef> GetAllAssociatedMutations()
        {
            var set = new HashSet<VTuple<BodyPartDef, MutationLayer>>();
            AnimalClassBase curNode = this;
            List<MutationDef> tmpList = new List<MutationDef>();
            List<VTuple<BodyPartDef, MutationLayer>> tmpSiteLst = new List<VTuple<BodyPartDef, MutationLayer>>();
            while (curNode != null)
            {
                tmpList.Clear();
                foreach (MutationDef mutation in MutationDef.AllMutations) //grab all mutations that give the current influence directly 
                {
                    if (mutation.classInfluence == curNode)
                    {
                        tmpList.Add(mutation);
                    }
                }

                foreach (MutationDef mutationDef in tmpList)
                {
                    tmpSiteLst.Clear();
                    tmpSiteLst.AddRange(mutationDef.GetAllDefMutationSites());
                    bool shouldReject = true; 
                    foreach (VTuple<BodyPartDef, MutationLayer> vTuple in tmpSiteLst)
                    {
                        if (!set.Contains(vTuple))
                        {
                            shouldReject = false; 
                            break;
                        }
                    }

                    if (!shouldReject) //if there are any free slots yield the mutation 
                    {
                        set.AddRange(tmpSiteLst);
                        yield return mutationDef; 
                    }

                }


                curNode = curNode.ParentClass; //move up one in the hierarchy 
            }

        }

        /// <summary>
        ///     get all configuration errors with this instance
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string configError in base.ConfigErrors()) yield return configError;

            if (race == null)
                yield return "No race def found!";
            else if (race.race == null) yield return $"Race {race.defName} has no race properties! Are you sure this is a race?";

            if (classification == null) yield return $"No classification set!"; 
        }

        /// <summary>
        ///     Determines whether this instance contains the object.
        /// </summary>
        /// <param name="other">The other.</param>
        /// <returns>
        ///     <c>true</c> if [contains] [the specified other]; otherwise, <c>false</c>.
        /// </returns>
        public override bool Contains(AnimalClassBase other)
        {
            return other == this;
        }


        /// <summary>Gets the mutation that affect the given part from this morph def</summary>
        /// <param name="partDef">The part definition.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">partDef</exception>
        [NotNull]
        public IEnumerable<MutationDef> GetMutationForPart([NotNull] BodyPartDef partDef)
        {
            if (partDef == null) throw new ArgumentNullException(nameof(partDef));
            if (_mutationsByParts == null)
            {
                _mutationsByParts = new Dictionary<BodyPartDef, List<MutationDef>>();
                foreach (MutationDef mutation in AllAssociatedMutations) //build the lookup dict here 
                foreach (BodyPartDef part in mutation.parts.MakeSafe()) //gets a list of all parts this mutation affects 
                {
                    List<MutationDef> lst;
                    if (_mutationsByParts.TryGetValue(part, out lst))
                    {
                        lst.Add(mutation);
                    }
                    else
                    {
                        lst = new List<MutationDef> {mutation};
                        _mutationsByParts[part] = lst;
                    }
                }
            }

            return _mutationsByParts.TryGetValue(partDef) ?? Enumerable.Empty<MutationDef>();
        }


        /// <summary>
        ///     obsolete, does nothing
        /// </summary>
        /// <param name="food"></param>
        /// <returns></returns>
        [Obsolete("This is no longer used")]
        public FoodPreferability? GetOverride(ThingDef food) //note, RawTasty is 5, RawBad is 4 
        {
            if (food?.ingestible == null) return null;
            foreach (HybridRaceSettings.FoodCategoryOverride foodOverride in raceSettings.foodSettings.foodOverrides)
                if ((food.ingestible.foodType & foodOverride.foodFlags) != 0)
                    return foodOverride.preferability;

            return null;
        }

        /// <summary>
        ///     Determines whether the specified hediff definition is an associated mutation .
        /// </summary>
        /// <param name="hediffDef">The hediff definition.</param>
        /// <returns>
        ///     <c>true</c> if the specified hediff definition is an associated mutation; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">hediffDef</exception>
        [Obsolete]
        public bool IsAnAssociatedMutation([NotNull] HediffDef hediffDef)
        {
            if (hediffDef == null) throw new ArgumentNullException(nameof(hediffDef));
            if (hediffDef is MutationDef mDef) return AllAssociatedMutations.Contains(mDef);

            return false;
        }

        /// <summary>
        ///     Determines whether the specified hediff definition is an associated mutation .
        /// </summary>
        /// <param name="mutationDef">The hediff definition.</param>
        /// <returns>
        ///     <c>true</c> if the specified hediff definition is an associated mutation; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">hediffDef</exception>
        public bool IsAnAssociatedMutation([NotNull] MutationDef mutationDef)
        {
            if (mutationDef == null) throw new ArgumentNullException(nameof(mutationDef));

            return AllAssociatedMutations.Contains(mutationDef);
        }


        /// <summary>
        ///     Determines whether the given hediff is an associated mutation.
        /// </summary>
        /// <param name="hediff">The hediff.</param>
        /// <returns>
        ///     <c>true</c> if the specified hediff is an associated mutation ; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">hediff</exception>
        public bool IsAnAssociatedMutation([NotNull] Hediff hediff)
        {
            if (hediff?.def == null) throw new ArgumentNullException(nameof(hediff));
            if (hediff is Hediff_AddedMutation mutation) return AllAssociatedMutations.Contains(mutation.def as MutationDef);

            return false;
        }

        /// <summary>
        ///     resolves all references after DefOfs are loaded
        /// </summary>
        public override void ResolveReferences()
        {
            if (explicitHybridRace != null)
            {
                hybridRaceDef = explicitHybridRace;
                Log.Warning($"MorphDef {defName} is using an explicit hybrid {explicitHybridRace.defName} for {race.defName}. This has not been tested yet");
            }

            if (_allAssociatedMutations.NullOrEmpty())
            {
                _allAssociatedMutations = GetAllAssociatedMutations().Distinct().ToList(); 
            }

            //TODO patch explicit race based on hybrid race settings? 
        }

        /// <summary> Settings to control what happens when a pawn changes race to this morph type.</summary>
        public class TransformSettings
        {
            /// <summary> The TaleDef that should be used in art that occurs whenever a pawn shifts to this morph.</summary>
            [CanBeNull] public TaleDef transformTale;

            /// <summary> The content of the message that should be spawned when a pawn shifts to this morph.</summary>
            [CanBeNull] public string transformationMessage;

            /// <summary> The message type that should be used when a pawn shifts to this morph.</summary>
            [CanBeNull] public MessageTypeDef messageDef;

            /// <summary> Memory added when a pawn shifts to this morph.</summary>
            [CanBeNull] public ThoughtDef transformationMemory;

            /// <summary>
            ///     Memory added when the pawn reverts from this morph back to human if they have neither the body purist or
            ///     furry traits.
            /// </summary>
            [CanBeNull] public ThoughtDef revertedMemory;

        }

        /// <summary> Aspects to add when a pawn changes race to this morph type and settings asociated with them.</summary>
        public class AddedAspect
        {
            /// <summary> The Def of the aspect to add.</summary>
            public AspectDef def;

            /// <summary> Whether or not the aspect should be kept even if the pawn switches race.</summary>
            public bool keepOnReversion;
        }
    }
}