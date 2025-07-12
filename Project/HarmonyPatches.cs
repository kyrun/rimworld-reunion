using System;
using System.Collections.Generic;
using UnityEngine;
using RimWorld;
using Verse;
using HarmonyLib;
using RimWorld.Planet;

namespace Kyrun.Reunion
{
    static class HarmonyPatches
    {
        // Called on new game
        [HarmonyPatch(typeof(Game), "InitNewGame")]
        internal static class Verse_Game_InitNewGame
        {
            static void Postfix()
            {
                GameComponent.InitOnNewGame();
                GameComponent.PostInit();
            }
        }


        // Called on load game
        [HarmonyPatch(typeof(Game), "LoadGame")]
        internal static class Verse_Game_LoadGame
        {
            static void Postfix()
            {
                GameComponent.InitOnLoad();
                GameComponent.PostInit();
            }
        }


        // Tick for this mod
        [HarmonyPatch(typeof(TickManager), "DoSingleTick")]
        internal static class TickManager_DoSingleTick
        {
            static void Postfix(TickManager __instance)
            {
                if (GameComponent.NextEventTick > 0 && __instance.TicksGame >= GameComponent.NextEventTick)
                {
                    GameComponent.DecideAndDoEvent();
                }
            }
        }


        // Prevent Reunion pawns from going to World pool
        [HarmonyPatch(typeof(RimWorld.Planet.WorldPawns), "PassToWorld")]
        [HarmonyPatch(new Type[] { typeof(Pawn), typeof(RimWorld.Planet.PawnDiscardDecideMode) })]
        static class WorldPawns_PassToWorld
        {
            static bool IsLeavingInShuttleWithNoDestination(Pawn pawn)
            {
                var parentHolder = pawn.ParentHolder;
                while (parentHolder != null)
                {
                    if (parentHolder is FlyShipLeaving)
                    {
                        var flyShipLeaving = (FlyShipLeaving)parentHolder;
                        if (flyShipLeaving.destinationTile == -1 &&
                            flyShipLeaving.createWorldObject == false)
                        {
                            return true;
                        }
                        break;
                    }
                    parentHolder = parentHolder.ParentHolder;
                }
                return false;
            }

            static bool Prefix(RimWorld.Planet.WorldPawns __instance, ref Pawn pawn, ref RimWorld.Planet.PawnDiscardDecideMode discardMode)
            {
                if (Current.Game.Info.RealPlayTimeInteracting > 0 && // prevent this from firing when the game hasn't even started proper
                    !pawn.Dead && // ignore dead pawns
                    !pawn.Destroyed && // ignore pawns destroyed for whatever reason
                    !KidnapUtility.IsKidnapped(pawn) && // don't make kidnapped pawns available; vanilla handles that naturally
                    !PawnsFinder.AllCaravansAndTravellingTransporters_Alive.Contains(pawn) && // ignore caravan/pods
                    !IsLeavingInShuttleWithNoDestination(pawn) && // ignore pawns in shuttle leaving for what is most likely a quest
                    GameComponent.ListAllySpawned.Contains(pawn.GetUniqueLoadID()))
                {
                    if (PawnComponentsUtility.HasSpawnedComponents(pawn))
                    {
                        PawnComponentsUtility.RemoveComponentsOnDespawned(pawn);
                    }
                    if (pawn.IsPrisoner) pawn.guest.SetGuestStatus(null, GuestStatus.Guest);
                    GameComponent.ReturnToAvailable(pawn);
                    return false;
                }
                return true;
            }
        }


        // On recruit a Reunion pawn, try to schedule next event
        [HarmonyPatch(typeof(InteractionWorker_RecruitAttempt), "DoRecruit")]
        [HarmonyPatch(new Type[] { typeof(Pawn), typeof(Pawn), typeof(bool) })]
        internal static class InteractionWorker_RecruitAttempt_DoRecruit
        {
            static void Postfix(InteractionWorker_RecruitAttempt __instance,
                Pawn recruiter, Pawn recruitee, bool useAudiovisualEffects)
            {
                if (GameComponent.ListAllySpawned.Contains(recruitee.GetUniqueLoadID()))
                {
                    GameComponent.TryScheduleNextEvent(ScheduleMode.Forced);
                }
            }
        }


        // CONFIGURE STARTING PAWNS SCREEN --------------------------------------------------------------

        const string NO_TRAIT_NAME = "Reunion.NoTrait";

        // Create a display name for Reunion traits, use "no trait" string if none
        internal static string GetReunionTraitDisplayName(Trait trait, Pawn pawn = null)
        {
            var displayName = "Reunion: ";

            if (trait == null) displayName += NO_TRAIT_NAME.Translate();
            else
            {
                if (trait.pawn == null && pawn != null)
                {
                    trait.pawn = pawn;
                }
                displayName += trait.Label;
            }

            return displayName;
        }


        // Generate a list of reunion traits, including a null option
        internal static List<Trait> GenerateReunionTraitsOptions()
        {
            var reunionTraitList = new List<Trait>();
            reunionTraitList.Add(null);
            reunionTraitList.AddRange(GameComponent.ReunionTraits);

            return reunionTraitList;
        }


        // Get a Reunion trait (includes null option)
        internal static Trait GetReunionTrait(Pawn pawn)
        {
            var reunionTraitList = GenerateReunionTraitsOptions();

            if (pawn != null && pawn.story != null && pawn.story.traits != null)
            {
                foreach (var reunionTrait in reunionTraitList)
                {
                    if (pawn.story.traits.allTraits.Contains(reunionTrait)) return reunionTrait;
                }
            }
            return null;
        }


        // Used to generate Reunion trait drop down
        internal static IEnumerable<Widgets.DropdownMenuElement<Trait>> GenerateReunionTraitDropDown(Pawn pawn)
        {
            var listDropdownElement = new List<Widgets.DropdownMenuElement<Trait>>();
            using (List<Trait>.Enumerator enumerator = GenerateReunionTraitsOptions().GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    Trait trait = enumerator.Current;

                    listDropdownElement.Add(new Widgets.DropdownMenuElement<Trait>
                    {
                        option = new FloatMenuOption(GetReunionTraitDisplayName(trait, pawn), delegate ()
                        {
                            foreach (var reunionTrait in GameComponent.ReunionTraits)
                            {
                                if (pawn.story.traits.allTraits.Contains(reunionTrait))
                                {
                                    pawn.story.traits.RemoveTrait(reunionTrait);
                                }
                            }

                            if (trait != null)
                            {
                                pawn.story.traits.GainTrait(trait);
                            }
                        }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0),
                        payload = trait
                    });
                }
            }
            return listDropdownElement;
        }


        // Draw the Reunion drop down widget
        [HarmonyPatch(typeof(StartingPawnUtility), "DrawPortraitArea")]
        [HarmonyPatch(new Type[] { typeof(Rect), typeof(int), typeof(bool), typeof(bool) })]
        internal static class StartingPawnUtility_DrawPortraitArea
        {
            static void Postfix(Rect rect, ref int pawnIndex, ref bool renderClothes, ref bool renderHeadgear)
            {
                List<Pawn> StartingAndOptionalPawns = Find.GameInitData.startingAndOptionalPawns;
                if (pawnIndex >= StartingAndOptionalPawns.Count)
                {
                    return;
                }

                Pawn pawn = StartingAndOptionalPawns[pawnIndex];

                if (pawn == null || pawn.story == null || pawn.story.traits == null) return;

                // This will place the widget besides the "Traits" header
                Widgets.BeginGroup(rect);
                Rect buttonRect = new Rect(90, 232, 150f, 20f);

                string currLabel = GetReunionTraitDisplayName(GetReunionTrait(pawn));

                Widgets.Dropdown<Pawn, Trait>(buttonRect, pawn,
                    GetReunionTrait,
                    new Func<Pawn, IEnumerable<Widgets.DropdownMenuElement<Trait>>>(GenerateReunionTraitDropDown),
                    currLabel,
                    null,
                    currLabel,
                    null,
                    null,
                    false);

                Widgets.EndGroup();
            }
        }
    }
}
