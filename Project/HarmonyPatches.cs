using System;
using System.Collections.Generic;
using UnityEngine;
using RimWorld;
using Verse;
using HarmonyLib;
using RimWorld.QuestGen;

namespace Kyrun.Reunion
{
    static class HarmonyPatches
    {
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


        // Draw the Reunion dropdown widget to the left of the Randomize button
        [HarmonyPatch(typeof(CharacterCardUtility), "DrawCharacterCard")]
        [HarmonyPatch(new Type[] { typeof(Rect), typeof(Pawn), typeof(Action), typeof(Rect), typeof(bool) })]
        internal static class CharacterCardUtility_DrawCharacterCard
        {
            static void Postfix(Rect rect, ref Pawn pawn, Action randomizeCallback, Rect creationRect, bool showName)
            {
                // If the Randomize button exists, this should be the Character Creation screen.
                // See CharacterCardUtility::DrawCharacterCard line 59.
                if (randomizeCallback == null || pawn == null) return;

                Widgets.BeginGroup(creationRect);

                const float CONTAINER_PADDING = 6f;
                const float RANDOM_BTN_W = 200f;
                const float RANDOM_BTN_H = 30f;
                // This is identical to the Randomize button.
                // See CharacterCardUtility::DrawCharacterCard line 120.
                Rect buttonRect = new Rect(creationRect.width - RANDOM_BTN_W - CONTAINER_PADDING, CONTAINER_PADDING, RANDOM_BTN_W, RANDOM_BTN_H);

                string currLabel = GetReunionTraitDisplayName(GetReunionTrait(pawn));
                const float PADDING = 24f; // arbitrary
                float dropdownButtonW = Text.CalcSize(currLabel).x + PADDING;

                // HACK: offset left of Randomize button and change to new width.
                buttonRect.x -= (dropdownButtonW + CONTAINER_PADDING + CONTAINER_PADDING);
                buttonRect.width = dropdownButtonW;

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


        // Use Reunion's custom QuestPart_PawnsArrive for the Refugee Chased quest.
        [HarmonyPatch(typeof(QuestNode_PawnsArrive), "RunInt")]
        static class QuestNode_PawnsArrive_RunInt
        {
            static IEnumerable<CodeInstruction> Transpiler(
                IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);

                var constructor = AccessTools.Constructor(
                    typeof(RimWorld.QuestPart_PawnsArrive));

                var factory = AccessTools.Method(
                    typeof(QuestNode_PawnsArrive_RunInt),
                    nameof(CreatePawnsArrive));

                int matches = 0;

                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == System.Reflection.Emit.OpCodes.Newobj &&
                        Equals(codes[i].operand, constructor))
                    {
                        // Preserve labels and exception block metadata.
                        var replacement = new CodeInstruction(
                            System.Reflection.Emit.OpCodes.Ldarg_0);

                        replacement.labels.AddRange(codes[i].labels);
                        replacement.blocks.AddRange(codes[i].blocks);

                        codes[i] = replacement;

                        codes.Insert(i + 1, new CodeInstruction(
                            System.Reflection.Emit.OpCodes.Call, factory));

                        matches++;
                        i++;
                    }
                }

                if (matches != 1)
                {
                    Log.Error(
                        "[Reunion] Failed to patch QuestNode_PawnsArrive.RunInt: " +
                        "expected exactly one QuestPart_PawnsArrive constructor, found " +
                        matches + ".");
                }

                return codes;
            }

            static RimWorld.QuestPart_PawnsArrive CreatePawnsArrive(QuestNode_PawnsArrive node)
            {
                if (node is IncidentAllyChased_PawnsArrive)
                {
                    return new Kyrun.Reunion.QuestPart_PawnsArrive
                    {
                        signalListenMode = QuestPart.SignalListenMode.OngoingOrNotYetAccepted
                    };
                }

                return new RimWorld.QuestPart_PawnsArrive();
            }
        }
    }
}
