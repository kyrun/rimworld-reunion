using System;
using System.Collections.Generic;
using RimWorld;
using Verse;


namespace Kyrun.Reunion
{
	public class Settings : ModSettings
	{
		public enum Event
		{
			WandererJoins,
			RefugeePodCrash,
			RefugeeChased,
			PrisonerRescue,
			DownedRefugee,
		};

		public int minDaysBetweenEvents = GenDate.DaysPerQuadrum/2;
		public int maxDaysBetweenEvents = GenDate.DaysPerQuadrum;

		// Toggle events
		public Dictionary<Event, bool> EventAllow = new Dictionary<Event, bool>()
		{
			{ Event.WandererJoins, true },
			{ Event.RefugeePodCrash, false },
			{ Event.RefugeeChased, true },
			{ Event.PrisonerRescue, true },
			{ Event.DownedRefugee, true },
		};

		public Dictionary<Event, Func<bool>> EventAction = new Dictionary<Event, Func<bool>>()
		{
			{ Event.WandererJoins, IncidentAllyJoin.Do },
			{ Event.RefugeePodCrash, IncidentAllyRefugeePod.Do },
			{ Event.RefugeeChased, IncidentAllyChased.Do },
			{ Event.PrisonerRescue, IncidentAllyPrisonerRescue.Do },
			{ Event.DownedRefugee, IncidentAllyDownedRefugee.Do },
		};

		// Save Mod Settings
		public override void ExposeData()
		{
			base.ExposeData();

			Scribe_Values.Look(ref minDaysBetweenEvents, "minDaysBetweenEvents", 1, true);
			Scribe_Values.Look(ref maxDaysBetweenEvents, "maxDaysBetweenEvents", GenDate.DaysPerQuadrum, true);

			foreach (Event evtType in Enum.GetValues(typeof(Event)))
			{
				bool allowEvent = EventAllow[evtType];
				var saveKey = CreateSaveKey(evtType);
				Scribe_Values.Look(ref allowEvent, saveKey, allowEvent, true);
				EventAllow[evtType] = allowEvent;
			}
		}

		public static string CreateSaveKey(Event eventType)
		{
			return "allowEvent" + eventType;
		}

		public static string CreateTranslationKey(Event eventType)
		{
			return "Reunion.AllowEvent" + eventType;
		}
	}
}
