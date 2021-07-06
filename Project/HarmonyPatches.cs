using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using HarmonyLib;

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
				GameComponent.PreInit();
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
				GameComponent.PreInit();
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
			static bool Prefix(RimWorld.Planet.WorldPawns __instance, ref Pawn pawn, ref RimWorld.Planet.PawnDiscardDecideMode discardMode)
			{
				if (Current.Game.Info.RealPlayTimeInteracting > 0 && // prevent this from firing when the game hasn't even started proper
					!pawn.Destroyed && // ignore pawns destroyed for whatever reason
					!KidnapUtility.IsKidnapped(pawn) && // don't make kidnapped pawns available; vanilla handles that naturally
					!PawnsFinder.AllCaravansAndTravelingTransportPods_Alive.Contains(pawn) && // ignore caravan/pods
					(pawn.ParentHolder == null || !(pawn.ParentHolder is CompTransporter)) && // ignore pawns in shuttle
					pawn.Faction != Faction.OfPlayer &&
					GameComponent.ListAllySpawned.Contains(pawn.GetUniqueLoadID()))
				{
					if (PawnComponentsUtility.HasSpawnedComponents(pawn))
					{
						PawnComponentsUtility.RemoveComponentsOnDespawned(pawn);
					}
					if (pawn.IsPrisoner) pawn.guest.SetGuestStatus(null, GuestStatus.Guest);
					GameComponent.ReturnToAvailable(pawn, GameComponent.ListAllySpawned, GameComponent.ListAllyAvailable);
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


		/* was previously used to send notifications
		// Reverse patches to use original code
		[HarmonyPatch]
		static class ReversePatch
		{
			[HarmonyReversePatch]
			[HarmonyPatch(typeof(IncidentWorker), "SendStandardLetter")]
			[HarmonyPatch(new Type[] { typeof(TaggedString), typeof(TaggedString), typeof(LetterDef),
			typeof(IncidentParms), typeof(LookTargets), typeof(NamedArgument[]) })]
			public static void SendStandardLetter(object instance, TaggedString baseLetterLabel, TaggedString baseLetterText, LetterDef baseLetterDef,
				IncidentParms parms, LookTargets lookTargets, params NamedArgument[] textArgs)
			{ }
		}
		*/


		// DEBUG MENU ACTION --------------------------------------------------------------------------

		[HarmonyPatch(typeof(Dialog_DebugActionsMenu))]
		[HarmonyPatch(new Type[] { })]
		[HarmonyPatch(MethodType.Constructor)]
		static class Dialog_DebugActionsMenu_DoListingItems_Patch
		{
			const string CATEGORY = "Mod - Reunion (by Kyrun)";
			static void Postfix(Dialog_DebugActionsMenu __instance, ref List<Dialog_DebugActionsMenu.DebugActionOption> ___debugActions)
			{
				// *** Force Start Reunion Event ***
				var debugAddAlly = new Dialog_DebugActionsMenu.DebugActionOption();
				debugAddAlly.actionType = DebugActionType.Action;
				debugAddAlly.label = "Force Start Reunion Event";
				debugAddAlly.category = CATEGORY;
				debugAddAlly.action = delegate
				{
					GameComponent.DecideAndDoEvent();
				};
				___debugActions.Add(debugAddAlly); // add to main list

				// *** Make world pawn "Ally" ***
				debugAddAlly = new Dialog_DebugActionsMenu.DebugActionOption();
				debugAddAlly.actionType = DebugActionType.Action;
				debugAddAlly.label = "Make world pawn \"Ally\"...";
				debugAddAlly.category = CATEGORY;
				debugAddAlly.action = delegate
				{
					List<DebugMenuOption> list = new List<DebugMenuOption>();
					Action<Pawn> actionPawn = delegate (Pawn p)
					{
						if (p != null && p.story != null)
						{
							GameComponent.TryRemoveTrait(p);
							GameComponent.ListAllyAvailable.Add(p);
							Find.WorldPawns.RemovePawn(p);
							Util.Msg(p.Name + " has been removed from the World and added to the Ally list.");
							if (GameComponent.ListAllyAvailable.Count == 1) GameComponent.TryScheduleNextEvent(ScheduleMode.Forced);
						}
					};

					foreach (Pawn current in Find.WorldPawns.AllPawnsAlive)
					{
						Pawn pLocal = current;
						if (current != null && current.story != null) // don't list those already with the trait
						{
							list.Add(new DebugMenuOption(current.LabelShort, DebugMenuOptionMode.Action, delegate
							{
								actionPawn(pLocal);
							}));
						}
					}

					Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
				};
				___debugActions.Add(debugAddAlly); // add to main list

				// *** Print Ally List ***
				var debugPrintAllyList = new Dialog_DebugActionsMenu.DebugActionOption();
				debugPrintAllyList.actionType = DebugActionType.Action;
				debugPrintAllyList.label = "Print \"Ally\" list";
				debugPrintAllyList.category = CATEGORY;
				debugPrintAllyList.action = delegate
				{
					if (GameComponent.ListAllyAvailable.Count > 0)
					{
						Util.PrintAllyList();
					}
					else
					{
						Util.Msg("There are no allies in the Ally list!");
					}
				};
				___debugActions.Add(debugPrintAllyList); // add to main list
			}
		}
	}
}
